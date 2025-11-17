namespace MediaDirectoryCleaner.Tests

open Xunit
open FsUnit.Xunit
open Domain
open Size

module DomainTests = 

    // ============================================================================
    // ValidatedPath Tests
    // ============================================================================

    [<Fact>]
    let ``create with empty path returns PathEmpty error``() =
        let result = ValidatedPath.create ""
        match result with
        | Error PathEmpty -> ()
        | _ -> failwith "Expected PathEmpty error"

    [<Fact>]
    let ``create with whitespace path returns PathEmpty error``() =
        let result = ValidatedPath.create "   "
        match result with
        | Error PathEmpty -> ()
        | _ -> failwith "Expected PathEmpty error"

    [<Fact>]
    let ``create with non-existent path returns PathNotFound error``() =
        let result = ValidatedPath.create "V:\\NonExistentPath123456"
        match result with
        | Error (PathNotFound path) -> 
            path |> should equal "V:\\NonExistentPath123456"
        | _ -> failwith "Expected PathNotFound error"

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
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Video

    [<Fact>]
    let ``classifies mkv as Video``() =
        let file = { 
            FullPath = "movie.mkv"
            Name = "movie.mkv"
            Extension = ".mkv"
            SizeInBytes = 1000L<byte>
        }
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Video

    [<Fact>]
    let ``classifies mp3 as Audio``() =
        let file = { 
            FullPath = "song.mp3"
            Name = "song.mp3"
            Extension = ".mp3"
            SizeInBytes = 1000L<byte>
        }
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Audio

    [<Fact>]
    let ``classifies jpg as Image``() =
        let file = { 
            FullPath = "poster.jpg"
            Name = "poster.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Image

    [<Fact>]
    let ``classifies srt as Subtitle``() =
        let file = { 
            FullPath = "subs.srt"
            Name = "subs.srt"
            Extension = ".srt"
            SizeInBytes = 1000L<byte>
        }
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Subtitle

    [<Fact>]
    let ``classifies unknown extension as Other``() =
        let file = { 
            FullPath = "readme.txt"
            Name = "readme.txt"
            Extension = ".txt"
            SizeInBytes = 1000L<byte>
        }
        let mediaType = ExistingFile.classifyMediaType file
        mediaType |> should equal Other

    // ============================================================================
    // Active Pattern Tests
    // ============================================================================

    [<Fact>]
    let ``VideoFile pattern matches large video file``() =
        let file = { 
            FullPath = "movie.mp4"
            Name = "movie.mp4"
            Extension = ".mp4"
            SizeInBytes = 200000000L<byte>  // 190 MB
        }
        match file with
        | VideoFile 100L<MB> _ -> ()
        | _ -> failwith "Expected VideoFile match"

    [<Fact>]
    let ``VideoFile pattern matches small video file by extension``() =
        let file = { 
            FullPath = "clip.mp4"
            Name = "clip.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000L<byte>  // Very small, but .mp4
        }
        match file with
        | VideoFile 100L<MB> _ -> ()
        | _ -> failwith "Expected VideoFile match"

    [<Fact>]
    let ``VideoFile pattern does not match small non-video file``() =
        let file = { 
            FullPath = "poster.jpg"
            Name = "poster.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        match file with
        | VideoFile 100L<MB> _ -> failwith "Should not match"
        | _ -> ()

    [<Fact>]
    let ``AudioFile pattern matches audio by size``() =
        let file = { 
            FullPath = "song.mp3"
            Name = "song.mp3"
            Extension = ".mp3"
            SizeInBytes = 600000L<byte>  // ~586 KB
        }
        match file with
        | AudioFile 500L<kB> _ -> ()
        | _ -> failwith "Expected AudioFile match"

    [<Fact>]
    let ``AudioFile pattern matches audio by extension even if small``() =
        let file = { 
            FullPath = "sample.mp3"
            Name = "sample.mp3"
            Extension = ".mp3"
            SizeInBytes = 1000L<byte>
        }
        match file with
        | AudioFile 500L<kB> _ -> ()
        | _ -> failwith "Expected AudioFile match"

    [<Fact>]
    let ``FolderImage pattern matches folder.jpg``() =
        let file = { 
            FullPath = "folder.jpg"
            Name = "folder.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        match file with
        | FolderImage _ -> ()
        | _ -> failwith "Expected FolderImage match"

    [<Fact>]
    let ``FolderImage pattern matches poster.jpg``() =
        let file = { 
            FullPath = "poster.jpg"
            Name = "poster.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        match file with
        | FolderImage _ -> ()
        | _ -> failwith "Expected FolderImage match"

    [<Fact>]
    let ``FolderImage pattern does not match fanart.jpg``() =
        let file = { 
            FullPath = "fanart.jpg"
            Name = "fanart.jpg"
            Extension = ".jpg"
            SizeInBytes = 1000L<byte>
        }
        match file with
        | FolderImage _ -> failwith "Should not match"
        | _ -> ()

    // ============================================================================
    // Error Message Tests
    // ============================================================================

    [<Fact>]
    let ``PathEmpty error has message``() =
        let error = ValidationError PathEmpty |> DomainError.toMessage
        error |> should not' (be EmptyString)

    [<Fact>]
    let ``PathNotFound error includes path``() =
        let error = ValidationError (PathNotFound "V:\\test") |> DomainError.toMessage
        error |> should haveSubstring "V:\\test"

    [<Fact>]
    let ``NoLeafNodes returns None for optional message``() =
        let error = DirectoryError (NoLeafNodes "test")
        let msg = DomainError.toOptionalMessage error
        msg |> should equal None

    [<Fact>]
    let ``PathNotFound returns Some for optional message``() =
        let error = ValidationError (PathNotFound "test")
        let msg = DomainError.toOptionalMessage error
        msg |> should not' (equal None)