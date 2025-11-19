module Music

open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeKB = 500L<kB>

/// Check if directory has any main audio files
let private hasAudioFiles (path: string) : bool =
    let files = getFiles path
    files
    |> Seq.exists (fun file ->
        match file with
        | AudioFile ThresholdSizeKB _ -> true
        | _ -> false)

/// Filter directories that have no audio files
let private filterDirectoriesWithoutAudio (directories: seq<string>) 
    : Result<seq<DeletableItem>, CleaningError> =
    
    let orphanedDirs =
        directories
        |> Seq.filter (hasAudioFiles >> not)
        |> Seq.map DeletableItem.fromDirectory
        |> Seq.toList
        |> List.sortBy DeletableItem.path  // Sort alphabetically for consistent ordering
    
    if List.isEmpty orphanedDirs then
        Error (NothingToClean "No directories without audio files")
    else
        Ok orphanedDirs

/// Clean music directories
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    ValidatedPath.create path 
    |> Result.liftValidationError
    |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
    |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    |> Result.bind (filterDirectoriesWithoutAudio >> Result.liftCleaningError)
    |> Result.teeIf isExecute (fun items -> 
        Logging.logListToFile logFilePath (items |> Seq.map DeletableItem.path))
    |> Result.bind (fun toDelete ->
        if isExecute then
            let dirs = toDelete |> Seq.map DeletableItem.path
            deleteDirectories dirs 
            |> Result.liftCleaningError
            |> Result.map (fun () -> toDelete)
        else
            Ok toDelete)