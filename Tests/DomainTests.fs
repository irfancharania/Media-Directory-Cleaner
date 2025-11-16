module DomainTests

open Expecto
open Expecto.Flip
open Domain
open Size

// ============================================================================
// ValidatedPath Tests
// ============================================================================

let validatedPathTests =
    testList "ValidatedPath Tests" [
        
        test "create with empty path returns PathEmpty error" {
            let result = ValidatedPath.create ""
            match result with
            | Error PathEmpty -> ()
            | _ -> failtest "Expected PathEmpty error"
        }
        
        test "create with whitespace path returns PathEmpty error" {
            let result = ValidatedPath.create "   "
            match result with
            | Error PathEmpty -> ()
            | _ -> failtest "Expected PathEmpty error"
        }
        
        test "create with non-existent path returns PathNotFound error" {
            let result = ValidatedPath.create "C:\\NonExistentPath123456"
            match result with
            | Error (PathNotFound path) -> 
                Expect.equal "" "C:\\NonExistentPath123456" path
            | _ -> failtest "Expected PathNotFound error"
        }
    ]

// ============================================================================
// ExistingFile Tests
// ============================================================================

let existingFileTests =
    testList "ExistingFile Tests" [
        
        testList "classifyMediaType" [
            test "classifies .mp4 as Video" {
                let file = { 
                    FullPath = "movie.mp4"
                    Name = "movie.mp4"
                    Extension = ".mp4"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Video mediaType
            }
            
            test "classifies .mkv as Video" {
                let file = { 
                    FullPath = "movie.mkv"
                    Name = "movie.mkv"
                    Extension = ".mkv"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Video mediaType
            }
            
            test "classifies .mp3 as Audio" {
                let file = { 
                    FullPath = "song.mp3"
                    Name = "song.mp3"
                    Extension = ".mp3"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Audio mediaType
            }
            
            test "classifies .jpg as Image" {
                let file = { 
                    FullPath = "poster.jpg"
                    Name = "poster.jpg"
                    Extension = ".jpg"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Image mediaType
            }
            
            test "classifies .srt as Subtitle" {
                let file = { 
                    FullPath = "subs.srt"
                    Name = "subs.srt"
                    Extension = ".srt"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Subtitle mediaType
            }
            
            test "classifies unknown extension as Other" {
                let file = { 
                    FullPath = "readme.txt"
                    Name = "readme.txt"
                    Extension = ".txt"
                    SizeInBytes = 1000L<byte>
                }
                let mediaType = ExistingFile.classifyMediaType file
                Expect.equal "" Other mediaType
            }
        ]
    ]

// ============================================================================
// Active Pattern Tests
// ============================================================================

let activePatternTests =
    testList "Active Pattern Tests" [
        
        testList "VideoFile pattern" [
            test "matches large video file" {
                let file = { 
                    FullPath = "movie.mp4"
                    Name = "movie.mp4"
                    Extension = ".mp4"
                    SizeInBytes = 200000000L<byte>  // 190 MB
                }
                match file with
                | VideoFile 100L<MB> _ -> ()
                | _ -> failtest "Expected VideoFile match"
            }
            
            test "matches small video file by extension" {
                let file = { 
                    FullPath = "clip.mp4"
                    Name = "clip.mp4"
                    Extension = ".mp4"
                    SizeInBytes = 1000L<byte>  // Very small, but .mp4
                }
                match file with
                | VideoFile 100L<MB> _ -> ()
                | _ -> failtest "Expected VideoFile match"
            }
            
            test "does not match small non-video file" {
                let file = { 
                    FullPath = "poster.jpg"
                    Name = "poster.jpg"
                    Extension = ".jpg"
                    SizeInBytes = 1000L<byte>
                }
                match file with
                | VideoFile 100L<MB> _ -> failtest "Should not match"
                | _ -> ()
            }
        ]
        
        testList "AudioFile pattern" [
            test "matches audio by size" {
                let file = { 
                    FullPath = "song.mp3"
                    Name = "song.mp3"
                    Extension = ".mp3"
                    SizeInBytes = 600000L<byte>  // ~586 KB
                }
                match file with
                | AudioFile 500L<kB> _ -> ()
                | _ -> failtest "Expected AudioFile match"
            }
            
            test "matches audio by extension even if small" {
                let file = { 
                    FullPath = "sample.mp3"
                    Name = "sample.mp3"
                    Extension = ".mp3"
                    SizeInBytes = 1000L<byte>
                }
                match file with
                | AudioFile 500L<kB> _ -> ()
                | _ -> failtest "Expected AudioFile match"
            }
        ]
        
        testList "FolderImage pattern" [
            test "matches 'folder.jpg'" {
                let file = { 
                    FullPath = "folder.jpg"
                    Name = "folder.jpg"
                    Extension = ".jpg"
                    SizeInBytes = 1000L<byte>
                }
                match file with
                | FolderImage _ -> ()
                | _ -> failtest "Expected FolderImage match"
            }
            
            test "matches 'poster.jpg'" {
                let file = { 
                    FullPath = "poster.jpg"
                    Name = "poster.jpg"
                    Extension = ".jpg"
                    SizeInBytes = 1000L<byte>
                }
                match file with
                | FolderImage _ -> ()
                | _ -> failtest "Expected FolderImage match"
            }
            
            test "does not match 'fanart.jpg'" {
                let file = { 
                    FullPath = "fanart.jpg"
                    Name = "fanart.jpg"
                    Extension = ".jpg"
                    SizeInBytes = 1000L<byte>
                }
                match file with
                | FolderImage _ -> failtest "Should not match"
                | _ -> ()
            }
        ]
    ]

// ============================================================================
// Error Message Tests
// ============================================================================

let errorMessageTests =
    testList "DomainError Message Tests" [
        
        test "PathEmpty error has message" {
            let error = ValidationError PathEmpty |> DomainError.toMessage
            Expect.isNonEmpty "" error
        }
        
        test "PathNotFound error includes path" {
            let error = ValidationError (PathNotFound "C:\\test") |> DomainError.toMessage
            Expect.stringContains "" "C:\\test" error
        }
        
        test "NoLeafNodes returns None for optional message" {
            let error = DirectoryError (NoLeafNodes "test")
            let msg = DomainError.toOptionalMessage error
            Expect.isNone "" msg
        }
        
        test "PathNotFound returns Some for optional message" {
            let error = ValidationError (PathNotFound "test")
            let msg = DomainError.toOptionalMessage error
            Expect.isSome "" msg
        }
    ]

// ============================================================================
// All Tests
// ============================================================================

[<Tests>]
let tests =
    testList "Domain Module" [
        validatedPathTests
        existingFileTests
        activePatternTests
        errorMessageTests
    ]