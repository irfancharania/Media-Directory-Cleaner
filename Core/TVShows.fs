module TVShows

open System
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open Domain
open FileSystem
open Size
open Utility

let ThresholdSizeMB = 100L<MB>

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
    : seq<DeletableItem> =
    
    // If no main files, all extra files are orphans
    if Seq.isEmpty mainFiles then
        extraFiles |> Seq.map (fun f -> DeletableItem.fromFile f.FullPath)
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
        |> Seq.map (fun f -> DeletableItem.fromFile f.FullPath)

/// Classification result for a directory
type DirectoryClassification =
    | HasVideos of orphanedFiles: DeletableItem list
    | NoVideos of directoryPath: string

/// Get orphaned files from a single directory (season folder)
/// Returns classification indicating whether directory has video files
let private processDirectory (dir: string) : DirectoryClassification =
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
    
    if Seq.isEmpty mainFiles then
        NoVideos dir
    else
        let orphanedFiles = findOrphanedFiles mainFiles extraFiles |> Seq.toList
        HasVideos orphanedFiles

/// Get orphaned files from all subdirectories
/// Returns both orphaned files and empty directories to delete
let private getOrphanedItems (directories: seq<string>) 
    : Result<seq<DeletableItem>, CleaningError> =
    
    let processedDirs =
        directories
        |> Seq.map processDirectory
        |> Seq.toList
    
    let allOrphans = 
        processedDirs 
        |> List.choose (function 
            | HasVideos files -> Some files 
            | _ -> None)
        |> List.concat
    
    let emptyDirs = 
        processedDirs 
        |> List.choose (function 
            | NoVideos dir -> Some (DeletableItem.fromDirectory dir)
            | _ -> None)
    
    let allItemsToDelete = 
        List.append allOrphans emptyDirs
        |> List.sortBy DeletableItem.path  // Sort alphabetically so related items appear together
    
    if List.isEmpty allItemsToDelete then
        Error (NothingToClean "No orphaned files or empty directories found")
    else
        Ok allItemsToDelete

/// Clean TV show directories
let clean (path: string) (previewMode: PreviewMode) 
    : Result<seq<DeletableItem>, DomainError> =
    
    let logFilePath = Path.Combine(path, logFileName)
    let isExecute = (previewMode = Execute)
    
    PathValidation.validate path 
    |> Result.liftValidationError
    |> Result.bind (getAllSubdirectories >> Result.liftDirectoryError)
    |> Result.bind (filterToLeafNodes >> Result.liftDirectoryError)
    |> Result.bind (getOrphanedItems >> Result.liftCleaningError)
    |> Result.teeIf isExecute (fun items -> 
        Logging.logListToFile logFilePath (items |> Seq.map DeletableItem.path))
    |> Result.bind (fun toDelete ->
        if isExecute then
            // Separate files from directories
            let files = 
                toDelete 
                |> Seq.choose (function DeletableItem.File path -> Some path | _ -> None)
            let dirs = 
                toDelete 
                |> Seq.choose (function DeletableItem.Directory path -> Some path | _ -> None)
            
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