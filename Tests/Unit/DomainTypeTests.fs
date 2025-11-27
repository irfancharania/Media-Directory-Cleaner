namespace MediaDirectoryCleaner.Tests.Unit

open Xunit
open Swensen.Unquote
open Domain
open Size
open System.IO

module DomainTypeTests =

    // ============================================================================
    // ValidatedPath Tests
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

    [<Fact>]
    let ``ValidatedPath combine with nested path``() =
        let basePath = ValidatedPath.createUnchecked "C:\\base"
        let combined = ValidatedPath.combine basePath "sub1\\sub2"
        test <@ combined = Path.Combine("C:\\base", "sub1\\sub2") @>

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

    [<Fact>]
    let ``DeletableItem File equality works``() =
        let file1 = DeletableItem.File "C:\\test.txt"
        let file2 = DeletableItem.File "C:\\test.txt"
        test <@ file1 = file2 @>

    [<Fact>]
    let ``DeletableItem Directory equality works``() =
        let dir1 = DeletableItem.Directory "C:\\folder"
        let dir2 = DeletableItem.Directory "C:\\folder"
        test <@ dir1 = dir2 @>

    // ============================================================================
    // ExistingFile Media Classification Tests
    // ============================================================================

    [<Fact>]
    let ``classifies mp4 as Video``() =
        let file = { FullPath = "movie.mp4"; Name = "movie.mp4"; Extension = ".mp4"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Video @>

    [<Fact>]
    let ``classifies mkv as Video``() =
        let file = { FullPath = "movie.mkv"; Name = "movie.mkv"; Extension = ".mkv"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Video @>
    
    [<Fact>]
    let ``classifies avi as Video``() =
        let file = { FullPath = "movie.avi"; Name = "movie.avi"; Extension = ".avi"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Video @>

    [<Fact>]
    let ``classifies wmv as Video``() =
        let file = { FullPath = "movie.wmv"; Name = "movie.wmv"; Extension = ".wmv"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Video @>

    [<Fact>]
    let ``classifies mp3 as Audio``() =
        let file = { FullPath = "song.mp3"; Name = "song.mp3"; Extension = ".mp3"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Audio @>
    
    [<Fact>]
    let ``classifies flac as Audio``() =
        let file = { FullPath = "song.flac"; Name = "song.flac"; Extension = ".flac"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Audio @>

    [<Fact>]
    let ``classifies ogg as Audio``() =
        let file = { FullPath = "song.ogg"; Name = "song.ogg"; Extension = ".ogg"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Audio @>

    [<Fact>]
    let ``classifies m4a as Audio``() =
        let file = { FullPath = "song.m4a"; Name = "song.m4a"; Extension = ".m4a"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Audio @>

    [<Fact>]
    let ``classifies srt as Subtitle``() =
        let file = { FullPath = "subs.srt"; Name = "subs.srt"; Extension = ".srt"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = MediaType.Subtitle @>
    
    [<Fact>]
    let ``classifies sub as Subtitle``() =
        let file = { FullPath = "subs.sub"; Name = "subs.sub"; Extension = ".sub"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = MediaType.Subtitle @>

    [<Fact>]
    let ``classifies ass as Subtitle``() =
        let file = { FullPath = "subs.ass"; Name = "subs.ass"; Extension = ".ass"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = MediaType.Subtitle @>

    [<Fact>]
    let ``classifies unknown extension as Other``() =
        let file = { FullPath = "readme.txt"; Name = "readme.txt"; Extension = ".txt"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Other @>

    [<Fact>]
    let ``classifies nfo as Other``() =
        let file = { FullPath = "info.nfo"; Name = "info.nfo"; Extension = ".nfo"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Other @>

    [<Fact>]
    let ``classifies jpg as Other``() =
        let file = { FullPath = "poster.jpg"; Name = "poster.jpg"; Extension = ".jpg"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Other @>
    
    [<Fact>]
    let ``extension classification is case insensitive``() =
        let file = { FullPath = "movie.MP4"; Name = "movie.MP4"; Extension = ".mp4"; SizeInBytes = 1000L<byte> }
        test <@ ExistingFile.classifyMediaType file = Video @>

    // ============================================================================
    // Active Pattern Tests - VideoFile
    // ============================================================================

    [<Fact>]
    let ``VideoFile pattern matches large video file``() =
        let file = { FullPath = "movie.mp4"; Name = "movie.mp4"; Extension = ".mp4"; SizeInBytes = 200000000L<byte> }
        let matched = match file with | VideoFile 100L<MB> _ -> true | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``VideoFile pattern matches small video file by extension``() =
        let file = { FullPath = "clip.mp4"; Name = "clip.mp4"; Extension = ".mp4"; SizeInBytes = 1000L<byte> }
        let matched = match file with | VideoFile 100L<MB> _ -> true | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``VideoFile pattern does not match small non-video file``() =
        let file = { FullPath = "poster.jpg"; Name = "poster.jpg"; Extension = ".jpg"; SizeInBytes = 1000L<byte> }
        let matched = match file with | VideoFile 100L<MB> _ -> true | _ -> false
        test <@ not matched @>
    
    [<Fact>]
    let ``VideoFile pattern matches large non-video file by size``() =
        let file = { FullPath = "data.bin"; Name = "data.bin"; Extension = ".bin"; SizeInBytes = 200000000L<byte> }
        let matched = match file with | VideoFile 100L<MB> _ -> true | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``VideoFile pattern with different threshold``() =
        let file = { FullPath = "data.bin"; Name = "data.bin"; Extension = ".bin"; SizeInBytes = 60000000L<byte> }  // ~57 MB
        let matchesLowThreshold = match file with | VideoFile 50L<MB> _ -> true | _ -> false
        let matchesHighThreshold = match file with | VideoFile 100L<MB> _ -> true | _ -> false
        test <@ matchesLowThreshold @>
        test <@ not matchesHighThreshold @>

    // ============================================================================
    // Active Pattern Tests - AudioFile
    // ============================================================================

    [<Fact>]
    let ``AudioFile pattern matches audio by size``() =
        let file = { FullPath = "song.mp3"; Name = "song.mp3"; Extension = ".mp3"; SizeInBytes = 600000L<byte> }
        let matched = match file with | AudioFile 500L<kB> _ -> true | _ -> false
        test <@ matched @>

    [<Fact>]
    let ``AudioFile pattern matches audio by extension even if small``() =
        let file = { FullPath = "sample.mp3"; Name = "sample.mp3"; Extension = ".mp3"; SizeInBytes = 1000L<byte> }
        let matched = match file with | AudioFile 500L<kB> _ -> true | _ -> false
        test <@ matched @>
    
    [<Fact>]
    let ``AudioFile pattern does not match small non-audio file``() =
        let file = { FullPath = "info.txt"; Name = "info.txt"; Extension = ".txt"; SizeInBytes = 1000L<byte> }
        let matched = match file with | AudioFile 500L<kB> _ -> true | _ -> false
        test <@ not matched @>

    [<Fact>]
    let ``AudioFile pattern matches large non-audio file by size``() =
        let file = { FullPath = "data.bin"; Name = "data.bin"; Extension = ".bin"; SizeInBytes = 600000L<byte> }
        let matched = match file with | AudioFile 500L<kB> _ -> true | _ -> false
        test <@ matched @>

    // ============================================================================
    // Active Pattern Tests - AudioFile
    // ============================================================================