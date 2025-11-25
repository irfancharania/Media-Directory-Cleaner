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
// Domain Types for TV Shows
// ============================================================================

/// Represents different types of TV show directory structures
/// Example: SeasonFolder "Z:\TV\Show Name\Season 01"
///          ShowFolderWithoutSeasons "Z:\TV\Show Name"
type TVShowPath =
    | SeasonFolderPath of path: string
    | ShowFolderWithoutSeasonsPath of path: string

/// Separated TV directories by type
/// Example: { SeasonFolders = ["Season 01", "Season 02"]; ShowFoldersWithoutSeasons = ["Show Root"] }
type LeafDirectoryClassification = {
    SeasonFolders: string list
    ShowFoldersWithoutSeasons: string list
}

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
let internal isShowRootFolder (path: string) : bool =
    try
        File.Exists(Path.Combine(path, "tvshow.nfo"))
    with
    | _ -> false

/// Classify TV directory type based on structure
let internal classifyTVDirectory (path: string) : TVShowPath =
    if isShowRootFolder path then
        ShowFolderWithoutSeasonsPath path
    else
        SeasonFolderPath path

/// Separate leaf directories into season folders and show folders (without seasons)
let internal classifyLeafDirectories (leafDirs: seq<string>) : LeafDirectoryClassification =
    let classified = leafDirs |> Seq.map classifyTVDirectory |> Seq.toList
    
    let seasonFolders = 
        classified 
        |> List.choose (function SeasonFolderPath path -> Some path | _ -> None)
    
    let showFolders = 
        classified 
        |> List.choose (function ShowFolderWithoutSeasonsPath path -> Some path | _ -> None)
    
    { SeasonFolders = seasonFolders
      ShowFoldersWithoutSeasons = showFolders }

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Directory with its associated files
type DirectoryWithFiles = {
    Path: string
    Files: ExistingFile list
}

/// Gather all files for a directory including non-special subdirectories
let private gatherDirectoryFiles (dir: string) : DirectoryWithFiles =
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

    { Path = dir; Files = allFiles }

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
        let separated = 
            Progress.run "Separating show folders" (fun () -> classifyLeafDirectories leafDirs)
        
        // Report show folders without seasons in preview mode
        if not isExecute then
            reportShowFoldersWithoutSeasons separated.ShowFoldersWithoutSeasons
        
        separated.SeasonFolders)
    |> Result.map (Progress.wrapMap "Gathering files" (List.map gatherDirectoryFiles))
    |> Result.map (Progress.wrapMap "Analyzing directories" (
        List.map (fun dwf -> classifyDirectory (dwf.Path, dwf.Files)) 
        >> extractDeletableItems))
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