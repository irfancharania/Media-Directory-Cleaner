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
    /// Includes common variants and alternative codes
    /// Source: https://www.opensubtitles.org/
    let private languagesToDelete = [
        // Arabic
        "ara"; "arb"; "ar"; "arabic" 
        // Asian languages
        "chi"; "zho"; "zh"; "cmn"; "yue"; "chinese"  // Chinese (Mandarin, Cantonese)
        "jpn"; "ja"; "jp"; "japanese" 
        "kor"; "ko"; "kr"; "korean" 
        "tha"; "th"; "thai"
        "vie"; "vi"; "vietnamese"
        "hin"; "hi"; "hindi"
        "kan"; "kn"; "kannada"
        "mal"; "ml"; "malayalam"
        "tam"; "ta"; "tamil"
        "tel"; "te"; "telugu"
        "ben"; "bn"; "bengali"
        "mar"; "marathi"
        "pan"; "pa"; "punjabi"
        // European languages (excluding English and French)
        "spa"; "es"; "esp"; "spanish"
        "por"; "pt"; "pt-br"; "portuguese"
        "ger"; "deu"; "de"; "german"
        "ita"; "italian"
        "dut"; "nld"; "nl"; "dutch"
        "pol"; "pl"; "polish"
        "rus"; "ru"; "russian"
        "ukr"; "ukrainian"
        "cze"; "ces"; "cs"; "czech"
        "swe"; "sv"; "swedish"
        "dan"; "da"; "danish"
        "nor"; "no"; "nob"; "nno"; "norwegian"
        "fin"; "fi" ; "finnish"
        "gre"; "ell"; "el"; "greek"
        "tur"; "tr"; "turkish"
        "hun"; "hu"; "hungarian"
        "rum"; "ron"; "ro"; "romanian"
        "bul"; "bg"; "bulgarian"
        "hrv"; "hr"; "croatian"
        "srp"; "sr"; "serbian"
        "slv"; "sl"; "slovenian"
        "slo"; "slk"; "sk"; "slovak"
        "bos"; "bs"; "bosnian"
        "mac"; "mkd"; "mk"; "macedonian"
        "alb"; "sqi"; "sq"; "albanian"
        "est"; "et"; "estonian"
        "lav"; "lv"; "latvian"
        "lit"; "lt"; "lithuanian"
        "ice"; "isl"; "is"; "icelandic"
        // Other European
        "baq"; "eus"; "eu"; "basque"
        "cat"; "ca"; "catalan"
        "glg"; "gl"; "galician"
        "arm"; "hye"; "hy"; "armenian"
        "geo"; "kat"; "ka"; "georgian"
        // Middle Eastern
        "heb"; "hebrew"
        "per"; "fas"; "fa"; "persian"; "farsi"
        // Southeast Asian
        "may"; "msa"; "ms"; "malay"
        "ind"; "id"; "indonesian"
        "fil"; "tl"; "filipino"; "tagalog"
        // Other
        "swa"; "sw"; "swahili"
    ]
    
    /// Language indicators to KEEP (English and French variants)
    let private languagesToKeep = [
        // English variants
        "english"; "eng"; "en"
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
    
    /// Check if a subtitle file's language is uncertain (no recognizable language code)
    /// Used for reporting in preview mode
    let isUncertain (filename: string) : bool =
        let hasLanguageToKeep = containsLanguageCode languagesToKeep filename
        let hasLanguageToDelete = containsLanguageCode languagesToDelete filename
        
        match hasLanguageToKeep, hasLanguageToDelete with
        | false, false -> true  // No language code detected - uncertain
        | _ -> false            // Language detected (either keep or delete)
    
    /// Check if file is a subtitle by extension
    let isSubtitleFile (file: ExistingFile) : bool =
        match file.Extension with
        | ".srt" | ".sub" | ".sbv" | ".ass" | ".ssa" | ".vtt" -> true
        | _ -> false
    
    /// Check if subtitle filename matches a video file in the same directory
    /// If it does, we should keep it (guaranteed to be wanted)
    let matchesVideoFile (subtitlePath: string) (dirFiles: seq<ExistingFile>) : bool =
        let subtitleBase = Path.GetFileNameWithoutExtension(subtitlePath).ToLowerInvariant()
        
        dirFiles
        |> Seq.filter (fun f -> ExistingFile.classifyMediaType f = Video)
        |> Seq.exists (fun videoFile ->
            let videoBase = Path.GetFileNameWithoutExtension(videoFile.Name).ToLowerInvariant()
            subtitleBase = videoBase)

// ============================================================================
// Error Formatting
// ============================================================================

module DomainError =
    let toMessage error =
        match error with
        | ValidationError ve ->
            match ve with
            | PathEmpty -> "Path cannot be empty"
            | PathNotFound path -> $"Directory not found: {path}"
            | PathNotDirectory path -> $"Path is not a directory: {path}"
        
        | DirectoryError de ->
            match de with
            | NoSubdirectories path -> $"No subdirectories found in: {path}"
            | NoLeafNodes path -> $"No leaf nodes found in: {path}"
            | NoFilesFound path -> $"No files found in: {path}"
            | AccessDenied (path, ex) -> $"Access denied to: {path} ({ex.Message})"
        
        | CleaningError ce ->
            match ce with
            | NothingToClean reason -> $"Nothing to clean: {reason}"
            | DeletionFailed (path, ex) -> $"Failed to delete: {path} ({ex.Message})"
    
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
    /// Lift a validation error to a domain error
    let liftValidationError result =
        result |> Result.mapError ValidationError
    
    /// Lift a directory error to a domain error
    let liftDirectoryError result =
        result |> Result.mapError DirectoryError
    
    /// Lift a cleaning error to a domain error
    let liftCleaningError result =
        result |> Result.mapError CleaningError
    
    /// Perform side effect only when condition is true
    let teeIf condition f result =
        result |> Result.tee (fun value -> if condition then f value)
    
    /// Try to perform an operation, catching exceptions and converting to Result
    let ofExn exnMapper f =
        try
            Ok (f())
        with
        | ex -> Error (exnMapper ex)