module TVShows

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Utility
open Size

let ThresholdSizeMB = 100L<MB>

/// Check if directory should be skipped (optimization)
/// Dot-prefixed directories' fate is determined by parent folder
let private shouldSkipDirectory (dirInfo: DirectoryInfo) : bool =
    dirInfo.Name.StartsWith(".")

/// Partition files into main files (videos) and extra files
let private partitionFiles (files: seq<ExistingFile>) =
    Seq.partition (fun file ->
        match file with
        | VideoFile ThresholdSizeMB _ -> true
        | _ -> false) files

/// Remove common suffixes from filenames for matching
let private normalizeFileName (fileName: string) =
    let removeSuffix (suffix: string) (s: string) =
        if s.EndsWith(suffix) then
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
        let mainFileNames =
            mainFiles
            |> Seq.map (fun f -> normalizeFileName f.Name)
            |> Set.ofSeq
        
        extraFiles
        |> Seq.filter (fun extraFile ->
            // Skip folder images - we want to keep these
            match extraFile with
            | FolderImage _ -> false
            | _ ->
                let normalizedName = normalizeFileName extraFile.Name
                mainFileNames
                |> Set.exists (fun mainName -> mainName.Contains(normalizedName))
                |> not)
        |> Seq.map (fun f -> f.FullPath)

/// Get orphaned files from a single directory (season folder)
/// Also returns whether the directory has any video files
let private processDirectory (dir: string) : bool * seq<string> =
    // Get files, but skip .actors and other dot-prefixed subdirectories
    let currentFiles = getFiles dir
    
    let subDirFiles =
        try
            DirectoryInfo(dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            |> Seq.filter (shouldSkipDirectory >> not)
            |> Seq.collect (fun subDir -> getFiles subDir.FullName)
        with
        | _ -> Seq.empty
    
    let allFiles = Seq.append currentFiles subDirFiles
    let mainFiles, extraFiles = partitionFiles allFiles
    
    let hasVideoFiles = not (Seq.isEmpty mainFiles)
    let orphanedFiles = findOrphanedFiles mainFiles extraFiles
    
    (hasVideoFiles, orphanedFiles)

/// Get orphaned files from all subdirectories
/// Returns both orphaned files and empty directories to delete
let private getOrphanedItems (directories: seq<string>) 
    : Result<seq<string>, CleaningError> =
    
    let mutable allOrphans = []
    let mutable emptyDirs = []
    
    for dir in directories do
        let hasVideoFiles, orphanedFiles = processDirectory dir
        
        // If directory has no video files, mark entire directory for deletion
        if not hasVideoFiles then
            emptyDirs <- dir :: emptyDirs
        else
            // Otherwise, just collect orphaned files
            allOrphans <- List.append allOrphans (orphanedFiles |> Seq.toList)
    
    let allItemsToDelete = List.append allOrphans emptyDirs
    
    if List.isEmpty allItemsToDelete then
        Error (NothingToClean "No orphaned files or empty directories found")
    else
        Ok allItemsToDelete

/// Clean TV show directories
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