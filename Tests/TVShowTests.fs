namespace MediaDirectoryCleaner.Tests

open System.IO
open Xunit
open Swensen.Unquote
open Domain
open TestHelpers

module TVShowTests = 

    // ============================================================================
    // TV Show with Valid Episodes
    // ============================================================================

    [<Fact>]
    let ``Season with video files - nothing to delete``() =
        withTestDir (tvSeasonWithEpisodes "Drama" "Season 01" [(1, true); (2, true)]) (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // Should not delete season folder (has videos)
                let season1 = Path.Combine(testDir, "Drama", "Season 01")
                test <@ not (containsDirectory season1 items) @>
                
                // Verify all items are files, not directories
                let _, dirs = countItems items
                test <@ dirs = 0 @>
            | Error (DirectoryError (NoLeafNodes _)) 
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Empty Season Tests
    // ============================================================================

    [<Fact>]
    let ``Empty season folder should be deleted``() =
        let structure = [
            ("Show/banner.jpg", Some 18811L)
            ("Show/poster.jpg", Some 63078L)
            // Season with metadata but NO video
            ("Show/Season 02/episode.nfo", Some 2424L)
            ("Show/Season 02/thumb.jpg", Some 37050L)
        ]
    
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let season2 = Path.Combine(testDir, "Show", "Season 02")
                test <@ containsDirectory season2 items @>
            | Error _ ->
                failwith "Should have found empty season to delete"
        )
    
    [<Fact>]
    let ``Multiple empty seasons all deleted``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            ("Show/Season 01/metadata.nfo", Some 1000L)
            ("Show/Season 02/metadata.nfo", Some 1000L)
            ("Show/Season 03/metadata.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let s1 = Path.Combine(testDir, "Show", "Season 01")
                let s2 = Path.Combine(testDir, "Show", "Season 02")
                let s3 = Path.Combine(testDir, "Show", "Season 03")
                
                test <@ containsDirectory s1 items @>
                test <@ containsDirectory s2 items @>
                test <@ containsDirectory s3 items @>
                
                let files, dirs = countItems items
                test <@ dirs = 3 @>
                test <@ files = 0 @>
            | Error _ ->
                failwith "Should have found empty seasons"
        )

    // ============================================================================
    // Orphaned Metadata Tests
    // ============================================================================

    [<Fact>]
    let ``Orphaned metadata files should be deleted``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            // Episode 1 has video
            ("Show/Season 01/Show.S01E01.mkv", Some 664624081L)
            ("Show/Season 01/Show.S01E01.srt", Some 36816L)
            ("Show/Season 01/Show.S01E01.nfo", Some 2424L)
            // Episode 2 - ORPHANED metadata (no video)
            ("Show/Season 01/Show.S01E02.srt", Some 38139L)
            ("Show/Season 01/Show.S01E02.nfo", Some 2604L)
            ("Show/Season 01/Show.S01E02-thumb.jpg", Some 42952L)
        ]
    
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // Should delete orphaned files
                let e02srt = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02.srt")
                let e02nfo = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02.nfo")
                let e02thumb = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02-thumb.jpg")
                
                test <@ containsFile e02srt items @>
                test <@ containsFile e02nfo items @>
                test <@ containsFile e02thumb items @>
            
                // Should NOT delete season folder
                let season1 = Path.Combine(testDir, "Show", "Season 01")
                test <@ not (containsDirectory season1 items) @>
            
                // Should NOT delete matched metadata
                test <@ not (containsPathSubstring "S01E01" items) @>
                
                let files, dirs = countItems items
                test <@ files = 3 @>
                test <@ dirs = 0 @>
            | Error _ ->
                failwith "Should have found orphaned metadata"
        )
    
    [<Fact>]
    let ``Orphaned files with different naming patterns``() =
        let structure = [
            // Video exists
            ("Show/Season 01/Episode_01.mp4", Some 600000000L)
            // Matching metadata
            ("Show/Season 01/Episode_01.srt", Some 50000L)
            ("Show/Season 01/Episode_01.nfo", Some 2000L)
            // Orphaned with underscore
            ("Show/Season 01/Episode_02.srt", Some 50000L)
            ("Show/Season 01/Episode_02-thumb.jpg", Some 40000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Orphaned files deleted
                test <@ containsPathSubstring "Episode_02" items @>
                // Matched files NOT deleted
                test <@ not (containsPathSubstring "Episode_01.srt" items) @>
                test <@ not (containsPathSubstring "Episode_01.nfo" items) @>
            | Error _ ->
                failwith "Should have found orphaned files"
        )

    // ============================================================================
    // Filename Normalization Tests
    // ============================================================================

    [<Fact>]
    let ``Subtitle with eng suffix matches video``() =
        let structure = [
            ("Show/Season 01/Episode.mkv", Some 600000000L)
            ("Show/Season 01/Episode.eng.srt", Some 50000L)
            ("Show/Season 01/Episode.en.srt", Some 48000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // English subtitles should be kept (not orphaned)
                test <@ Seq.isEmpty items @>
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Thumb suffix is normalized for matching``() =
        let structure = [
            ("Show/Season 01/Episode.mp4", Some 600000000L)
            ("Show/Season 01/Episode-thumb.jpg", Some 40000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Thumb should match video (not orphaned)
                test <@ Seq.isEmpty items @>
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Special Directories Tests
    // ============================================================================

    [<Fact>]
    let ``actors folder files are not processed separately``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            ("Show/.actors/hero.jpg", Some 29347L)
            ("Show/.actors/villain.jpg", Some 33759L)
            // Season with video
            ("Show/Season 01/Show.S01E01.mkv", Some 664624081L)
        ]
    
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // .actors files should not appear in results
                test <@ not (containsPathSubstring ".actors" items) @>
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Folder images are kept even without video``() =
        let structure = [
            ("Show/Season 01/folder.jpg", Some 50000L)
            ("Show/Season 01/poster.jpg", Some 60000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Season folder deleted, but not because of folder images
                let season = Path.Combine(testDir, "Show", "Season 01")
                test <@ containsDirectory season items @>
            | Error _ ->
                failwith "Should delete empty season"
        )

    // ============================================================================
    // Mixed Scenarios
    // ============================================================================

    [<Fact>]
    let ``Mix of valid episodes and orphaned files``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            // Episode 1 - complete
            ("Show/Season 01/E01.mp4", Some 600000000L)
            ("Show/Season 01/E01.srt", Some 50000L)
            // Episode 2 - orphaned
            ("Show/Season 01/E02.srt", Some 50000L)
            ("Show/Season 01/E02.nfo", Some 2000L)
            // Episode 3 - complete
            ("Show/Season 01/E03.mp4", Some 600000000L)
            ("Show/Season 01/E03.srt", Some 50000L)
            // Episode 4 - orphaned
            ("Show/Season 01/E04.nfo", Some 2000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Orphaned files from E02 and E04
                test <@ containsPathSubstring "E02.srt" items @>
                test <@ containsPathSubstring "E02.nfo" items @>
                test <@ containsPathSubstring "E04.nfo" items @>
                
                // Valid episode files NOT deleted
                test <@ not (containsPathSubstring "E01" items) @>
                test <@ not (containsPathSubstring "E03" items) @>
                
                // Season folder NOT deleted
                let season = Path.Combine(testDir, "Show", "Season 01")
                test <@ not (containsDirectory season items) @>
                
                let files, dirs = countItems items
                test <@ files = 3 @>
                test <@ dirs = 0 @>
            | Error _ ->
                failwith "Should have found orphaned files"
        )

    // ============================================================================
    // Execute Mode Tests
    // ============================================================================

    [<Fact>]
    let ``Execute mode deletes empty season folder``() =
        let structure = [
            ("Show/Season 01/metadata.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let season = Path.Combine(testDir, "Show", "Season 01")
            test <@ Directory.Exists(season) @>
            
            let result = TVShows.clean testDir Domain.Execute
            
            match result with
            | Ok items ->
                test <@ containsDirectory season items @>
                // Verify actual deletion
                test <@ not (Directory.Exists(season)) @>
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )
    
    [<Fact>]
    let ``Execute mode deletes orphaned files``() =
        let structure = [
            ("Show/Season 01/Episode.mp4", Some 600000000L)
            ("Show/Season 01/Orphan.srt", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let orphan = Path.Combine(testDir, "Show", "Season 01", "Orphan.srt")
            test <@ File.Exists(orphan) @>
            
            let result = TVShows.clean testDir Domain.Execute
            
            match result with
            | Ok items ->
                test <@ containsFile orphan items @>
                // Verify actual deletion
                test <@ not (File.Exists(orphan)) @>
            | Error e ->
                failwithf $"Unexpected error: {e}"
        )

    // ============================================================================
    // Error Cases
    // ============================================================================

    [<Fact>]
    let ``No items to clean returns appropriate error``() =
        let structure = [
            ("Show/Season 01/Episode.mp4", Some 600000000L)
            ("Show/Season 01/Episode.srt", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
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
        let result = TVShows.clean "V:\\NonExistent\\Path\\12345" Domain.Preview
        
        match result with
        | Error (ValidationError (PathNotFound _)) ->
            () // Expected
        | Error e ->
            failwithf $"Unexpected error type: {e}"
        | Ok _ ->
            failwith "Should return error for invalid path"