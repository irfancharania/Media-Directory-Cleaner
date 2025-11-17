module Domain

open System
open System.IO
open FsToolkit.ErrorHandling
open Size

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
      SizeInBytes: int64<byte> }

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
          SizeInBytes = Size.int64ToBytes fileInfo.Length }
    
    /// Get size in megabytes
    let sizeInMB (file: ExistingFile) : int64<MB> =
        file.SizeInBytes |> Size.bytesToMegaBytes
    
    /// Get size in kilobytes
    let sizeInKB (file: ExistingFile) : int64<kB> =
        file.SizeInBytes |> Size.bytesToKiloBytes
    
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

// ============================================================================
// Subtitle Language Detection
// ============================================================================

module Subtitle =
    
    /// ISO 639-2/3 language codes that should be DELETED (not kept)
    /// Updated to keep English and French (including Canadian French)
    /// Source: https://www.opensubtitles.org/
    let private languagesToDelete = [
        // European languages (excluding French)
        "ara"; "baq"; "cat"; "cze"; "dan"; "dut"; "fin"; "ger"; "glg"
        "gre"; "hun"; "ita"; "nor"; "pol"; "por"; "rum"; "spa"; "swe"; "tur"
        // Asian languages
        "chi"; "jpn"; "kor"; "tha"; "vie"; "hin"; "kan"; "mal"; "tam"; "tel"
        // Other languages
        "rus"; "ukr"; "heb"; "per"; "bul"; "hrv"; "slv"; "srp"; "est"; "lav"
        "lit"; "ice"; "alb"; "arm"; "geo"; "mac"; "slo"; "bos"; "may"; "ind"
    ]
    
    /// Language indicators to KEEP (English and French variants)
    let private languagesToKeep = [
        // English variants
        "english"; "eng"; "en"
        // Hearing impaired subtitles (always keep regardless of language)
        "sdh"; "hi"; "cc"  // SDH = Subtitles for Deaf/Hard of hearing, HI = Hearing Impaired, CC = Closed Captions
        // French variants (standard ISO codes first for better matching)
        "fra"; "fre"; "fr"; "french"; "francais"; "français"
        // Canadian French variants
        "fr-ca"; "frc"; "frca"; "french-canadian"; "canadien"; "quebec"; "québec"
    ]
    
    /// Check if filename contains a language code from the given list
    /// Matches patterns: .code., _code_, .code.srt, _code.srt, code.srt (at start)
    let private containsLanguageCode (codes: string list) (filename: string) =
        let lower = filename.ToLowerInvariant()
        codes
        |> List.exists (fun code -> 
            lower.Contains($".{code}.") || 
            lower.Contains($"_{code}_") || 
            lower.Contains($".{code}_") || 
            lower.Contains($"_{code}.") || 
            lower.Contains($"-{code}.") ||
            lower.Contains($"-{code}_") ||
            lower.EndsWith($".{code}.srt") ||
            lower.EndsWith($"_{code}.srt") ||
            lower.EndsWith($".{code}.sub") ||
            lower.EndsWith($"_{code}.sub") ||
            lower.StartsWith($"{code}.") ||
            lower.StartsWith($"{code}_") ||
            lower = $"{code}.srt" ||
            lower = $"{code}.sub")
    
    /// Determine if a subtitle file should be deleted
    /// Returns true if we're confident it should be DELETED (not English/French)
    /// Returns false if it's English/French OR we're uncertain (err on the side of caution)
    let shouldDelete (filename: string) : bool =
        let hasLanguageToKeep = containsLanguageCode languagesToKeep filename
        let hasLanguageToDelete = containsLanguageCode languagesToDelete filename
        
        match hasLanguageToKeep, hasLanguageToDelete with
        | true, _ -> false      // Explicitly English/French - keep it
        | false, true -> true   // Has other language code - delete it
        | false, false -> false // Uncertain - keep it (safe default)
    
    /// Check if file is a subtitle by extension
    let isSubtitleFile (file: ExistingFile) : bool =
        match file.Extension with
        | ".srt" | ".sub" | ".sbv" | ".ass" | ".ssa" | ".vtt" -> true
        | _ -> false

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
    let (|VideoFile|_|) (thresholdMB: int64<MB>) (file: ExistingFile) =
        let isLargeEnough = ExistingFile.sizeInMB file > thresholdMB
        let isVideoExtension = ExistingFile.classifyMediaType file = Video
        if isLargeEnough || isVideoExtension then Some file else None
    
    /// Check if file is an audio file by size or extension
    let (|AudioFile|_|) (thresholdKB: int64<kB>) (file: ExistingFile) =
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