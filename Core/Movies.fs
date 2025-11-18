module Movies

open System
open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeMB = 100L<MB>

/// Filter directories that are below the size threshold
let private filterDirectoriesBySize (directories: seq<string>) : Result<seq<string>, CleaningError> =
    let smallDirs =
        directories
        |> Seq.filter (fun path -> getDirectorySizeMB path < ThresholdSizeMB)
        |> Seq.toList
    
    if List.isEmpty smallDirs then
        Error (NothingToClean "No directories below size threshold")
    else
        Ok smallDirs

/// Classify subtitle files into categories
type SubtitleClassification =
    | ToDelete of path: string
    | Uncertain of path: string
    | ToKeep

/// Classify a subtitle file based on language detection
let private classifySubtitle (file: ExistingFile) (dirFiles: seq<ExistingFile>) : SubtitleClassification =
    // If subtitle matches video filename, always keep it
    if Subtitle.matchesVideoFile file.FullPath dirFiles then
        ToKeep
    elif Subtitle.shouldDelete file.Name then
        ToDelete file.FullPath
    elif Subtitle.isUncertain file.Name then
        Uncertain file.FullPath
    else
        ToKeep

/// Find and classify subtitle files in directories (single pass)
/// Returns (filesToDelete, uncertainFiles)
let private classifySubtitles (directories: seq<string>) : seq<string> * seq<string> =
    let allSubtitles =
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
            
            // Process current directory files
            let currentSubs = 
                dirFiles 
                |> List.filter Subtitle.isSubtitleFile
                |> List.map (fun f -> classifySubtitle f dirFiles)
            
            // Process subdirectory files
            let subDirSubs = 
                subDirFiles
                |> List.filter (fun (f, _) -> Subtitle.isSubtitleFile f)
                |> List.map (fun (f, filesInSameDir) -> classifySubtitle f filesInSameDir)
            
            List.append currentSubs subDirSubs)
        |> Seq.toList
    
    let toDelete = 
        allSubtitles 
        |> List.choose (function ToDelete path -> Some path | _ -> None)
    
    let uncertain = 
        allSubtitles 
        |> List.choose (function Uncertain path -> Some path | _ -> None)
    
    (toDelete, uncertain)

/// Clean movie directories - delete small folders and unwanted subtitles
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<string>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Check last run date for optimization
    let lastRunDate = LastRun.tryGetLastRunDate path
    
    // Get all leaf directories
    let allDirsResult =
        ValidatedPath.create path
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    
    match allDirsResult with
    | Error e -> Error e
    | Ok allDirs ->
        // Filter to only directories that changed since last run
        let dirsToCheck = LastRun.filterChangedDirectories lastRunDate allDirs
        
        // Log optimization stats if we're skipping directories
        let totalDirs = Seq.length allDirs
        let checkedDirs = Seq.length dirsToCheck
        if checkedDirs < totalDirs then
            Logging.logInfo (sprintf "Optimization: Checking %d of %d directories (skipped %d unchanged)" 
                                    checkedDirs totalDirs (totalDirs - checkedDirs))
        
        // Try to get directories to delete (might fail if none are small enough)
        let foldersResult = dirsToCheck |> filterDirectoriesBySize
        
        let foldersToDelete = 
            match foldersResult with
            | Ok folders -> folders
            | Error _ -> Seq.empty
        
        // Classify subtitles in changed directories (single pass)
        let subtitlesToDelete, uncertainSubtitles = classifySubtitles dirsToCheck
        
        // Report uncertain subtitles in preview mode (inline with deletions)
        if not isExecute && not (Seq.isEmpty uncertainSubtitles) then
            Logging.logInfo "=== UNCERTAIN SUBTITLES (Review Manually) ==="
            uncertainSubtitles |> Seq.iter (fun path -> 
                Logging.logInfo (sprintf "  [UNCERTAIN] %s" path))
            Logging.logInfo ""
        
        // Combine folders and subtitle files
        let allItemsToDelete = 
            Seq.append foldersToDelete subtitlesToDelete
            |> Seq.sort  // Sort alphabetically so related items appear together
        
        if Seq.isEmpty allItemsToDelete then
            // Update last run even if nothing to delete
            if isExecute then
                LastRun.saveLastRunDate path |> ignore
            Error (CleaningError (NothingToClean "No directories or subtitles to clean"))
        else
            if isExecute then
                // Log everything
                Logging.logListToFile logFilePath allItemsToDelete
                
                // Delete folders
                let folderResult = 
                    if Seq.isEmpty foldersToDelete then 
                        Ok ()
                    else
                        deleteDirectories foldersToDelete 
                        |> Result.liftCleaningError
                
                // Delete subtitle files
                let subtitleResult =
                    if Seq.isEmpty subtitlesToDelete then
                        Ok ()
                    else
                        deleteFiles subtitlesToDelete
                        |> Result.liftCleaningError
                
                // Update last run date on success
                match folderResult, subtitleResult with
                | Ok _, Ok _ -> 
                    LastRun.saveLastRunDate path |> ignore
                    Ok allItemsToDelete
                | Error e, _ -> Error e
                | _, Error e -> Error e
            else
                Ok allItemsToDelete