namespace MediaDirectoryCleaner.Tests

open System.IO
open Xunit
open Swensen.Unquote
open Domain
open TestHelpers
open Errors

module MusicTests = 

    // ============================================================================
    // Empty Album Tests
    // ============================================================================

    [<Fact>]
    let ``Empty album folder should be deleted``() =
        withTestDir (musicAlbum "Artist" "Empty Album" false) (fun testDir ->
            let result = Music.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let album = Path.Combine(testDir, "Artist", "Empty Album")
                test <@ containsDirectory album items @>
                
                let files, dirs = countItems items
                test <@ dirs = 1 @>
                test <@ files = 0 @>
            | Error _ ->
                failwith "Should have found empty album"
        )
    
    [<Fact>]
    let ``Multiple empty albums all deleted``() =
        let structure = [
            ("Artist/Empty1/cover.jpg", Some 50000L)
            ("Artist/Empty2/info.nfo", Some 1000L)
            ("Artist/Empty3/folder.jpg", Some 60000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let e1 = Path.Combine(testDir, "Artist", "Empty1")
                let e2 = Path.Combine(testDir, "Artist", "Empty2")
                let e3 = Path.Combine(testDir, "Artist", "Empty3")
                
                test <@ containsDirectory e1 items @>
                test <@ containsDirectory e2 items @>
                test <@ containsDirectory e3 items @>
                
                let files, dirs = countItems items
                test <@ dirs = 3 @>
                test <@ files = 0 @>
            | Error _ ->
                failwith "Should have found empty albums"
        )

    // ============================================================================
    // Album with Audio Tests
    // ============================================================================

    [<Fact>]
    let ``Album with audio files should be kept``() =
        withTestDir (musicAlbum "Artist" "Good Album" true) (fun testDir ->
            let result = Music.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let album = Path.Combine(testDir, "Artist", "Good Album")
                test <@ not (containsDirectory album items) @>
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Album with single small audio file is kept``() =
        let structure = [
            ("Artist/Album/track.mp3", Some 1000L)  // Tiny file
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Album kept despite small audio file
                let album = Path.Combine(testDir, "Artist", "Album")
                test <@ not (containsDirectory album items) @>
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Various Audio Format Tests
    // ============================================================================

    [<Fact>]
    let ``Album with mp3 files is kept``() =
        let structure = [
            ("Artist/Album/track.mp3", Some 5000000L)
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Album with flac files is kept``() =
        let structure = [
            ("Artist/Album/track.flac", Some 20000000L)  // 20 MB
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Album with wav files is kept``() =
        let structure = [
            ("Artist/Album/track.wav", Some 30000000L)
            ("Artist/Album/info.txt", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Album with m4a files is kept``() =
        let structure = [
            ("Artist/Album/track.m4a", Some 5000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Size Threshold Tests (500 KB)
    // ============================================================================

    [<Fact>]
    let ``Album under 500KB with no audio is deleted``() =
        let structure = [
            ("Artist/Small/cover.jpg", Some 250000L)  // 250 KB
            ("Artist/Small/info.txt", Some 200000L)   // 200 KB
            // Total: 450 KB, no audio
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let album = Path.Combine(testDir, "Artist", "Small")
                test <@ containsDirectory album items @>
            | Error _ ->
                failwith "Should delete album under threshold"
        )
    
    [<Fact>]
    let ``Large audio file by size threshold is kept``() =
        let structure = [
            ("Artist/Album/large.mp3", Some 600000L)  // 600 KB - over threshold
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - audio file over 500KB threshold
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Mixed Scenarios
    // ============================================================================

    [<Fact>]
    let ``Mix of empty and valid albums``() =
        let structure = [
            // Valid album 1
            ("Artist/Album1/track.mp3", Some 5000000L)
            ("Artist/Album1/cover.jpg", Some 50000L)
            // Empty album
            ("Artist/Empty/cover.jpg", Some 50000L)
            // Valid album 2
            ("Artist/Album2/song.flac", Some 20000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Only empty album deleted
                let empty = Path.Combine(testDir, "Artist", "Empty")
                test <@ containsDirectory empty items @>
                
                // Valid albums NOT deleted
                let a1 = Path.Combine(testDir, "Artist", "Album1")
                let a2 = Path.Combine(testDir, "Artist", "Album2")
                test <@ not (containsDirectory a1 items) @>
                test <@ not (containsDirectory a2 items) @>
                
                let files, dirs = countItems items
                test <@ dirs = 1 @>
                test <@ files = 0 @>
            | Error _ ->
                failwith "Should have found empty album"
        )
    
    [<Fact>]
    let ``Multiple artists with mixed albums``() =
        let structure = [
            // Artist 1 - has audio
            ("Artist1/Album/track.mp3", Some 5000000L)
            // Artist 2 - empty
            ("Artist2/Album/cover.jpg", Some 50000L)
            // Artist 3 - has audio
            ("Artist3/Album/song.wav", Some 30000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Only Artist2's album deleted
                let a2 = Path.Combine(testDir, "Artist2", "Album")
                test <@ containsDirectory a2 items @>
                
                let files, dirs = countItems items
                test <@ dirs = 1 @>
            | Error _ ->
                failwith "Should have found empty album"
        )

    // ============================================================================
    // Nested Structure Tests
    // ============================================================================

    [<Fact>]
    let ``Deeply nested empty album is detected``() =
        let structure = [
            ("Genre/Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let album = Path.Combine(testDir, "Genre", "Artist", "Album")
                test <@ containsDirectory album items @>
            | Error _ ->
                failwith "Should find nested empty album"
        )
    
    [<Fact>]
    let ``Album with audio in parent has empty subdirectory``() =
        let structure = [
            ("Artist/Album/track.mp3", Some 5000000L)
            ("Artist/Album/Bonus/cover.jpg", Some 50000L)  // Empty subdir
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Empty Bonus subdirectory should be marked for deletion
                let bonus = Path.Combine(testDir, "Artist", "Album", "Bonus")
                test <@ containsDirectory bonus items @>
                
                // Parent album NOT deleted
                let album = Path.Combine(testDir, "Artist", "Album")
                test <@ not (containsDirectory album items) @>
            | Error _ ->
                failwith "Should find empty subdirectory"
        )

    // ============================================================================
    // Execute Mode Tests
    // ============================================================================

    [<Fact>]
    let ``Execute mode actually deletes empty album``() =
        let structure = [
            ("Artist/Empty/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let album = Path.Combine(testDir, "Artist", "Empty")
            test <@ Directory.Exists(album) @>
            
            let result = Music.clean testDir Domain.Execute
            
            match result with
            | Ok items ->
                test <@ containsDirectory album items @>
                // Verify actual deletion
                test <@ not (Directory.Exists(album)) @>
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Execute mode preserves albums with audio``() =
        let structure = [
            ("Artist/Album/track.mp3", Some 5000000L)
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let album = Path.Combine(testDir, "Artist", "Album")
            let audioFile = Path.Combine(album, "track.mp3")
            
            let result = Music.clean testDir Domain.Execute
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                // Verify files still exist
                test <@ Directory.Exists(album) @>
                test <@ File.Exists(audioFile) @>
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Error Cases
    // ============================================================================

    [<Fact>]
    let ``No items to clean returns appropriate error``() =
        let structure = [
            ("Artist/Album/track.mp3", Some 5000000L)
            ("Artist/Album/cover.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should return error when nothing to clean"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error type: {e}"
        )
    
    [<Fact>]
    let ``Invalid path returns validation error``() =
        let result = Music.clean "V:\\NonExistent\\Path\\12345" Domain.Preview
        
        match result with
        | Error (ValidationError (PathNotFound _)) ->
            () // Expected
        | Error e ->
            failwithf $"Unexpected error type: {e}"
        | Ok _ ->
            failwith "Should return error for invalid path"
    
    // ============================================================================
    // Edge Cases
    // ============================================================================

    [<Fact>]
    let ``Album with only metadata files is deleted``() =
        let structure = [
            ("Artist/Album/cover.jpg", Some 50000L)
            ("Artist/Album/info.nfo", Some 1000L)
            ("Artist/Album/folder.jpg", Some 60000L)
            ("Artist/Album/artist.txt", Some 500L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let album = Path.Combine(testDir, "Artist", "Album")
                test <@ containsDirectory album items @>
            | Error _ ->
                failwith "Should delete metadata-only album"
        )
    
    [<Fact>]
    let ``Album with ogg files is kept``() =
        let structure = [
            ("Artist/Album/track.ogg", Some 5000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Music.clean testDir Domain.Preview
            
            match result with
            | Ok _ ->
                failwith "Should not find items to delete"
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - ogg is audio format
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )