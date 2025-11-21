module Movies

open System
open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeMB = 100L<MB>

// ============================================================================
// Directory Filtering (Pure)
// ============================================================================

/// Filter directories that are below the size threshold
let private filterDirectoriesBySize (getDirSize: string -> int64<MB>) (directories: seq<string>) 
    : seq<string> =
    directories
    |> Seq.filter (fun path -> getDirSize path < ThresholdSizeMB)

// ============================================================================
// Subtitle Classification (Pure)
// ============================================================================

type SubtitleClassification =
    | ToDelete of path: string
    | Uncertain of path: string
    | ToKeep

/// Classify a subtitle file based on language detection (pure function)
let private classifySubtitle (file: ExistingFile) (dirFiles: seq<ExistingFile>) : SubtitleClassification =
    if Subtitle.matchesVideoFile file.FullPath dirFiles then
        ToKeep
    elif Subtitle.shouldDelete file.Name then
        ToDelete file.FullPath
    elif Subtitle.isUncertain file.Name then
        Uncertain file.FullPath
    else
        ToKeep

/// Classify all subtitles in a list of files (pure function)
let private classifySubtitlesInFiles (filesWithContext: seq<ExistingFile * ExistingFile list>) 
    : SubtitleClassification list =
    filesWithContext
    |> Seq.filter (fun (f, _) -> Subtitle.isSubtitleFile f)
    |> Seq.map (fun (f, dirFiles) -> classifySubtitle f dirFiles)
    |> Seq.toList

/// Partition classifications into deletable items and uncertain paths (pure function)
let private partitionClassifications (classifications: SubtitleClassification list) 
    : DeletableItem list * string list =
    let toDelete = 
        classifications 
        |> List.choose (function ToDelete path -> Some (DeletableItem.fromFile path) | _ -> None)
    let uncertain = 
        classifications 
        |> List.choose (function Uncertain path -> Some path | _ -> None)
    (toDelete, uncertain)

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Gather all files with their directory context for subtitle classification
let private gatherFilesWithContext (directories: seq<string>) : seq<ExistingFile * ExistingFile list> =
    directories
    |> Seq.collect (fun dir ->
        let dirFiles = getFiles dir |> Seq.toList
        
        let subDirFiles = 
            try
                DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                |> Seq.filter (shouldSkipDirectory >> not)
                |> Seq.collect (fun subDir -> 
                    let subFiles = getFiles subDir.FullName |> Seq.toList
                    subFiles |> Seq.map (fun f -> (f, subFiles)))
                |> Seq.toList
            with
            | _ -> []
        
        let currentDirPairs = dirFiles |> List.map (fun f -> (f, dirFiles))
        
        Seq.append currentDirPairs subDirFiles)

// ============================================================================
// Reporting (Side Effects at Edge)
// ============================================================================

/// Log optimization statistics
let private reportOptimizationStats (allDirs: seq<string>) (dirsToCheck: seq<string>) : unit =
    let totalDirs = Seq.length allDirs
    let checkedDirs = Seq.length dirsToCheck
    if checkedDirs < totalDirs then
        Progress.info $"  Optimization: Checking {checkedDirs} of {totalDirs} directories (skipped {totalDirs - checkedDirs} unchanged)"

/// Log uncertain subtitles in preview mode
let private reportUncertainSubtitles (uncertainSubtitles: string list) : unit =
    if not (List.isEmpty uncertainSubtitles) then
        Progress.info ""
        Progress.info "=== UNCERTAIN SUBTITLES (Review Manually) ==="
        uncertainSubtitles |> List.iter (fun path -> 
            Progress.info $"  [UNCERTAIN] {path}")

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean movie directories - delete small folders and unwanted subtitles
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
        let allDirsResult =
            Progress.runResult "Scanning directories" (fun () ->
                getAllSubdirectories validPath |> Result.liftDirectoryError)
            |> Result.bind (fun dirs ->
                Progress.runResult "Finding leaf nodes" (fun () ->
                    filterToLeafNodes dirs |> Result.liftDirectoryError))
        
        match allDirsResult with
        | Error e -> Error e
        | Ok allDirs ->
            
            // Phase 3: Filter to changed directories (optimization)
            let lastRunDate = LastRun.tryGetLastRunDate path
            let dirsToCheck = 
                Progress.run "Checking for changes" (fun () ->
                    LastRun.filterChangedDirectories lastRunDate allDirs |> Seq.toList)
            
            reportOptimizationStats allDirs dirsToCheck
            
            // Phase 4: Find small directories to delete
            let foldersToDelete = 
                Progress.run "Finding small directories" (fun () ->
                    dirsToCheck 
                    |> filterDirectoriesBySize getDirectorySizeMB
                    |> Seq.map DeletableItem.fromDirectory
                    |> Seq.toList)
            
            // Phase 5: Classify subtitles
            let subtitlesToDelete, uncertainSubtitles = 
                Progress.run "Classifying subtitles" (fun () ->
                    let filesWithContext = gatherFilesWithContext dirsToCheck
                    let classifications = classifySubtitlesInFiles filesWithContext
                    partitionClassifications classifications)
            
            // Report uncertain subtitles in preview mode
            if not isExecute then
                reportUncertainSubtitles uncertainSubtitles
            
            // Combine and sort results
            let allItemsToDelete = 
                List.append foldersToDelete subtitlesToDelete
                |> List.sortBy DeletableItem.path
            
            if List.isEmpty allItemsToDelete then
                if isExecute then
                    LastRun.saveLastRunDate path |> ignore
                Error (CleaningError (NothingToClean "No directories or subtitles to clean"))
            else
                if isExecute then
                    // Phase 6: Execute deletions
                    let folders = 
                        allItemsToDelete 
                        |> List.choose (function DeletableItem.Directory p -> Some p | _ -> None)
                    let files = 
                        allItemsToDelete 
                        |> List.choose (function DeletableItem.File p -> Some p | _ -> None)
                    
                    // Log before deleting
                    Logging.logListToFile logFilePath (allItemsToDelete |> Seq.map DeletableItem.path)
                    
                    let deleteResult =
                        Progress.runResult "Deleting items" (fun () ->
                            let folderResult = 
                                if List.isEmpty folders then Ok ()
                                else deleteDirectories folders |> Result.liftCleaningError
                            let fileResult =
                                if List.isEmpty files then Ok ()
                                else deleteFiles files |> Result.liftCleaningError
                            
                            match folderResult, fileResult with
                            | Ok _, Ok _ -> Ok ()
                            | Error e, _ -> Error e
                            | _, Error e -> Error e)
                    
                    match deleteResult with
                    | Ok () -> 
                        LastRun.saveLastRunDate path |> ignore
                        Ok (allItemsToDelete |> List.toSeq)
                    | Error e -> Error e
                else
                    Ok (allItemsToDelete |> List.toSeq)