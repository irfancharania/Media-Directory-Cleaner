namespace MediaDirectoryCleaner.Tests

open Xunit
open Swensen.Unquote
open Domain
open Size
open System.IO
open TestHelpers

module DomainTests =

    // ============================================================================
    // ValidatedPath Tests (Pure Domain - using internal createUnchecked)
    // ============================================================================

    [<Fact>]
    let ``ValidatedPath value extraction works``() =
        let path = ValidatedPath.createUnchecked "C:\\test"
        test <@ ValidatedPath.value path = "C:\\test" @>

    [<Fact>]
    let ``ValidatedPath combine works``() =
        let basePath = ValidatedPath.createUnchecked "C:\\base"
        let combined = ValidatedPath.combine basePath "subfolder"
        test <@ combined = Path.Combine("C:\\base", "subfolder") @>

    // ============================================================================
    // FileSystem.validatePath Tests (Infrastructure - I/O)
    // ============================================================================

    [<Fact>]
    let ``validatePath with empty path returns PathEmpty error``() =
        let result = FileSystem.validatePath ""
        test <@ result = Error PathEmpty @>

    [<Fact>]
    let ``validatePath with whitespace path returns PathEmpty error``() =
        let result = FileSystem.validatePath "   "
        test <@ result = Error PathEmpty @>

    [<Fact>]
    let ``validatePath with non-existent path returns PathNotFound error``() =
        let nonExistentPath = "V:\\NonExistentPath123456"
        let result = FileSystem.validatePath nonExistentPath
        test <@ result = Error (PathNotFound nonExistentPath) @>
    
    [<Fact>]
    let ``validatePath with valid directory returns Ok``() =
        withTestDir [] (fun testDir ->
            let result = FileSystem.validatePath testDir
            match result with
            | Ok validPath -> 
                test <@ ValidatedPath.value validPath = testDir @>
            | Error e -> 
                failwithf $"Expected Ok, got Error: {e}"
        )

    // ============================================================================
    // DeletableItem Tests
    // ============================================================================
    
    [<Fact>]
    let ``DeletableItem path extracts from File``() =
        let item = DeletableItem.File "C:\\test\\file.txt"
        test <@ DeletableItem.path item = "C:\\test\\file.txt" @>
    
    [<Fact>]
    let ``DeletableItem path extracts from Directory``() =
        let item = DeletableItem.Directory "C:\\test\\folder"
        test <@ DeletableItem.path item = "C:\\test\\folder" @>
    
    [<Fact>]
    let ``DeletableItem fromFile creates File``() =
        let item = DeletableItem.fromFile "test.txt"
        test <@ match item with DeletableItem.File _ -> true | _ -> false @>
    
    [<Fact>]
    let ``DeletableItem fromDirectory creates Directory``() =
        let item = DeletableItem.fromDirectory "folder"
        test <@ match item with DeletableItem.Directory _ -> true | _ -> false @>
    
    [<Fact>]
    let ``DeletableItem File and Directory are distinct``() =
        let file = DeletableItem.File "path"
        let dir = DeletableItem.Directory "path"
        test <@ file <> dir @>

    // ============================================================================
    // ExistingFile Tests
    // ============================================================================

    [<Fact>]
    let ``classifies mp4 as Video``() =
        let file = { 
            FullPath = "movie.mp4"
            Name = "movie.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Video @>

    [<Fact>]
    let ``classifies mkv as Video``() =
        let file = { 
            FullPath = "movie.mkv"
            Name = "movie.mkv"
            Extension = ".mkv"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Video @>
    
    [<Fact>]
    let ``classifies avi as Video``() =
        let file = { 
            FullPath = "movie.avi"
            Name = "movie.avi"
            Extension = ".avi"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Video @>

    [<Fact>]
    let ``classifies mp3 as Audio``() =
        let file = { 
            FullPath = "song.mp3"
            Name = "song.mp3"
            Extension = ".mp3"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Audio @>
    
    [<Fact>]
    let ``classifies flac as Audio``() =
        let file = { 
            FullPath = "song.flac"
            Name = "song.flac"
            Extension = ".flac"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Audio @>

    [<Fact>]
    let ``classifies jpg as Image``() =
        let file = { 
            FullPath = "poster.jpg"
            Name = "poster.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Image @>
    
    [<Fact>]
    let ``classifies png as Image``() =
        let file = { 
            FullPath = "cover.png"
            Name = "cover.png"
            Extension = ".png"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Image @>

    [<Fact>]
    let ``classifies srt as Subtitle``() =
        let file = { 
            FullPath = "subs.srt"
            Name = "subs.srt"
            Extension = ".srt"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Subtitle @>
    
    [<Fact>]
    let ``classifies sub as Subtitle``() =
        let file = { 
            FullPath = "subs.sub"
            Name = "subs.sub"
            Extension = ".sub"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Subtitle @>

    [<Fact>]
    let ``classifies unknown extension as Other``() =
        let file = { 
            FullPath = "readme.txt"
            Name = "readme.txt"
            Extension = ".txt"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Other @>
    
    [<Fact>]
    let ``extension is case insensitive``() =
        let file = { 
            FullPath = "movie.MP4"
            Name = "movie.MP4"
            Extension = ".mp4"
            SizeInBytes = 1000L<byte>
        }
        test <@ ExistingFile.classifyMediaType file = Video @>
    
    [<Fact>]
    let ``sizeInMB converts correctly``() =
        let file = { 
            FullPath = "test"
            Name = "test"
            Extension = ".mp4"
            SizeInBytes = 2097152L<byte>  // 2 MB
        }
        test <@ ExistingFile.sizeInMB file = 2L<MB> @>
    
    [<Fact>]
    let ``sizeInKB converts correctly``() =
        let file = { 
            FullPath = "test"
            Name = "test"
            Extension = ".mp3"
            SizeInBytes = 1024000L<byte>  // 1000 KB
        }
        test <@ ExistingFile.sizeInKB file = 1000L<kB> @>

    // ============================================================================
    // Active Pattern Tests
    // ============================================================================

    [<Fact>]
    let ``VideoFile pattern matches large video file``() =
        let file = { 
            FullPath = "movie.mp4"
            Name = "movie.mp4"
            Extension = ".mp4"
            SizeInBytes = 200000000L<byte>  // ~190 MB
        }
        let matched = 
            match file with
            | VideoFile 100L<MB> _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``VideoFile pattern matches small video file by extension``() =
        let file = { 
            FullPath = "clip.mp4"
            Name = "clip.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | VideoFile 100L<MB> _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``VideoFile pattern does not match small non-video file``() =
        let file = { 
            FullPath = "poster.jpg"
            Name = "poster.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | VideoFile 100L<MB> _ -> true
            | _ -> false
        test <@ not matched @>
    
    [<Fact>]
    let ``VideoFile pattern matches large non-video file by size``() =
        let file = { 
            FullPath = "data.bin"
            Name = "data.bin"
            Extension = ".bin"
            SizeInBytes = 200000000L<byte>  // 190 MB
        }
        let matched = 
            match file with
            | VideoFile 100L<MB> _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``AudioFile pattern matches audio by size``() =
        let file = { 
            FullPath = "song.mp3"
            Name = "song.mp3"
            Extension = ".mp3"
            SizeInBytes = 600000L<byte>  // ~586 KB
        }
        let matched = 
            match file with
            | AudioFile 500L<kB> _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``AudioFile pattern matches audio by extension even if small``() =
        let file = { 
            FullPath = "sample.mp3"
            Name = "sample.mp3"
            Extension = ".mp3"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | AudioFile 500L<kB> _ -> true
            | _ -> false
        test <@ matched @>
    
    [<Fact>]
    let ``AudioFile pattern does not match small non-audio file``() =
        let file = { 
            FullPath = "info.txt"
            Name = "info.txt"
            Extension = ".txt"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | AudioFile 500L<kB> _ -> true
            | _ -> false
        test <@ not matched @>

    [<Fact>]
    let ``FolderImage pattern matches folder.jpg``() =
        let file = { 
            FullPath = "folder.jpg"
            Name = "folder.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | FolderImage _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``FolderImage pattern matches poster.png``() =
        let file = { 
            FullPath = "poster.png"
            Name = "poster.png"
            Extension = ".png"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | FolderImage _ -> true
            | _ -> false
        test <@ matched @>
    
    [<Fact>]
    let ``FolderImage pattern is case insensitive``() =
        let file = { 
            FullPath = "FOLDER.JPG"
            Name = "FOLDER.JPG"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | FolderImage _ -> true
            | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``FolderImage pattern does not match fanart.jpg``() =
        let file = { 
            FullPath = "fanart.jpg"
            Name = "fanart.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        let matched = 
            match file with
            | FolderImage _ -> true
            | _ -> false
        test <@ not matched @>

    // ============================================================================
    // Error Message Tests
    // ============================================================================

    [<Fact>]
    let ``PathEmpty error has non-empty message``() =
        let error = ValidationError PathEmpty |> DomainError.toMessage
        test <@ not (System.String.IsNullOrWhiteSpace(error)) @>

    [<Fact>]
    let ``PathNotFound error includes path``() =
        let testPath = "V:\\test"
        let error = ValidationError (PathNotFound testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>
    
    [<Fact>]
    let ``PathNotDirectory error includes path``() =
        let testPath = "C:\\file.txt"
        let error = ValidationError (PathNotDirectory testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>

    [<Fact>]
    let ``NoLeafNodes returns None for optional message``() =
        let error = DirectoryError (NoLeafNodes "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>
    
    [<Fact>]
    let ``NoSubdirectories returns None for optional message``() =
        let error = DirectoryError (NoSubdirectories "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>

    [<Fact>]
    let ``NothingToClean returns None for optional message``() =
        let error = CleaningError (NothingToClean "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>

    [<Fact>]
    let ``PathNotFound returns Some for optional message``() =
        let error = ValidationError (PathNotFound "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>
    
    [<Fact>]
    let ``AccessDenied returns Some for optional message``() =
        let error = DirectoryError (AccessDenied ("test", System.Exception("test")))
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>
    
    [<Fact>]
    let ``DeletionFailed returns Some for optional message``() =
        let error = CleaningError (DeletionFailed ("test", System.Exception("test")))
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>

    // ============================================================================
    // Subtitle Matching Tests
    // ============================================================================

    [<Fact>]
    let ``matchesVideoFile returns true when subtitle matches video exactly``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie.2024.1080p.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.1080p.mp4"
            Name = "Movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``matchesVideoFile returns false when subtitle does not match any video``() =
        let subtitlePath = "V:\\Movies\\Test\\Different.Name.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.1080p.mp4"
            Name = "Movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ not (Subtitle.matchesVideoFile subtitlePath files) @>

    [<Fact>]
    let ``matchesVideoFile is case insensitive``() =
        let subtitlePath = "V:\\Movies\\Test\\MOVIE.2024.1080P.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\movie.2024.1080p.mp4"
            Name = "movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``matchesVideoFile ignores non-video files``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie.2024.srt"
        let imageFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.jpg"
            Name = "Movie.2024.jpg"
            Extension = ".jpg"
            SizeInBytes = 500000L<byte>
        }
        let files = [imageFile]
        test <@ not (Subtitle.matchesVideoFile subtitlePath files) @>
    
    [<Fact>]
    let ``matchesVideoFile works with multiple videos``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie2.srt"
        let video1 = { 
            FullPath = "V:\\Movies\\Test\\Movie1.mp4"
            Name = "Movie1.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000000000L<byte>
        }
        let video2 = { 
            FullPath = "V:\\Movies\\Test\\Movie2.mp4"
            Name = "Movie2.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000000000L<byte>
        }
        let files = [video1; video2]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``isSubtitleFile detects srt files``() =
        let file = { 
            FullPath = "test.srt"
            Name = "test.srt"
            Extension = ".srt"
            SizeInBytes = 1000L<byte>
        }
        test <@ Subtitle.isSubtitleFile file @>
    
    [<Fact>]
    let ``isSubtitleFile detects sub files``() =
        let file = { 
            FullPath = "test.sub"
            Name = "test.sub"
            Extension = ".sub"
            SizeInBytes = 1000L<byte>
        }
        test <@ Subtitle.isSubtitleFile file @>
    
    [<Fact>]
    let ``isSubtitleFile rejects non-subtitle files``() =
        let file = { 
            FullPath = "test.txt"
            Name = "test.txt"
            Extension = ".txt"
            SizeInBytes = 1000L<byte>
        }
        test <@ not (Subtitle.isSubtitleFile file) @>

    [<Fact>]
    let ``isUncertain returns true when no language code detected``() =
        test <@ Subtitle.isUncertain "movie.srt" @>

    [<Fact>]
    let ``isUncertain returns false when English detected``() =
        test <@ not (Subtitle.isUncertain "movie.eng.srt") @>

    [<Fact>]
    let ``isUncertain returns false when other language detected``() =
        test <@ not (Subtitle.isUncertain "movie.spa.srt") @>
    
    [<Fact>]
    let ``isUncertain returns false when French detected``() =
        test <@ not (Subtitle.isUncertain "movie.fre.srt") @>