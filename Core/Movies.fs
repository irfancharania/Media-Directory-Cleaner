module Movies

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
let private gatherFilesWithContext (directories: seq<string>) : (ExistingFile * ExistingFile list) list =
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
    |> Seq.toList

// ============================================================================
// Reporting (Side Effects at Edge)
// ============================================================================

/// Log optimization statistics
let private reportOptimizationStats (totalCount: int) (checkedCount: int) : unit =
    if checkedCount < totalCount then
        Progress.info $"  Optimization: Checking {checkedCount} of {totalCount} directories (skipped {totalCount - checkedCount} unchanged)"

/// Log uncertain subtitles in preview mode
let private reportUncertainSubtitles (uncertainSubtitles: string list) : unit =
    if not (List.isEmpty uncertainSubtitles) then
        Progress.info ""
        Progress.info "=== UNCERTAIN SUBTITLES (Review Manually) ==="
        uncertainSubtitles |> List.iter (fun path -> 
            Progress.info $"  [UNCERTAIN] {path}")

// ============================================================================
// Pipeline Helpers
// ============================================================================

/// Execute deletion of items, returning the items on success
let private executeDelete (logFilePath: string) (items: DeletableItem list) : Result<DeletableItem list, DomainError> =
    // Log before deleting
    Logging.logListToFile logFilePath (items |> Seq.map DeletableItem.path)
    
    let folders = items |> List.choose (function DeletableItem.Directory p -> Some p | _ -> None)
    let files = items |> List.choose (function DeletableItem.File p -> Some p | _ -> None)
    
    let folderResult = 
        if List.isEmpty folders then Ok ()
        else deleteDirectories folders |> Result.liftCleaningError
    
    let fileResult =
        if List.isEmpty files then Ok ()
        else deleteFiles files |> Result.liftCleaningError
    
    match folderResult, fileResult with
    | Ok _, Ok _ -> Ok items
    | Error e, _ -> Error e
    | _, Error e -> Error e

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean movie directories - delete small folders and unwanted subtitles
let clean (path: string) (previewMode: PreviewMode) : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    let lastRunDate = LastRun.tryGetLastRunDate path
    
    Progress.runResult "Validating path" (fun () -> 
        FileSystem.validatePath path |> Result.liftValidationError)
    |> Result.bind (Progress.wrap "Scanning directories" (getAllSubdirectories >> Result.liftDirectoryError))
    |> Result.bind (Progress.wrap "Finding leaf nodes" (filterToLeafNodes >> Result.liftDirectoryError))
    |> Result.map (fun allDirs ->
        let dirsToCheck = 
            allDirs
            |> LastRun.filterChangedDirectories lastRunDate 
            |> Seq.toList
        reportOptimizationStats (Seq.length allDirs) (List.length dirsToCheck)
        dirsToCheck)
    |> Result.map (Progress.wrapMap "Finding small directories" (fun dirs ->
        dirs 
        |> filterDirectoriesBySize getDirectorySizeMB
        |> Seq.map DeletableItem.fromDirectory
        |> Seq.toList))
    |> Result.map (fun foldersToDelete ->
        let filesWithContext = 
            Progress.run "Gathering subtitle info" (fun () -> 
                gatherFilesWithContext (foldersToDelete |> List.map DeletableItem.path |> List.distinct))
        let subtitlesToDelete, uncertainSubtitles =
            filesWithContext
            |> classifySubtitlesInFiles
            |> partitionClassifications
        
        if not isExecute then
            reportUncertainSubtitles uncertainSubtitles
        
        List.append foldersToDelete subtitlesToDelete
        |> List.distinctBy DeletableItem.path
        |> List.sortBy DeletableItem.path)
    |> Result.bind (fun items ->
        if List.isEmpty items then
            Error (CleaningError (NothingToClean "No directories or subtitles to clean"))
        else
            Ok items)
    |> Result.bind (fun items ->
        if isExecute then
            Progress.wrap "Deleting items" (executeDelete logFilePath) items
            |> Result.tee (fun _ -> LastRun.saveLastRunDate path |> ignore)
        else
            Ok items)
    |> Result.map Seq.ofList