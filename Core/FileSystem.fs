module FileSystem

open System
open System.IO
open FsToolkit.ErrorHandling
open Domain
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