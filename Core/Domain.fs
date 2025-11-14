module Domain

open System
open System.IO
open FsToolkit.ErrorHandling

// ============================================================================
// Domain Types - Making illegal states unrepresentable
// ============================================================================

/// A validated directory path that is guaranteed to exist
type ValidatedPath = private ValidatedPath of string

/// A file that exists on disk
type ExistingFile = 
    { FullPath: string
      Name: string
      Extension: string
      SizeInBytes: int64 }

/// A directory that exists on disk
type ExistingDirectory =
    { FullPath: string
      Name: string }

/// Media type classifications
type MediaType =
    | Video
    | Audio
    | Image
    | Subtitle
    | Other

/// Clean mode for the application
type CleanMode =
    | Movies
    | TVShows
    | Music

/// Preview mode - if true, don't delete, just show what would be deleted
type PreviewMode = 
    | Preview
    | Execute

// ============================================================================
// Error Types - Rich, contextual errors
// ============================================================================

type ValidationError =
    | PathEmpty
    | PathNotFound of path: string
    | PathNotDirectory of path: string

type DirectoryError =
    | NoSubdirectories of path: string
    | NoLeafNodes of path: string
    | NoFilesFound of path: string
    | AccessDenied of path: string * exn: Exception

type CleaningError =
    | NothingToClean of reason: string
    | DeletionFailed of path: string * exn: Exception

type DomainError =
    | ValidationError of ValidationError
    | DirectoryError of DirectoryError
    | CleaningError of CleaningError

// ============================================================================
// Smart Constructors - Validate at creation time
// ============================================================================

module ValidatedPath =
    /// Create a validated path, ensuring it exists and is a directory
    let create (path: string) : Result<ValidatedPath, ValidationError> =
        if String.IsNullOrWhiteSpace(path) then
            Error PathEmpty
        elif not (Directory.Exists(path)) then
            if File.Exists(path) then
                Error (PathNotDirectory path)
            else
                Error (PathNotFound path)
        else
            Ok (ValidatedPath path)
    
    /// Get the underlying string value
    let value (ValidatedPath path) = path
    
    /// Combine with a relative path
    let combine (ValidatedPath basePath) relativePath =
        Path.Combine(basePath, relativePath)

module ExistingFile =
    /// Create from FileInfo
    let fromFileInfo (fileInfo: FileInfo) : ExistingFile =
        { FullPath = fileInfo.FullName
          Name = fileInfo.Name
          Extension = fileInfo.Extension.ToLowerInvariant()
          SizeInBytes = fileInfo.Length }
    
    /// Get size in megabytes
    let sizeInMB file =
        float file.SizeInBytes / 1024.0 / 1024.0
    
    /// Get size in kilobytes
    let sizeInKB file =
        float file.SizeInBytes / 1024.0
    
    /// Classify the media type based on extension
    let classifyMediaType file =
        match file.Extension with
        | ".avi" | ".flv" | ".mkv" | ".mp4" | ".mpeg" | ".mpg" | ".wmv" | ".3gp" -> Video
        | ".mp3" | ".m4a" | ".flac" | ".wav" | ".wma" | ".aac" | ".aiff" 
        | ".m4b" | ".m4p" | ".ogg" -> Audio
        | ".jpg" | ".jpeg" | ".png" | ".gif" | ".bmp" | ".tif" | ".tiff" -> Image
        | ".srt" | ".sub" | ".sbv" | ".ass" | ".ssa" | ".vtt" -> Subtitle
        | _ -> Other

module ExistingDirectory =
    /// Create from DirectoryInfo
    let fromDirectoryInfo (dirInfo: DirectoryInfo) : ExistingDirectory =
        { FullPath = dirInfo.FullName
          Name = dirInfo.Name }
    
    /// Check if directory name indicates it should be ignored
    let shouldIgnore dir =
        dir.Name.StartsWith(".")

// ============================================================================
// Error Formatting
// ============================================================================

module DomainError =
    let toMessage error =
        match error with
        | ValidationError ve ->
            match ve with
            | PathEmpty -> "Path cannot be empty"
            | PathNotFound path -> sprintf "Directory not found: %s" path
            | PathNotDirectory path -> sprintf "Path is not a directory: %s" path
        
        | DirectoryError de ->
            match de with
            | NoSubdirectories path -> sprintf "No subdirectories found in: %s" path
            | NoLeafNodes path -> sprintf "No leaf nodes found in: %s" path
            | NoFilesFound path -> sprintf "No files found in: %s" path
            | AccessDenied (path, ex) -> sprintf "Access denied to: %s (%s)" path ex.Message
        
        | CleaningError ce ->
            match ce with
            | NothingToClean reason -> sprintf "Nothing to clean: %s" reason
            | DeletionFailed (path, ex) -> sprintf "Failed to delete: %s (%s)" path ex.Message
    
    /// Convert to an optional message (empty for non-critical errors)
    let toOptionalMessage error =
        match error with
        | DirectoryError (NoSubdirectories _)
        | DirectoryError (NoLeafNodes _)
        | DirectoryError (NoFilesFound _)
        | CleaningError (NothingToClean _) -> 
            None  // These are expected conditions, not errors to report
        | _ -> 
            Some (toMessage error)

// ============================================================================
// Active Patterns for File Classification
// ============================================================================

[<AutoOpen>]
module FilePatterns =
    
    /// Check if file is a video file by size or extension
    let (|VideoFile|_|) (thresholdMB: float) (file: ExistingFile) =
        let isLargeEnough = ExistingFile.sizeInMB file > thresholdMB
        let isVideoExtension = ExistingFile.classifyMediaType file = Video
        if isLargeEnough || isVideoExtension then Some file else None
    
    /// Check if file is an audio file by size or extension
    let (|AudioFile|_|) (thresholdKB: float) (file: ExistingFile) =
        let isLargeEnough = ExistingFile.sizeInKB file > thresholdKB
        let isAudioExtension = ExistingFile.classifyMediaType file = Audio
        if isLargeEnough || isAudioExtension then Some file else None
    
    /// Check if file is a folder image (poster/folder.jpg)
    let (|FolderImage|_|) (file: ExistingFile) =
        let name = file.Name.ToLowerInvariant()
        if name.StartsWith("folder") || name.StartsWith("poster") then
            Some file
        else
            None

// ============================================================================
// Result Helpers
// ============================================================================

module Result =
    /// Convert a domain error to another error type
    let mapError f result =
        Result.mapError f result
    
    /// Lift a validation error to a domain error
    let liftValidationError result =
        result |> Result.mapError ValidationError
    
    /// Lift a directory error to a domain error
    let liftDirectoryError result =
        result |> Result.mapError DirectoryError
    
    /// Lift a cleaning error to a domain error
    let liftCleaningError result =
        result |> Result.mapError CleaningError
    
    /// Perform side effect only on success (from FsToolkit.ErrorHandling)
    /// This is now available as Result.tee from the library
    
    /// Perform side effect only when condition is true
    let teeIf condition f result =
        result |> Result.tee (fun value -> if condition then f value)
    
    /// Try to perform an operation, catching exceptions and converting to Result
    let ofExn exnMapper f =
        try
            Ok (f())
        with
        | ex -> Error (exnMapper ex)