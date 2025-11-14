module Directory

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open Utility
open Size

[<Literal>]
let logFileName = "cleanLog.log"

// ============================================================================
// Core Directory Operations
// ============================================================================

/// Get all subdirectories, filtering out special directories (starting with .)
let getSubdirectories (searchOption: SearchOption) (path: ValidatedPath) 
    : Result<seq<ExistingDirectory>, DirectoryError> =
    try
        let pathStr = ValidatedPath.value path
        let directories = 
            DirectoryInfo(pathStr).EnumerateDirectories("*.*", searchOption)
            |> Seq.filter (fun di -> not (di.Name.StartsWith(".")))
            |> Seq.map ExistingDirectory.fromDirectoryInfo
        
        if Seq.isEmpty directories then
            Error (NoSubdirectories pathStr)
        else
            Ok directories
    with
    | :? UnauthorizedAccessException as ex ->
        Error (AccessDenied (ValidatedPath.value path, ex))

let getAllSubdirectories = getSubdirectories SearchOption.AllDirectories
let getTopSubdirectories = getSubdirectories SearchOption.TopDirectoryOnly

/// Get all files in a directory (non-recursive)
let getFiles (path: string) : seq<ExistingFile> =
    DirectoryInfo(path).EnumerateFiles("*", SearchOption.TopDirectoryOnly)
    |> Seq.map ExistingFile.fromFileInfo

/// Calculate total size of files in directory (MB)
let getDirectorySizeMB (path: string) : int64<MB> =
    getFiles path
    |> Seq.sumBy ExistingFile.sizeInMB

/// Check if a directory is a leaf node (has no subdirectories)
let isLeafNode (path: string) : bool =
    match ValidatedPath.create path with
    | Error _ -> false
    | Ok validPath ->
        match getTopSubdirectories validPath with
        | Ok _ -> false
        | Error _ -> true

/// Filter directories to only leaf nodes
let filterToLeafNodes (directories: seq<ExistingDirectory>) 
    : Result<seq<string>, DirectoryError> =
    let leafPaths =
        directories
        |> Seq.map (fun d -> d.FullPath)
        |> Seq.filter isLeafNode
        |> Seq.toList
    
    if List.isEmpty leafPaths then
        Error (NoLeafNodes "No leaf node directories found")
    else
        Ok leafPaths

// ============================================================================
// Deletion Operations
// ============================================================================

/// Delete a list of directories
let deleteDirectories (paths: seq<string>) : Result<unit, CleaningError> =
    Result.ofExn 
        (fun ex -> DeletionFailed ("multiple directories", ex))
        (fun () -> paths |> Seq.iter (fun path -> Directory.Delete(path, true)))

/// Delete a list of files
let deleteFiles (paths: seq<string>) : Result<unit, CleaningError> =
    Result.ofExn 
        (fun ex -> DeletionFailed ("multiple files", ex))
        (fun () -> paths |> Seq.iter File.Delete)

// ============================================================================
// Movies Module
// ============================================================================

module Movies =
    let ThresholdSizeMB = 100L<MB>
    
    /// Filter directories that are below the size threshold
    let filterBySize (directories: seq<string>) : Result<seq<string>, CleaningError> =
        let smallDirs =
            directories
            |> Seq.filter (fun path -> getDirectorySizeMB path < ThresholdSizeMB)
            |> Seq.toList
        
        if List.isEmpty smallDirs then
            Error (NothingToClean "No directories below size threshold")
        else
            Ok smallDirs
    
    /// Clean movie directories
    let clean (path: string) (previewMode: PreviewMode) 
        : Result<seq<string>, DomainError> =
        
        let logFilePath = Path.Combine(path, logFileName)
        let isExecute = (previewMode = Execute)
        
        ValidatedPath.create path 
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
        |> Result.bind (filterBySize >> Result.liftCleaningError)
        |> Result.teeIf isExecute (Logging.logListToFile logFilePath)
        |> Result.bind (fun toDelete ->
            if isExecute then
                deleteDirectories toDelete 
                |> Result.liftCleaningError
                |> Result.map (fun () -> toDelete)
            else
                Ok toDelete)

// ============================================================================
// TV Shows Module
// ============================================================================

module TV =
    let ThresholdSizeMB = 100L<MB>
    
    /// Partition files into main files (videos) and extra files
    let partitionFiles (files: seq<ExistingFile>) =
        Seq.partition (fun file ->
            match file with
            | VideoFile ThresholdSizeMB _ -> true
            | _ -> false) files
    
    /// Remove common suffixes from filenames for matching
    let normalizeFileName (fileName: string) =
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
    let findOrphanedFiles (mainFiles: seq<ExistingFile>) (extraFiles: seq<ExistingFile>) 
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
    
    /// Get orphaned files from all subdirectories
    let getOrphanedFilesFromDirs (directories: seq<string>) 
        : Result<seq<string>, CleaningError> =
        
        let allOrphans =
            directories
            |> Seq.collect (fun dir ->
                let files = getFiles dir
                let mainFiles, extraFiles = partitionFiles files
                findOrphanedFiles mainFiles extraFiles)
            |> Seq.toList
        
        if List.isEmpty allOrphans then
            Error (NothingToClean "No orphaned files found")
        else
            Ok allOrphans
    
    /// Clean TV show directories
    let clean (path: string) (previewMode: PreviewMode) 
        : Result<seq<string>, DomainError> =
        
        let logFilePath = Path.Combine(path, logFileName)
        let isExecute = (previewMode = Execute)
        
        ValidatedPath.create path 
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
        |> Result.bind (getOrphanedFilesFromDirs >> Result.liftCleaningError)
        |> Result.teeIf isExecute (Logging.logListToFile logFilePath)
        |> Result.bind (fun toDelete ->
            if isExecute then
                deleteFiles toDelete 
                |> Result.liftCleaningError
                |> Result.map (fun () -> toDelete)
            else
                Ok toDelete)

// ============================================================================
// Music Module
// ============================================================================

module Music =
    let ThresholdSizeKB = 500L<kB>
    
    /// Check if directory has any main audio files
    let hasAudioFiles (path: string) : bool =
        let files = getFiles path
        files
        |> Seq.exists (fun file ->
            match file with
            | AudioFile ThresholdSizeKB _ -> true
            | _ -> false)
    
    /// Filter directories that have no audio files
    let filterDirectoriesWithoutAudio (directories: seq<string>) 
        : Result<seq<string>, CleaningError> =
        
        let orphanedDirs =
            directories
            |> Seq.filter (hasAudioFiles >> not)
            |> Seq.toList
        
        if List.isEmpty orphanedDirs then
            Error (NothingToClean "No directories without audio files")
        else
            Ok orphanedDirs
    
    /// Clean music directories
    let clean (path: string) (previewMode: PreviewMode) 
        : Result<seq<string>, DomainError> =
        
        let logFilePath = Path.Combine(path, logFileName)
        let isExecute = (previewMode = Execute)
        
        ValidatedPath.create path 
        |> Result.liftValidationError
        |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
        |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
        |> Result.bind (filterDirectoriesWithoutAudio >> Result.liftCleaningError)
        |> Result.teeIf isExecute (Logging.logListToFile logFilePath)
        |> Result.bind (fun toDelete ->
            if isExecute then
                deleteDirectories toDelete 
                |> Result.liftCleaningError
                |> Result.map (fun () -> toDelete)
            else
                Ok toDelete)