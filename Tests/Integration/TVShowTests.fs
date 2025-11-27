namespace MediaDirectoryCleaner.Tests.Integration

open System.IO
open Xunit
open Swensen.Unquote
open Domain
open TestHelpers
open Errors

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
                test <@ Seq.isEmpty items @>
            | Error (DirectoryError (NoLeafNodes _)) ->
                ()  // Show root is not a leaf if it has subdirectories
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Empty Season Tests
    // ============================================================================

    [<Fact>]
    let ``Empty season folder should be deleted``() =
        let structure = [
            ("Show/banner.jpg", Some 18811L)
            ("Show/poster.jpg", Some 63078L)
            ("Show/Season 02/episode.nfo", Some 2424L)
            ("Show/Season 02/thumb.jpg", Some 37050L)
        ]
    
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let season2 = Path.Combine(testDir, "Show", "Season 02")
                test <@ containsDirectory season2 items @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
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
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Orphaned Metadata Tests
    // ============================================================================

    [<Fact>]
    let ``Orphaned metadata files should be deleted``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            ("Show/Season 01/Show.S01E01.mkv", Some 664624081L)
            ("Show/Season 01/Show.S01E01.srt", Some 36816L)
            ("Show/Season 01/Show.S01E01.nfo", Some 2424L)
            ("Show/Season 01/Show.S01E02.srt", Some 38139L)
            ("Show/Season 01/Show.S01E02.nfo", Some 2604L)
            ("Show/Season 01/Show.S01E02-thumb.jpg", Some 42952L)
        ]
    
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let e02srt = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02.srt")
                let e02nfo = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02.nfo")
                let e02thumb = Path.Combine(testDir, "Show", "Season 01", "Show.S01E02-thumb.jpg")
                
                test <@ containsFile e02srt items @>
                test <@ containsFile e02nfo items @>
                test <@ containsFile e02thumb items @>
            
                let season1 = Path.Combine(testDir, "Show", "Season 01")
                test <@ not (containsDirectory season1 items) @>
            
                test <@ not (containsPathSubstring "S01E01" items) @>
                
                let files, dirs = countItems items
                test <@ files = 3 @>
                test <@ dirs = 0 @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )
    
    [<Fact>]
    let ``Orphaned files with different naming patterns``() =
        let structure = [
            ("Show/Season 01/Episode_01.mp4", Some 600000000L)
            ("Show/Season 01/Episode_01.srt", Some 50000L)
            ("Show/Season 01/Episode_01.nfo", Some 2000L)
            ("Show/Season 01/Episode_02.srt", Some 50000L)
            ("Show/Season 01/Episode_02-thumb.jpg", Some 40000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                test <@ containsPathSubstring "Episode_02" items @>
                test <@ not (containsPathSubstring "Episode_01.srt" items) @>
                test <@ not (containsPathSubstring "Episode_01.nfo" items) @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
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
                test <@ Seq.isEmpty items @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
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
                test <@ Seq.isEmpty items @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Folder Image Preservation
    // ============================================================================

    [<Fact>]
    let ``Folder images are kept when video files present``() =
        let structure = [
            ("Show/Season 01/Episode.mp4", Some 600000000L)
            ("Show/Season 01/folder.jpg", Some 50000L)
            ("Show/Season 01/poster.png", Some 60000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                test <@ Seq.isEmpty items @>
                test <@ not (containsPathSubstring "folder.jpg" items) @>
                test <@ not (containsPathSubstring "poster.png" items) @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Folder images deleted when no video files present``() =
        let structure = [
            ("Show/Season 01/folder.jpg", Some 50000L)
            ("Show/Season 01/poster.jpg", Some 60000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                // Entire season folder deleted (no video files)
                let season = Path.Combine(testDir, "Show", "Season 01")
                test <@ containsDirectory season items @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Mixed Scenarios
    // ============================================================================

    [<Fact>]
    let ``Mix of valid episodes and orphaned files``() =
        let structure = [
            ("Show/poster.jpg", Some 63078L)
            ("Show/Season 01/E01.mp4", Some 600000000L)
            ("Show/Season 01/E01.srt", Some 50000L)
            ("Show/Season 01/E02.srt", Some 50000L)
            ("Show/Season 01/E02.nfo", Some 2000L)
            ("Show/Season 01/E03.mp4", Some 600000000L)
            ("Show/Season 01/E03.srt", Some 50000L)
            ("Show/Season 01/E04.nfo", Some 2000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                test <@ containsPathSubstring "E02.srt" items @>
                test <@ containsPathSubstring "E02.nfo" items @>
                test <@ containsPathSubstring "E04.nfo" items @>
                
                test <@ not (containsPathSubstring "E01" items) @>
                test <@ not (containsPathSubstring "E03" items) @>
                
                let season = Path.Combine(testDir, "Show", "Season 01")
                test <@ not (containsDirectory season items) @>
                
                let files, dirs = countItems items
                test <@ files = 3 @>
                test <@ dirs = 0 @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
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
                test <@ not (Directory.Exists(season)) @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
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
                test <@ not (File.Exists(orphan)) @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Error Cases
    // ============================================================================

    [<Fact>]
    let ``No items to clean returns empty list``() =
        let structure = [
            ("Show/Season 01/Episode.mp4", Some 600000000L)
            ("Show/Season 01/Episode.srt", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = TVShows.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                test <@ Seq.isEmpty items @>
            | Error e ->
                failwithf $"Unexpected error type: {DomainError.toMessage e}"
        )
    
    [<Fact>]
    let ``Invalid path returns validation error``() =
        let result = TVShows.clean "V:\\NonExistent\\Path\\12345" Domain.Preview
        
        match result with
        | Error (ValidationError (PathNotFound _)) ->
            ()
        | Error e ->
            failwithf $"Unexpected error type: {DomainError.toMessage e}"
        | Ok _ ->
            failwith "Should return error for invalid path"