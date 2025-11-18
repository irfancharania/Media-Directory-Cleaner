namespace MediaDirectoryCleaner.Tests

open System.IO
open Xunit
open FsUnit.Xunit
open Domain
open FileSystemSetup

module MusicTests = 

    // ============================================================================
    // Music Tests
    // ============================================================================

    [<Fact>]
    let ``Empty album folder should be deleted``() =
        let testDir = createTestStructure [
            ("Rock Artist/Empty Album/cover.jpg", Some 50000L)
            ("Rock Artist/Empty Album/info.nfo", Some 1000L)
            // No audio files!
        ]
    
        try
            let result = Music.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // Album should be marked for deletion
                let album = Path.Combine(testDir, "Rock Artist", "Empty Album")
                items |> should contain album
            | Error _ ->
                failwith "Should have found empty album"
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Album with audio files should be kept``() =
        let testDir = createTestStructure [
            ("Jazz Artist/Good Album/track01.mp3", Some 5000000L) // 5 MB
            ("Jazz Artist/Good Album/track02.mp3", Some 4500000L)
            ("Jazz Artist/Good Album/cover.jpg", Some 50000L)
        ]
    
        try
            let result = Music.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // Album should NOT be in the list
                let album = Path.Combine(testDir, "Jazz Artist", "Good Album")
                items |> should not' (contain album)
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf "Unexpected error: %A" e
        finally
            cleanupTestDir testDir