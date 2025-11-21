module TVShows

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeMB = 100L<MB>

// ============================================================================
// File Classification (Pure)
// ============================================================================

/// Partition files into main files (videos) and extra files
let private partitionFiles (files: seq<ExistingFile>) =
    Utility.Seq.partition (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false) files

/// Remove common suffixes from filenames for matching
let private normalizeFileName (fileName: string) =
    let removeSuffix (suffix: string) (s: string) =
        if s.EndsWith(suffix) then
            s.Substring(0, s.Length - suffix.Length)
        else
            s
    
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
let private findOrphanedFiles (mainFiles: seq<ExistingFile>) (extraFiles: seq<ExistingFile>) 
    : DeletableItem list =
    
    if Seq.isEmpty mainFiles then
        extraFiles 
        |> Seq.map (fun f -> DeletableItem.fromFile f.FullPath)
        |> Seq.toList
    else
        let mainFileNames =
            mainFiles
            |> Seq.map (fun f -> normalizeFileName f.Name)
            |> Set.ofSeq
        
        extraFiles
        |> Seq.filter (fun extraFile ->
            match extraFile with
            | FolderImage _ -> false
            | _ ->
                let normalizedName = normalizeFileName extraFile.Name
                mainFileNames
                |> Set.exists (fun mainName -> mainName.Contains(normalizedName))
                |> not)
        |> Seq.map (fun f -> DeletableItem.fromFile f.FullPath)
        |> Seq.toList

// ============================================================================
// Directory Classification (Pure)
// ============================================================================

type DirectoryClassification =
    | HasVideos of orphanedFiles: DeletableItem list
    | NoVideos of directoryPath: string

/// Classify a directory based on its files - pure function
let private classifyDirectory (dirPath: string) (allFiles: seq<ExistingFile>) : DirectoryClassification =
    let mainFiles, extraFiles = partitionFiles allFiles
    
    if Seq.isEmpty mainFiles then
        NoVideos dirPath
    else
        let orphanedFiles = findOrphanedFiles mainFiles extraFiles
        HasVideos orphanedFiles

/// Extract deletable items from classifications - pure function
let private extractDeletableItems (classifications: DirectoryClassification list) : DeletableItem list =
    let allOrphans = 
        classifications 
        |> List.choose (function 
            | HasVideos files -> Some files 
            | _ -> None)
        |> List.concat
    
    let emptyDirs = 
        classifications 
        |> List.choose (function 
            | NoVideos dir -> Some (DeletableItem.fromDirectory dir)
            | _ -> None)
    
    List.append allOrphans emptyDirs
    |> List.sortBy DeletableItem.path

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Gather all files for a directory including non-special subdirectories
let private gatherDirectoryFiles (dir: string) : ExistingFile list =
    let currentFiles = getFiles dir
    
    let subDirFiles =
        try
            DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            |> Seq.filter (shouldSkipDirectory >> not)
            |> Seq.collect (fun subDir -> getFiles subDir.FullName)
        with
        | _ -> Seq.empty
    
    Seq.append currentFiles subDirFiles |> Seq.toList

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean TV show directories
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Phase 1: Validate path
    let validatedPath =
        Progress.runResult "Validating path" (fun () ->
            FileSystem.validatePath path |> Result.liftValidationError)
    
    match validatedPath with
    | Error e -> Error e
    | Ok validPath ->
        
        // Phase 2: Get all leaf directories
        let leafDirsResult =
            Progress.runResult "Scanning directories" (fun () ->
                getAllSubdirectories validPath |> Result.liftDirectoryError)
            |> Result.bind (fun dirs ->
                Progress.runResult "Finding leaf nodes" (fun () ->
                    filterToLeafNodes dirs |> Result.liftDirectoryError))
        
        match leafDirsResult with
        | Error e -> Error e
        | Ok leafDirs ->
            
            // Phase 3: Classify directories and find orphaned files
            let classifications =
                Progress.run "Analyzing directories" (fun () ->
                    leafDirs
                    |> Seq.map (fun dir -> 
                        let files = gatherDirectoryFiles dir
                        classifyDirectory dir files)
                    |> Seq.toList)
            
            let itemsToDelete = extractDeletableItems classifications
            
            if List.isEmpty itemsToDelete then
                Error (CleaningError (NothingToClean "No orphaned files or empty directories found"))
            else
                if isExecute then
                    // Log before deleting
                    Logging.logListToFile logFilePath (itemsToDelete |> Seq.map DeletableItem.path)
                    
                    // Phase 4: Execute deletions
                    let files = 
                        itemsToDelete 
                        |> List.choose (function DeletableItem.File p -> Some p | _ -> None)
                    let dirs = 
                        itemsToDelete 
                        |> List.choose (function DeletableItem.Directory p -> Some p | _ -> None)
                    
                    let deleteResult =
                        Progress.runResult "Deleting items" (fun () ->
                            // Delete files first, then directories
                            let fileResult = 
                                if List.isEmpty files then Ok ()
                                else deleteFiles files |> Result.liftCleaningError
                            
                            let dirResult = 
                                if List.isEmpty dirs then Ok ()
                                else deleteDirectories dirs |> Result.liftCleaningError
                            
                            match fileResult, dirResult with
                            | Ok _, Ok _ -> Ok ()
                            | Error e, _ -> Error e
                            | _, Error e -> Error e)
                    
                    match deleteResult with
                    | Ok () -> Ok (itemsToDelete |> List.toSeq)
                    | Error e -> Error e
                else
                    Ok (itemsToDelete |> List.toSeq)