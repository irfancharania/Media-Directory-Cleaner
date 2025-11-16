module Movies

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

/// Check if directory should be skipped (optimization)
/// Dot-prefixed directories' fate is determined by parent folder
let private shouldSkipDirectory (dirInfo: DirectoryInfo) : bool =
    dirInfo.Name.StartsWith(".")

/// Find non-English subtitle files in directories
/// Optimized: Skip subdirectories like .actors and extrafanart
let private findNonEnglishSubtitles (directories: seq<string>) : seq<string> =
    directories
    |> Seq.collect (fun dir ->
        // Get files from current directory
        let currentFiles = getFiles dir
        
        // Get subdirectories, but filter out ones we should skip
        let subDirs = 
            try
                DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                |> Seq.filter (shouldSkipDirectory >> not)
                |> Seq.collect (fun subDir -> getFiles subDir.FullName)
            with
            | _ -> Seq.empty
        
        Seq.append currentFiles subDirs)
    |> Seq.filter Subtitle.isSubtitleFile
    |> Seq.filter (fun file -> Subtitle.isNonEnglish file.Name)
    |> Seq.map (fun file -> file.FullPath)

/// Clean movie directories - delete small folders and non-English subtitles
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<string>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Get directories to delete
    let foldersResult =
        ValidatedPath.create path 
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
        |> Result.bind (filterDirectoriesBySize >> Result.liftCleaningError)
    
    match foldersResult with
    | Ok foldersToDelete ->
        // Also find non-English subtitles in ALL directories (not just small ones)
        let allDirsResult =
            ValidatedPath.create path
            |> Result.liftValidationError
            |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
            |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
        
        let subtitlesToDelete =
            match allDirsResult with
            | Ok allDirs -> findNonEnglishSubtitles allDirs
            | Error _ -> Seq.empty
        
        // Combine folders and subtitle files
        let allItemsToDelete = Seq.append foldersToDelete subtitlesToDelete
        
        if isExecute then
            // Log everything
            Logging.logListToFile logFilePath allItemsToDelete
            
            // Delete folders
            let folderResult = 
                deleteDirectories foldersToDelete 
                |> Result.liftCleaningError
            
            // Delete subtitle files
            let subtitleResult =
                if Seq.isEmpty subtitlesToDelete then
                    Ok ()
                else
                    deleteFiles subtitlesToDelete
                    |> Result.liftCleaningError
            
            // Combine results
            match folderResult, subtitleResult with
            | Ok _, Ok _ -> Ok allItemsToDelete
            | Error e, _ -> Error e
            | _, Error e -> Error e
        else
            Ok allItemsToDelete
    
    | Error e -> Error e