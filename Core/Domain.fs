module Domain

open System.IO
open Size
open Errors

// ============================================================================
// Domain Types - Making illegal states unrepresentable
// ============================================================================

/// Items that can be deleted - we track what they are from the start
type DeletableItem =
    | File of path: string
    | Directory of path: string
    
module DeletableItem =
    let path item =
        match item with
        | File path -> path
        | Directory path -> path
    
    let fromFile path = File path
    let fromDirectory path = Directory path

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

/// A directory with its associated files - shared across TV, Movies, Music
/// Example: { Path = "Z:\Movies\Title (2010)"; Files = [file.mkv; poster.jpg; file.srt] }
type DirectoryWithFiles = {
    Path: string
    Files: ExistingFile list
}

/// Media type classifications
type MediaType =
    | Video
    | Audio
    | Subtitle
    | Other

/// Clean mode for the application
type CleanMode =
    | Tv
    | Movies
    | Music

/// Preview mode - whether to actually delete or just show what would be deleted
type PreviewMode = 
    | Preview
    | Execute

/// Scan mode - whether to use optimization or scan all directories
type ScanMode =
    | Optimized
    | ScanAll

// ============================================================================
// Smart Constructors
// ============================================================================

module ValidatedPath =
    /// Internal constructor - only for use by infrastructure layer
    let internal createUnchecked (path: string) : ValidatedPath =
        ValidatedPath path
    
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
        | ".srt" | ".sub" | ".sbv" | ".ass" | ".ssa" | ".vtt" -> MediaType.Subtitle
        | _ -> Other

module ExistingDirectory =
    /// Create from DirectoryInfo
    let fromDirectoryInfo (dirInfo: DirectoryInfo) : ExistingDirectory =
        { FullPath = dirInfo.FullName
          Name = dirInfo.Name }

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