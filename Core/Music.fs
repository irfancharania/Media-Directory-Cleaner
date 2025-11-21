module Music

open System.IO
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeKB = 500L<kB>

// ============================================================================
// Directory Classification (Pure)
// ============================================================================

/// Check if a list of files contains any audio files - pure function
let private containsAudioFiles (files: ExistingFile list) : bool =
    files
    |> List.exists (fun file ->
        match file with
        | AudioFile ThresholdSizeKB _ -> true
        | _ -> false)

/// Filter directories to those without audio files - pure function
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
let private gatherFilesForDirectories (directories: seq<string>) : (string * ExistingFile list) list =
    directories
    |> Seq.map (fun dir -> (dir, getFiles dir |> Seq.toList))
    |> Seq.toList

// ============================================================================
// Main Clean Function
// ============================================================================

/// Clean music directories
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    // Phase 1: Validate path
    let validatedPath =
        Progress.runResult "Validating path" (fun () ->
            validatePath path |> Result.liftValidationError)
    
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
            
            // Phase 3: Gather files and find directories without audio
            let dirsWithFiles =
                Progress.run "Scanning for audio files" (fun () ->
                    gatherFilesForDirectories leafDirs)
            
            let itemsToDelete = findDirectoriesWithoutAudio dirsWithFiles
            
            if List.isEmpty itemsToDelete then
                Error (CleaningError (NothingToClean "No directories without audio files"))
            else
                if isExecute then
                    // Log before deleting
                    Logging.logListToFile logFilePath (itemsToDelete |> Seq.map DeletableItem.path)
                    
                    // Phase 4: Execute deletions
                    let deleteResult =
                        Progress.runResult "Deleting directories" (fun () ->
                            let dirs = itemsToDelete |> List.map DeletableItem.path
                            deleteDirectories dirs |> Result.liftCleaningError)
                    
                    match deleteResult with
                    | Ok () -> Ok (itemsToDelete |> List.toSeq)
                    | Error e -> Error e
                else
                    Ok (itemsToDelete |> List.toSeq)