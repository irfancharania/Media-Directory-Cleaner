module Movies

open System.IO
open FsToolkit.ErrorHandling
open Domain
open Errors
open FileSystem
open Size
open Utility

let ThresholdSizeMB = 100L<MB>

// ============================================================================
// Domain Types for Movies
// ============================================================================

/// Represents context for a file within its directory
/// Example: { File = file.spa.srt; DirectoryFiles = [file.mkv; file.eng.srt; file.spa.srt] }
type FileWithContext = {
    File: ExistingFile
    DirectoryFiles: ExistingFile list
}

/// Classification results from subtitle analysis
/// Example: { ToDelete = [file1.spa.srt; file2.ger.srt]; UncertainSubtitles = ["file3.unknown.srt"] }
type SubtitleAnalysis = {
    ToDelete: DeletableItem list
    UncertainSubtitles: string list
}

/// Optimization statistics for reporting
/// Example: { TotalDirectories = 150; CheckedDirectories = 45; SkippedDirectories = 105 }
type OptimizationStats = {
    TotalDirectories: int
    CheckedDirectories: int
    SkippedDirectories: int
}

module OptimizationStats =
    let create (totalDirs: int) (checkedDirs: int) =
        { TotalDirectories = totalDirs
          CheckedDirectories = checkedDirs
          SkippedDirectories = totalDirs - checkedDirs }
    
    /// Format statistics as a human-readable string
    /// Example: "Optimization: Checking 45 of 150 directories (skipped 105 unchanged)"
    let toString (stats: OptimizationStats) : string =
        $"Optimization: Checking {stats.CheckedDirectories} of {stats.TotalDirectories} directories (skipped {stats.SkippedDirectories} unchanged)"

// ============================================================================
// Subtitle Classification (Pure)
// ============================================================================

type SubtitleClassification =
    | ToDelete of path: string
    | Uncertain of path: string
    | ToKeep

/// Classify a subtitle file based on language detection (pure function)
let private classifySubtitle (fileWithContext: FileWithContext) : SubtitleClassification =
    let file = fileWithContext.File
    let dirFiles = fileWithContext.DirectoryFiles
    
    if Subtitle.matchesVideoFile file.FullPath dirFiles then
        ToKeep
    elif Subtitle.shouldDelete file.Name then
        ToDelete file.FullPath
    elif Subtitle.isUncertain file.Name then
        Uncertain file.FullPath
    else
        ToKeep

/// Classify all subtitles from files with their directory context (pure function)
let private classifySubtitles (filesWithContext: FileWithContext list) : SubtitleAnalysis =
    let classifications =
        filesWithContext
        |> List.filter (fun fwc -> Subtitle.isSubtitleFile fwc.File)
        |> List.map classifySubtitle
    
    let toDelete = 
        classifications 
        |> List.choose (function ToDelete path -> Some (DeletableItem.fromFile path) | _ -> None)
    
    let uncertain = 
        classifications 
        |> List.choose (function Uncertain path -> Some path | _ -> None)
    
    { ToDelete = toDelete; UncertainSubtitles = uncertain }

// ============================================================================
// Directory Analysis (Pure)
// ============================================================================

/// Filter directories that are below the size threshold (pure - takes size function)
let private filterSmallDirectories (getDirSize: string -> int64<MB>) (directories: string list) 
    : DeletableItem list =
    directories
    |> List.filter (fun path -> getDirSize path < ThresholdSizeMB)
    |> List.map DeletableItem.fromDirectory

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Gather all files with their directory context for subtitle classification
let private gatherFilesWithContext (directories: string list) : FileWithContext list =
    directories
    |> List.collect (fun dir ->
        let dirFiles = getFiles dir |> Seq.toList
        
        let subDirFiles = 
            try
                DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                |> Seq.filter (shouldSkipDirectory >> not)
                |> Seq.collect (fun subDir -> 
                    let subFiles = getFiles subDir.FullName |> Seq.toList
                    subFiles |> List.map (fun f -> 
                        { File = f; DirectoryFiles = subFiles }))
                |> Seq.toList
            with
            | _ -> []
        
        let currentDirPairs = 
            dirFiles |> List.map (fun f -> 
                { File = f; DirectoryFiles = dirFiles })
        
        List.append currentDirPairs subDirFiles)

