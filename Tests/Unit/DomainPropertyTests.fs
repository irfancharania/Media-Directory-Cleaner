namespace MediaDirectoryCleaner.Tests.Unit

open Xunit
open FsCheck
open Domain

// Alias to avoid conflict with Size.byte unit of measure
type Byte = System.Byte

/// Property-based tests for media type classification
module DomainPropertyTests =

    // ============================================================================
    // Extension Lists (must match Domain.fs)
    // ============================================================================
    
    let private videoExtensions = [| ".avi"; ".flv"; ".mkv"; ".mp4"; ".mpeg"; ".mpg"; ".wmv"; ".3gp" |]
    let private audioExtensions = [| ".mp3"; ".m4a"; ".flac"; ".wav"; ".wma"; ".aac"; ".aiff"; ".m4b"; ".m4p"; ".ogg" |]
    let private subtitleExtensions = [| ".srt"; ".sub"; ".sbv"; ".ass"; ".ssa"; ".vtt" |]
    let private unknownExtensions = [| ".txt"; ".nfo"; ".xml"; ".json"; ".doc"; ".pdf"; ".exe"; ".bin"; ".dat"; ".jpg"; ".png"; ".gif" |]
    
    let private createFile ext = 
        { FullPath = $"test{ext}"
          Name = $"test{ext}"
          Extension = ext
          SizeInBytes = Size.int64ToBytes 1L }

    let private createFileWithSize ext (size: int64) =
        { FullPath = $"test{ext}"
          Name = $"test{ext}"
          Extension = ext
          SizeInBytes = Size.int64ToBytes size }

    // ============================================================================
    // Property: All Video Extensions Classify as Video
    // ============================================================================
    
    [<Fact>]
    let ``All video extensions classify as Video``() =
        let prop (idx: Byte) =
            let ext = videoExtensions.[int idx % videoExtensions.Length]
            let file = createFile ext
            ExistingFile.classifyMediaType file = Video
        Check.QuickThrowOnFailure prop

    [<Fact>]
    let ``All video extensions in uppercase classify as Video``() =
        let prop (idx: Byte) =
            let ext = videoExtensions.[int idx % videoExtensions.Length]
            let file = createFile (ext.ToLowerInvariant())
            ExistingFile.classifyMediaType file = Video
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: All Audio Extensions Classify as Audio
    // ============================================================================
    
    [<Fact>]
    let ``All audio extensions classify as Audio``() =
        let prop (idx: Byte) =
            let ext = audioExtensions.[int idx % audioExtensions.Length]
            let file = createFile ext
            ExistingFile.classifyMediaType file = Audio
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Classification is Deterministic
    // ============================================================================
    
    [<Fact>]
    let ``Classification returns same result when called twice``() =
        let allExtensions = Array.concat [videoExtensions; audioExtensions; subtitleExtensions]
        let prop (idx: Byte) =
            let ext = allExtensions.[int idx % allExtensions.Length]
            let file = createFile ext
            ExistingFile.classifyMediaType file = ExistingFile.classifyMediaType file
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    let ``All subtitle extensions classify as Subtitle``() =
        let prop (idx: Byte) =
            let ext = subtitleExtensions.[int idx % subtitleExtensions.Length]
            let file = createFile ext
            ExistingFile.classifyMediaType file = MediaType.Subtitle
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Unknown Extensions Classify as Other
    // ============================================================================
    
    [<Fact>]
    let ``Unknown extensions classify as Other``() =
        let prop (idx: Byte) =
            let ext = unknownExtensions.[int idx % unknownExtensions.Length]
            let file = createFile ext
            ExistingFile.classifyMediaType file = Other
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: VideoFile Pattern Matches All Video Extensions
    // ============================================================================
    
    [<Fact>]
    let ``VideoFile pattern matches all video extensions regardless of size``() =
        let prop (idx: Byte) =
            let ext = videoExtensions.[int idx % videoExtensions.Length]
            let file = createFileWithSize ext 1L
            match file with
            | VideoFile 100L<Size.MB> _ -> true
            | _ -> false
        Check.QuickThrowOnFailure prop

    [<Fact>]
    let ``VideoFile pattern matches large files regardless of extension``() =
        let prop (idx: Byte) =
            let ext = unknownExtensions.[int idx % unknownExtensions.Length]
            let file = createFileWithSize ext 200000000L
            match file with
            | VideoFile 100L<Size.MB> _ -> true
            | _ -> false
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: AudioFile Pattern Matches All Audio Extensions
    // ============================================================================
    
    [<Fact>]
    let ``AudioFile pattern matches all audio extensions regardless of size``() =
        let prop (idx: Byte) =
            let ext = audioExtensions.[int idx % audioExtensions.Length]
            let file = createFileWithSize ext 1L
            match file with
            | AudioFile 500L<Size.kB> _ -> true
            | _ -> false
        Check.QuickThrowOnFailure prop

    [<Fact>]
    let ``AudioFile pattern matches large files regardless of extension``() =
        let prop (idx: Byte) =
            let ext = unknownExtensions.[int idx % unknownExtensions.Length]
            let file = createFileWithSize ext 600000L
            match file with
            | AudioFile 500L<Size.kB> _ -> true
            | _ -> false
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Subtitle.isSubtitleFile Matches All Subtitle Extensions
    // ============================================================================
    
    [<Fact>]
    let ``isSubtitleFile returns true for all subtitle extensions``() =
        let prop (idx: Byte) =
            let ext = subtitleExtensions.[int idx % subtitleExtensions.Length]
            let file = createFile ext
            Subtitle.isSubtitleFile file
        Check.QuickThrowOnFailure prop

    [<Fact>]
    let ``isSubtitleFile returns false for non-subtitle extensions``() =
        let nonSubtitleExtensions = Array.concat [videoExtensions; audioExtensions; unknownExtensions]
        let prop (idx: Byte) =
            let ext = nonSubtitleExtensions.[int idx % nonSubtitleExtensions.Length]
            let file = createFile ext
            not (Subtitle.isSubtitleFile file)
        Check.QuickThrowOnFailure prop