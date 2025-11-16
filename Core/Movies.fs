module Movies

open System
open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeMB = 100L<MB>

/// Check if directory has any video files
let private hasVideoFiles (path: string) : bool =
    getFiles path
    |> Seq.exists (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false)

/// Filter directories that are below size threshold and have no video files
let private filterSmallDirectoriesWithoutVideo (directories: seq<string>) : seq<string> =
    directories
    |> Seq.filter (fun path -> 
        getDirectorySizeMB path < ThresholdSizeMB && not (hasVideoFiles path))

/// Find non-English subtitle files in all directories
/// Only search top-level directory, skip .actors and extrafanart as they'll be deleted with parent
let private findNonEnglishSubtitles (directories: seq<string>) : seq<string> =
    directories
    |> Seq.collect (fun dir ->
        getFiles dir
        |> Seq.filter Subtitle.isSubtitleFile
        |> Seq.filter (fun file -> Subtitle.isNonEnglish file.Name)
        |> Seq.map (fun file -> file.FullPath))

/// Clean movie directories - delete small folders without video and non-English subtitles
/// Runs iteratively: metadata deletion → empty folder deletion on next run
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<string>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Get all leaf directories
    let leafDirsResult =
        ValidatedPath.create path 
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    
    match leafDirsResult with
    | Ok leafDirs ->
        // Find small folders without video (orphaned metadata folders)
        let foldersToDelete = 
            filterSmallDirectoriesWithoutVideo leafDirs
            |> Seq.toList
        
        // Find non-English subtitles in all leaf directories
        // (folders being deleted will include their subtitles anyway)
        let subtitlesToDelete = 
            findNonEnglishSubtitles leafDirs
            |> Seq.toList
        
        // Combine all items
        let allItemsToDelete = List.append foldersToDelete subtitlesToDelete
        
        if List.isEmpty allItemsToDelete then
            Error (DomainError.CleaningError (NothingToClean "No orphaned folders or non-English subtitles found"))
        else
            if isExecute then
                // Log everything
                Logging.logListToFile logFilePath allItemsToDelete
                
                // Delete folders
                let folderResult = 
                    if List.isEmpty foldersToDelete then Ok ()
                    else deleteDirectories foldersToDelete |> Result.liftCleaningError
                
                // Delete subtitle files
                let subtitleResult =
                    if List.isEmpty subtitlesToDelete then Ok ()
                    else deleteFiles subtitlesToDelete |> Result.liftCleaningError
                
                // Combine results
                match folderResult, subtitleResult with
                | Ok _, Ok _ -> Ok allItemsToDelete
                | Error e, _ -> Error e
                | _, Error e -> Error e
            else
                Ok allItemsToDelete
    
    | Error e -> Error e