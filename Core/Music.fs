module Music

open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size
open Errors
open Utility

let ThresholdSizeKB = 500L<kB>

// ============================================================================
// Directory Classification (Pure)
// ============================================================================

/// Check if a list of files contains any audio files - pure function
let private containsAudioFiles (files: ExistingFile list) : bool =
    files |> List.exists (fun file ->
        match file with
        | AudioFile ThresholdSizeKB _ -> true
        | _ -> false)

/// Filter to directories without audio files - pure function
let private findDirectoriesWithoutAudio (dirsWithFiles: (string * ExistingFile list) list) 
    : DeletableItem list =
    dirsWithFiles
    |> List.filter (fun (_, files) -> not (containsAudioFiles files))
    |> List.map (fun (dir, _) -> DeletableItem.fromDirectory dir)
    |> List.sortBy DeletableItem.path

// ============================================================================
// File Gathering (Infrastructure)
// ============================================================================

/// Gather files for each directory
let private gatherFilesForDirectory (dir: string) : string * ExistingFile list =
    (dir, getFiles dir |> Seq.toList)

// ============================================================================
// Pipeline Helpers
// ============================================================================

/// Execute deletion of directories, returning the items on success
let private executeDelete (logFilePath: string) (items: DeletableItem list) : Result<DeletableItem list, DomainError> =
    Logging.logListToFile logFilePath (items |> Seq.map DeletableItem.path)
    
    let dirs = items |> List.map DeletableItem.path
    
    deleteDirectories dirs
    |> Result.liftCleaningError
    |> Result.map (fun () -> items)

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean music directories
let clean (path: string) (previewMode: PreviewMode) : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    Progress.runResult "Validating path" (fun () ->
        validatePath path |> Result.liftValidationError)
    |> Result.bind (Progress.wrap "Scanning directories" (getAllSubdirectories >> Result.liftDirectoryError))
    |> Result.bind (Progress.wrap "Finding leaf nodes" (filterToLeafNodes >> Result.liftDirectoryError))
    |> Result.map (Progress.wrapMap "Scanning for audio files" (Seq.map gatherFilesForDirectory >> Seq.toList))
    |> Result.map (Progress.wrapMap "Finding empty directories" findDirectoriesWithoutAudio)
    |> Result.bind (fun items ->
        if List.isEmpty items then
            Error (CleaningError (NothingToClean "No directories without audio files"))
        else
            Ok items)
    |> Result.bind (fun items ->
        if isExecute then
            Progress.wrap "Deleting directories" (executeDelete logFilePath) items
        else
            Ok items)
    |> Result.map Seq.ofList