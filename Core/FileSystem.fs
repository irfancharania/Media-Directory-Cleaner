module FileSystem

open System
open System.IO
open Domain
open Errors
open Size

[<Literal>]
let logFileName = "cleanLog.log"

// ============================================================================
// Path Validation - Infrastructure Layer (I/O)
// ============================================================================

/// Validate a path string by checking the file system
let validatePath (path: string) : Result<ValidatedPath, ValidationError> =
    if String.IsNullOrWhiteSpace(path) then
        Error PathEmpty
    else
        let normalizedPath = Path.GetFullPath(path)
        
        if not (Directory.Exists(normalizedPath)) then
            if File.Exists(normalizedPath) then
                Error (PathNotDirectory path)
            else
                Error (PathNotFound path)
        else
            Ok (ValidatedPath.createUnchecked normalizedPath)

// ============================================================================
// Directory Filtering
// ============================================================================

/// Check if a directory is a special directory that should be excluded from operations
let private isSpecialDirectory (dirName: string) : bool =
    dirName.StartsWith(".") || 
    String.Equals(dirName, "extrafanart", StringComparison.OrdinalIgnoreCase)

/// Check if a directory should be skipped when recursing to find files
let shouldSkipDirectory (dirInfo: DirectoryInfo) : bool =
    isSpecialDirectory dirInfo.Name

// ============================================================================
// Core Directory Operations
// ============================================================================

/// Get all subdirectories, filtering out special directories 
let getSubdirectories (searchOption: SearchOption) (path: ValidatedPath) 
    : Result<seq<ExistingDirectory>, DirectoryError> =
    try
        let pathStr = ValidatedPath.value path
        let directories = 
            DirectoryInfo(pathStr).EnumerateDirectories("*.*", searchOption)
            |> Seq.filter (fun di -> not (isSpecialDirectory di.Name))
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
    match validatePath path with
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
        Ok (Seq.ofList leafPaths)

// ============================================================================
// Deletion Operations
// ============================================================================

/// Delete a single directory
let private deleteDirectory (path: string) : Result<unit, CleaningError> =
    try
        Directory.Delete(path, true)
        Ok ()
    with
    | ex -> Error (DeletionFailed (path, ex))

/// Delete a single file
let private deleteFile (path: string) : Result<unit, CleaningError> =
    try
        File.Delete(path)
        Ok ()
    with
    | ex -> Error (DeletionFailed (path, ex))

/// Delete multiple directories, stopping on first error
let deleteDirectories (paths: seq<string>) : Result<unit, CleaningError> =
    paths
    |> Seq.map deleteDirectory
    |> Seq.tryFind Result.isError
    |> Option.defaultValue (Ok ())

/// Delete multiple files, stopping on first error
let deleteFiles (paths: seq<string>) : Result<unit, CleaningError> =
    paths
    |> Seq.map deleteFile
    |> Seq.tryFind Result.isError
    |> Option.defaultValue (Ok ())