// ============================================================================
// Reporting (Side Effects at Edge)
// ============================================================================

/// Log optimization statistics
let private reportOptimizationStats (stats: OptimizationStats) : unit =
    if stats.SkippedDirectories > 0 then
        Progress.info $"  {OptimizationStats.toString stats}"

/// Log uncertain subtitles in preview mode
let private reportUncertainSubtitles (uncertainSubtitles: string list) : unit =
    if not (List.isEmpty uncertainSubtitles) then
        Progress.info ""
        Progress.info "=== UNCERTAIN SUBTITLES (Review Manually) ==="
        uncertainSubtitles |> List.iter (fun path -> 
            Progress.info $"  [UNCERTAIN] {path}")
        Progress.info ""

// ============================================================================
// Pipeline Helpers
// ============================================================================

/// Execute deletion of items, returning the items on success
let private executeDelete (logFilePath: string) (items: DeletableItem list) : Result<DeletableItem list, DomainError> =
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
let clean (path: string) (previewMode: PreviewMode) (scanMode: ScanMode) : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    let lastRunDate = 
        match scanMode with
        | ScanAll -> None  // Bypass optimization
        | Optimized -> LastRun.tryGetLastRunDate path
    
    // Phase 1: Validate path
    Progress.runResult "Validating path" (fun () -> 
        FileSystem.validatePath path |> Result.liftValidationError)
    
    // Phase 2: Get all subdirectories
    |> Result.bind (Progress.wrap "Scanning directories" (getAllSubdirectories >> Result.liftDirectoryError))
    
    // Phase 3: Filter to leaf nodes
    |> Result.bind (Progress.wrap "Finding leaf nodes" (filterToLeafNodes >> Result.liftDirectoryError))
    
    // Phase 4: Filter to changed directories (optimization)
    |> Result.map (fun allDirs ->
        let allDirsList = allDirs |> Seq.toList
        let dirsToCheck = 
            match scanMode with
            | ScanAll -> 
                Progress.info "  Scan all mode: Checking all directories (optimization disabled)"
                allDirsList
            | Optimized ->
                let filtered = LastRun.filterChangedDirectories lastRunDate allDirsList |> Seq.toList
                let stats = OptimizationStats.create (List.length allDirsList) (List.length filtered)
                reportOptimizationStats stats
                filtered
        dirsToCheck)
    
    // Phase 5: Find small directories AND classify subtitles in ALL changed directories
    |> Result.map (fun dirsToCheck ->
        // Find small directories to delete
        let foldersToDelete = 
            Progress.run "Finding small directories" (fun () ->
                filterSmallDirectories getDirectorySizeMB dirsToCheck)
        
        // Gather files and classify subtitles in ALL directories (not just small ones)
        let filesWithContext = 
            Progress.run "Gathering subtitle info" (fun () -> 
                gatherFilesWithContext dirsToCheck)
        
        let analysis =
            Progress.run "Classifying subtitles" (fun () ->
                classifySubtitles filesWithContext)
        
        // Report uncertain subtitles in preview mode
        if not isExecute then
            reportUncertainSubtitles analysis.UncertainSubtitles
        
        // Combine folders and subtitles
        List.append foldersToDelete analysis.ToDelete
        |> List.distinctBy DeletableItem.path
        |> List.sortBy DeletableItem.path)
    
    // Phase 6: Check if anything to clean
    |> Result.bind (fun items ->
        if List.isEmpty items then
            Error (CleaningError (NothingToClean "No directories or subtitles to clean"))
        else
            Ok items)
    
    // Phase 7: Execute or preview
    |> Result.bind (fun items ->
        if isExecute then
            Progress.wrap "Deleting items" (executeDelete logFilePath) items
            |> Result.tee (fun _ -> LastRun.saveLastRunDate path |> ignore)
        else
            Ok items)
    
    |> Result.map Seq.ofList