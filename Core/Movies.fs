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

/// Find subtitle files to delete (non-English, non-French) in directories
/// Optimized: Skip subdirectories like .actors and extrafanart
let private findSubtitlesToDelete (directories: seq<string>) : seq<string> =
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
    |> Seq.filter (fun file -> Subtitle.shouldDelete file.Name)
    |> Seq.map (fun file -> file.FullPath)

/// Clean movie directories - delete small folders and unwanted subtitles
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<string>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Get all leaf directories for subtitle scanning
    let allDirsResult =
        ValidatedPath.create path
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    
    match allDirsResult with
    | Error e -> Error e
    | Ok allDirs ->
        // Try to get directories to delete (might fail if none are small enough)
        let foldersResult =
            allDirs
            |> filterDirectoriesBySize
        
        let foldersToDelete = 
            match foldersResult with
            | Ok folders -> folders
            | Error _ -> Seq.empty
        
        // Find subtitles to delete in all directories (regardless of size)
        let subtitlesToDelete = findSubtitlesToDelete allDirs
        
        // Combine folders and subtitle files
        let allItemsToDelete = Seq.append foldersToDelete subtitlesToDelete
        
        if Seq.isEmpty allItemsToDelete then
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
                
                // Combine results
                match folderResult, subtitleResult with
                | Ok _, Ok _ -> Ok allItemsToDelete
                | Error e, _ -> Error e
                | _, Error e -> Error e
            else
                Ok allItemsToDelete