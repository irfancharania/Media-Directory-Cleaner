module TVShows

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size

let ThresholdSizeMB = 100L<MB>

/// Partition files into main files (videos) and extra files
/// Using the Seq.partition from Utility.fs (which handles sequences properly)
let private partitionFiles (files: seq<ExistingFile>) =
    files
    |> Seq.toList  // Convert to list to avoid multiple enumeration issues
    |> List.partition (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false)

/// Remove common suffixes from filenames for matching
let private normalizeFileName (fileName: string) =
    let removeSuffix (suffix: string) (s: string) =
        if s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) then
            s.Substring(0, s.Length - suffix.Length)
        else
            s
    
    let removeRippingGroup (s: string) =
        Regex.Replace(s, @"\s\([\w\.\-\s\,]+\)?$", String.Empty)
    
    fileName
    |> Path.GetFileNameWithoutExtension
    |> removeSuffix ".en"
    |> removeSuffix ".eng"
    |> removeSuffix ".english"
    |> removeSuffix "-thumb"
    |> removeRippingGroup

/// Find orphaned extra files (no corresponding main video file)
let private findOrphanedFiles (mainFiles: seq<ExistingFile>) (extraFiles: seq<ExistingFile>) 
    : seq<string> =
    
    // If no main files, all extra files are orphans
    if Seq.isEmpty mainFiles then
        extraFiles |> Seq.map (fun f -> f.FullPath)
    else
        // Get normalized base names of all video files
        let mainFileBaseNames =
            mainFiles
            |> Seq.map (fun f -> normalizeFileName f.Name)
            |> Set.ofSeq
        
        extraFiles
        |> Seq.filter (fun extraFile ->
            // Skip folder images - we want to keep these
            match extraFile with
            | FolderImage _ -> false
            | _ ->
                // Check if the extra file's name contains any video file's base name
                let extraFileName = extraFile.Name
                mainFileBaseNames
                |> Set.exists (fun videoBaseName -> extraFileName.Contains(videoBaseName))
                |> not)  // If no match found, it's an orphan
        |> Seq.map (fun f -> f.FullPath)

/// Check if directory has video files
let private hasVideoFiles (path: string) : bool =
    getFiles path
    |> Seq.exists (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false)

/// Get orphaned files from all leaf directories
let private getOrphanedItems (directories: seq<string>) 
    : Result<seq<string>, CleaningError> =
    
    let mutable allOrphans = []
    let mutable emptyDirs = []
    
    for dir in directories do
        let files = getFiles dir
        let mainFiles, extraFiles = partitionFiles files
        
        // If directory has no video files, mark entire directory for deletion
        if Seq.isEmpty mainFiles then
            emptyDirs <- dir :: emptyDirs
        else
            // Otherwise, just collect orphaned files
            let orphanedFiles = findOrphanedFiles mainFiles extraFiles
            allOrphans <- List.append allOrphans (orphanedFiles |> Seq.toList)
    
    let allItemsToDelete = List.append allOrphans emptyDirs
    
    if List.isEmpty allItemsToDelete then
        Error (NothingToClean "No orphaned files or empty directories found")
    else
        Ok allItemsToDelete

/// Clean TV show directories - delete orphaned files and empty directories
/// Runs iteratively: orphan deletion → empty folder deletion on next run
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<string>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    ValidatedPath.create path 
    |> Result.liftValidationError
    |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
    |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    |> Result.bind (getOrphanedItems >> Result.liftCleaningError)
    |> Result.teeIf isExecute (Logging.logListToFile logFilePath)
    |> Result.bind (fun toDelete ->
        if isExecute then
            // Separate files from directories
            let files = toDelete |> Seq.filter File.Exists
            let dirs = toDelete |> Seq.filter Directory.Exists
            
            // Delete files first
            let fileResult = 
                if Seq.isEmpty files then Ok ()
                else deleteFiles files |> Result.liftCleaningError
            
            // Then delete directories
            let dirResult = 
                if Seq.isEmpty dirs then Ok ()
                else deleteDirectories dirs |> Result.liftCleaningError
            
            // Combine results
            match fileResult, dirResult with
            | Ok _, Ok _ -> Ok toDelete
            | Error e, _ -> Error e
            | _, Error e -> Error e
        else
            Ok toDelete)