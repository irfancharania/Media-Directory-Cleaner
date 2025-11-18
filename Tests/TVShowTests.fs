namespace MediaDirectoryCleaner.Tests

open System.IO
open Xunit
open FsUnit.Xunit
open Domain
open FileSystemSetup

module TVShowTests = 

    // ============================================================================
    // TV Show Tests
    // ============================================================================

    [<Fact>]
    let ``TV show with valid season - keep everything``() =
        let testDir = createTestStructure [
            ("Crime Drama/banner.jpg", Some 18811L)
            ("Crime Drama/fanart.jpg", Some 125482L)
            ("Crime Drama/folder.jpg", Some 63078L)
            ("Crime Drama/poster.jpg", Some 63078L)
            ("Crime Drama/tvshow.nfo", Some 1790L)
            ("Crime Drama/.actors/Lead_Detective.jpg", Some 29347L)
            ("Crime Drama/.actors/Partner.jpg", Some 33759L)
            // Season 1 with video files (664 MB and 730 MB)
            ("Crime Drama/Season 01/Drama.S01E01.Episode One.mkv", Some 664624081L)
            ("Crime Drama/Season 01/Drama.S01E01.Episode One.eng.srt", Some 36816L)
            ("Crime Drama/Season 01/Drama.S01E01.Episode One.nfo", Some 2424L)
            ("Crime Drama/Season 01/Drama.S01E01.Episode One-thumb.jpg", Some 37050L)
            ("Crime Drama/Season 01/Drama.S01E02.Episode Two.mkv", Some 730031885L)
            ("Crime Drama/Season 01/Drama.S01E02.Episode Two.eng.srt", Some 38139L)
            ("Crime Drama/Season 01/Drama.S01E02.Episode Two.nfo", Some 2604L)
            ("Crime Drama/Season 01/Drama.S01E02.Episode Two-thumb.jpg", Some 42952L)
        ]
    
        try
            let result = TVShows.clean testDir Domain.Preview
        
            // Should find nothing to delete (or only orphaned files if any)
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                // Season folder should NOT be in the list (has video files)
                let season1 = Path.Combine(testDir, "Crime Drama", "Season 01")
                itemList |> should not' (contain season1)
            | Error (DirectoryError (NoLeafNodes _)) 
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf "Unexpected error: %A" e
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Empty season folder should be deleted``() =
        let testDir = createTestStructure [
            ("Mystery Show/banner.jpg", Some 18811L)
            ("Mystery Show/poster.jpg", Some 63078L)
            // Empty season folder with only metadata
            ("Mystery Show/Season 02/episode.nfo", Some 2424L)
            ("Mystery Show/Season 02/thumb.jpg", Some 37050L)
            // No video files!
        ]
    
        try
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                // Season folder should be marked for deletion
                let season2 = Path.Combine(testDir, "Mystery Show", "Season 02")
                items |> should contain season2
            | Error _ ->
                failwith "Should have found empty season to delete"
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Orphaned metadata files should be deleted``() =
        let testDir = createTestStructure [
            ("Sci-Fi Series/poster.jpg", Some 63078L)
            ("Sci-Fi Series/.actors/Protagonist.jpg", Some 29347L)
            // Video file exists for episode 1
            ("Sci-Fi Series/Season 01/SciFi.S01E01.mkv", Some 664624081L)
            ("Sci-Fi Series/Season 01/SciFi.S01E01.eng.srt", Some 36816L)
            ("Sci-Fi Series/Season 01/SciFi.S01E01.nfo", Some 2424L)
            ("Sci-Fi Series/Season 01/SciFi.S01E01-thumb.jpg", Some 37050L)
            // Orphaned metadata for episode 2 (no video file!)
            ("Sci-Fi Series/Season 01/SciFi.S01E02.eng.srt", Some 38139L)
            ("Sci-Fi Series/Season 01/SciFi.S01E02.nfo", Some 2604L)
            ("Sci-Fi Series/Season 01/SciFi.S01E02-thumb.jpg", Some 42952L)
            // Orphaned metadata for episode 3 (no video file!)
            ("Sci-Fi Series/Season 01/SciFi.S01E03.eng.srt", Some 35000L)
            ("Sci-Fi Series/Season 01/SciFi.S01E03.nfo", Some 2500L)
        ]
    
        try
            let result = TVShows.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                // Should delete orphaned metadata for episode 2
                items |> should contain (Path.Combine(testDir, "Sci-Fi Series", "Season 01", "SciFi.S01E02.eng.srt"))
                items |> should contain (Path.Combine(testDir, "Sci-Fi Series", "Season 01", "SciFi.S01E02.nfo"))
                items |> should contain (Path.Combine(testDir, "Sci-Fi Series", "Season 01", "SciFi.S01E02-thumb.jpg"))
            
                // Should delete orphaned metadata for episode 3
                items |> should contain (Path.Combine(testDir, "Sci-Fi Series", "Season 01", "SciFi.S01E03.eng.srt"))
                items |> should contain (Path.Combine(testDir, "Sci-Fi Series", "Season 01", "SciFi.S01E03.nfo"))
            
                // Should NOT delete season folder (has video for episode 1)
                let season1 = Path.Combine(testDir, "Sci-Fi Series", "Season 01")
                itemList |> should not' (contain season1)
            
                // Should NOT delete matched metadata for episode 1
                itemList |> List.exists (fun x -> x.Contains("SciFi.S01E01")) |> should be False
            | Error _ ->
                failwith "Should have found orphaned metadata files"
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``.actors folder should not be processed separately``() =
        let testDir = createTestStructure [
            ("Action Series/poster.jpg", Some 63078L)
            ("Action Series/.actors/hero.jpg", Some 29347L)
            ("Action Series/.actors/villain.jpg", Some 33759L)
            // Season with video
            ("Action Series/Season 01/Action.S01E01.mkv", Some 664624081L)
        ]
    
        try
            let result = TVShows.clean testDir Domain.Preview
        
            // .actors files should not be in the results
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                itemList |> List.exists (fun x -> x.Contains(".actors")) |> should be False
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf "Unexpected error: %A" e
        finally
            cleanupTestDir testDir
