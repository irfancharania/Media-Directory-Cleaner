module TVShows

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size
open Errors
open Utility

let ThresholdSizeMB = 100L<MB>

// ============================================================================
// File Classification (Pure)
// ============================================================================

/// Partition files into main files (videos) and extra files
let private partitionFiles (files: ExistingFile list) =
    files |> List.partition (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false)

/// Remove common suffixes from filenames for matching
let private normalizeFileName (fileName: string) =
    let removeSuffix (suffix: string) (s: string) =
        if s.EndsWith(suffix) then s.Substring(0, s.Length - suffix.Length) else s
    
    let removeRippingGroup (s: string) =
        Regex.Replace(s, @"\s\([\w\.\-\s\,]+\)?$", String.Empty)
    
    fileName
    |> Path.GetFileNameWithoutExtension
    |> removeSuffix ".en"
    |> removeSuffix ".eng"
    |> removeSuffix ".english"
    |> removeSuffix "-thumb"
    |> removeRippingGroup

/// Find orphaned extra files (no corresponding main video file) - pure function
let private findOrphanedFiles (mainFiles: ExistingFile list) (extraFiles: ExistingFile list) 
    : DeletableItem list =
    
    if List.isEmpty mainFiles then
        extraFiles |> List.map (fun f -> DeletableItem.fromFile f.FullPath)
    else
        let mainFileNames = mainFiles |> List.map (fun f -> normalizeFileName f.Name) |> Set.ofList
        
        extraFiles
        |> List.filter (fun extraFile ->
            // Always keep folder images when video files are present
            let isFolderImage = 
                let name = extraFile.Name.ToLowerInvariant()
                name.StartsWith("folder") || name.StartsWith("poster")
            
            if isFolderImage then
                false  // Keep folder images
            else
                let normalizedName = normalizeFileName extraFile.Name
                mainFileNames |> Set.exists (fun mainName -> mainName.Contains(normalizedName)) |> not)
        |> List.map (fun f -> DeletableItem.fromFile f.FullPath)

// ============================================================================
// Directory Classification (Pure)
// ============================================================================

type DirectoryClassification =
    | HasVideos of orphanedFiles: DeletableItem list
    | NoVideos of directoryPath: string

/// Classify a directory based on its files - pure function
let private classifyDirectory (dirPath: string, files: ExistingFile list) : DirectoryClassification =
    let mainFiles, extraFiles = partitionFiles files
    
    if List.isEmpty mainFiles then
        NoVideos dirPath
    else
        HasVideos (findOrphanedFiles mainFiles extraFiles)

/// Extract deletable items from classifications - pure function
let private extractDeletableItems (classifications: DirectoryClassification list) : DeletableItem list =
    let orphans = 
        classifications 
        |> List.collect (function HasVideos files -> files | _ -> [])
    
    let emptyDirs = 
        classifications 
        |> List.choose (function NoVideos dir -> Some (DeletableItem.fromDirectory dir) | _ -> None)
    
    List.append orphans emptyDirs |> List.sortBy DeletableItem.path

// ============================================================================
// Show Folder Detection (Pure)
// ============================================================================

/// Check if directory contains a tvshow.nfo file (indicator of show root)
let private isShowRootFolder (path: string) : bool =
    try
        File.Exists(Path.Combine(path, "tvshow.nfo"))
    with
    | _ -> false

/// Separate leaf directories into season folders and show folders (without seasons)
let private separateShowFolders (leafDirs: seq<string>) : string list * string list =
    let leafList = leafDirs |> Seq.toList
    let showFolders = leafList |> List.filter isShowRootFolder
    let seasonFolders = leafList |> List.filter (isShowRootFolder >> not)
    (seasonFolders, showFolders)

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Gather all files for a directory including non-special subdirectories
let private gatherDirectoryFiles (dir: string) : string * ExistingFile list =
    let currentFiles = getFiles dir |> Seq.toList
    
    let subDirFiles =
        try
            DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            |> Seq.filter (shouldSkipDirectory >> not)
            |> Seq.collect (fun subDir -> getFiles subDir.FullName)
            |> Seq.toList
        with
        | _ -> []
    
    let allFiles = List.append currentFiles subDirFiles

    (dir, allFiles)

// ============================================================================
// Reporting (Side Effects at Edge)
// ============================================================================

/// Log TV show folders without seasons in preview mode
let private reportShowFoldersWithoutSeasons (showFolders: string list) : unit =
    if not (List.isEmpty showFolders) then
        Progress.info ""
        Progress.info "=== TV SHOW FOLDERS WITHOUT SEASONS (Review Manually) ==="
        showFolders |> List.iter (fun path -> 
            Progress.info $"  [NO SEASONS] {path}")
        Progress.info ""

// ============================================================================
// Pipeline Helpers
// ============================================================================

/// Execute deletion of items, returning the items on success
let private executeDelete (logFilePath: string) (items: DeletableItem list) : Result<DeletableItem list, DomainError> =
    Logging.logListToFile logFilePath (items |> Seq.map DeletableItem.path)
    
    let files = items |> List.choose (function DeletableItem.File p -> Some p | _ -> None)
    let dirs = items |> List.choose (function DeletableItem.Directory p -> Some p | _ -> None)
    
    let fileResult = 
        if List.isEmpty files then Ok () 
        else deleteFiles files |> Result.liftCleaningError
    
    let dirResult = 
        if List.isEmpty dirs then Ok () 
        else deleteDirectories dirs |> Result.liftCleaningError
    
    match fileResult, dirResult with
    | Ok _, Ok _ -> Ok items
    | Error e, _ -> Error e
    | _, Error e -> Error e

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean TV show directories
let clean (path: string) (previewMode: PreviewMode) : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    Progress.runResult "Validating path" (fun () ->
        FileSystem.validatePath path |> Result.liftValidationError)
    |> Result.bind (Progress.wrap "Scanning directories" (getAllSubdirectories >> Result.liftDirectoryError))
    |> Result.bind (Progress.wrap "Finding leaf nodes" (filterToLeafNodes >> Result.liftDirectoryError))
    |> Result.map (fun leafDirs ->
        let seasonFolders, showFolders = 
            Progress.run "Separating show folders" (fun () -> separateShowFolders leafDirs)
        
        // Report show folders without seasons in preview mode
        if not isExecute then
            reportShowFoldersWithoutSeasons showFolders
        
        seasonFolders)
    |> Result.map (Progress.wrapMap "Gathering files" (List.map gatherDirectoryFiles))
    |> Result.map (Progress.wrapMap "Analyzing directories" (List.map classifyDirectory >> extractDeletableItems))
    |> Result.bind (fun items ->
        if List.isEmpty items then
            Error (CleaningError (NothingToClean "No orphaned files or empty directories found"))
        else
            Ok items)
    |> Result.bind (fun items ->
        if isExecute then
            Progress.wrap "Deleting items" (executeDelete logFilePath) items
        else
            Ok items)
    |> Result.map Seq.ofList