module FileSystem

open System
open System.IO
open Domain
open Size

[<Literal>]
let logFileName = "cleanLog.log"

// ============================================================================
// Core Directory Operations
// ============================================================================

/// Check if directory should be ignored (starts with "." or is "extrafanart")
let private shouldIgnoreDirectory (dirInfo: DirectoryInfo) : bool =
    dirInfo.Name.StartsWith(".") || 
    String.Equals(dirInfo.Name, "extrafanart", StringComparison.OrdinalIgnoreCase)

/// Get all subdirectories, filtering out special directories 
let getSubdirectories (searchOption: SearchOption) (path: ValidatedPath) 
    : Result<seq<ExistingDirectory>, DirectoryError> =
    try
        let pathStr = ValidatedPath.value path
        let directories = 
            DirectoryInfo(pathStr).EnumerateDirectories("*.*", searchOption)
            |> Seq.filter (shouldIgnoreDirectory >> not)
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

/// Calculate total size of files in directory (MB) - top level only
let getDirectorySizeMB (path: string) : int64<MB> =
    getFiles path
    |> Seq.sumBy ExistingFile.sizeInMB

/// Check if a directory is a leaf node (has no non-special subdirectories)
let isLeafNode (path: string) : bool =
    match ValidatedPath.create path with
    | Error _ -> false
    | Ok validPath ->
        match getTopSubdirectories validPath with
        | Ok subdirs -> Seq.isEmpty subdirs  // If we got subdirs after filtering, it's not a leaf
        | Error _ -> true  // No subdirs means it's a leaf

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