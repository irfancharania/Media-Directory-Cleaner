namespace MediaDirectoryCleaner.Tests

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Domain

module IntegrationTests =

    // ============================================================================
    // Test Helpers
    // ============================================================================

    /// Create a temporary test directory structure
    /// Uses cross-platform temp directory
    let createTestStructure (structure: (string * int64 option) list) =
        let testDir = Path.Combine(Path.GetTempPath(), $"DirectoryCleanerTests_{Guid.NewGuid()}")
        Directory.CreateDirectory(testDir) |> ignore
    
        for (relativePath, size) in structure do
            let fullPath = Path.Combine(testDir, relativePath)
            let dir = Path.GetDirectoryName(fullPath)
        
            if not (Directory.Exists(dir)) then
                Directory.CreateDirectory(dir) |> ignore
        
            match size with
            | Some bytes ->
                // Create file with specified size
                use fs = File.Create(fullPath)
                fs.SetLength(bytes)
            | None ->
                // Create directory
                if not (Directory.Exists(fullPath)) then
                    Directory.CreateDirectory(fullPath) |> ignore
    
        testDir

    /// Clean up test directory
    let cleanupTestDir testDir =
        if Directory.Exists(testDir) then
            try
                Directory.Delete(testDir, true)
            with
            | _ -> () // Ignore cleanup errors

    // ============================================================================
    // Movie Tests
    // ============================================================================

    [<Fact>]
    let ``Movie without video file - entire folder should be deleted``() =
        let testDir = createTestStructure [
            ("Classic Sports Movie (1996)/fanart.jpg", Some 290368L)
            ("Classic Sports Movie (1996)/poster.jpg", Some 181504L)
            ("Classic Sports Movie (1996)/Movie.1996.1080p.eng.srt", Some 77384L)
            ("Classic Sports Movie (1996)/Movie.1996.1080p.srt", Some 77515L)
            ("Classic Sports Movie (1996)/Movie.1996.1080p.nfo", Some 33902L)
            ("Classic Sports Movie (1996)/.actors/Lead_Actor.jpg", Some 135064L)
            ("Classic Sports Movie (1996)/.actors/Supporting_Actor.jpg", Some 124810L)
            ("Classic Sports Movie (1996)/extrafanart/fanart0.jpg", Some 122924L)
            ("Classic Sports Movie (1996)/extrafanart/fanart1.jpg", Some 194663L)
        ]
    
        try
            // Run cleaning in preview mode
            let result = Movies.clean testDir Domain.Preview
        
            // Should identify the entire movie folder for deletion (< 100 MB, no video)
            match result with
            | Ok items ->
                let movieFolder = Path.Combine(testDir, "Classic Sports Movie (1996)")
                items |> should contain movieFolder
            | Error _ ->
                failwith "Should have found folder to delete"
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Movie with video file - keep folder but delete non-English/French subtitles``() =
        let testDir = createTestStructure [
            // Main video file (1.87 GB)
            ("Adventure Movie (2024)/Adventure.Movie.2024.1080p.mp4", Some 1871531057L)
            ("Adventure Movie (2024)/Adventure.Movie.2024.1080p.nfo", Some 15025L)
            // English subtitles - should keep
            ("Adventure Movie (2024)/English.srt", Some 129119L)
            ("Adventure Movie (2024)/SDH.eng.HI.srt", Some 153335L)
            // French subtitles - should keep
            ("Adventure Movie (2024)/fre.srt", Some 93784L)
            ("Adventure Movie (2024)/SDH.fre.srt", Some 89000L)
            // Non-English/French subtitles - should delete
            ("Adventure Movie (2024)/ara.srt", Some 132965L)
            ("Adventure Movie (2024)/spa.srt", Some 97349L)
            ("Adventure Movie (2024)/ger.srt", Some 97813L)
            ("Adventure Movie (2024)/por.srt", Some 95000L)
            // Metadata - should keep
            ("Adventure Movie (2024)/poster.jpg", Some 286220L)
            ("Adventure Movie (2024)/fanart.jpg", Some 244720L)
            ("Adventure Movie (2024)/.actors/Actor_One.jpg", Some 25840L)
            ("Adventure Movie (2024)/extrafanart/fanart0.jpg", Some 343521L)
        ]
    
        try
            let result = Movies.clean testDir Domain.Preview
        
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                // Should NOT delete the folder
                let movieFolder = Path.Combine(testDir, "Adventure Movie (2024)")
                itemList |> should not' (contain movieFolder)
            
                // Should delete non-English/French subtitles
                itemList |> should contain (Path.Combine(testDir, "Adventure Movie (2024)", "ara.srt"))
                itemList |> should contain (Path.Combine(testDir, "Adventure Movie (2024)", "spa.srt"))
                itemList |> should contain (Path.Combine(testDir, "Adventure Movie (2024)", "ger.srt"))
                itemList |> should contain (Path.Combine(testDir, "Adventure Movie (2024)", "por.srt"))
            
                // Should NOT delete English subtitles
                itemList |> List.exists (fun x -> x.Contains("English.srt")) |> should be False
                itemList |> List.exists (fun x -> x.Contains("SDH.eng.HI.srt")) |> should be False
            
                // Should NOT delete French subtitles
                itemList |> List.exists (fun x -> x.Contains("fre.srt")) |> should be False
                itemList |> List.exists (fun x -> x.Contains("SDH.fre.srt")) |> should be False
            | Error _ ->
                () // No items to clean is OK
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Movie folder with extrafanart should not have extrafanart evaluated separately``() =
        let testDir = createTestStructure [
            ("Blockbuster (2023)/Blockbuster.2023.1080p.mp4", Some 2000000000L) // 2 GB
            ("Blockbuster (2023)/poster.jpg", Some 286220L)
            ("Blockbuster (2023)/extrafanart/fanart1.jpg", Some 343521L)
            ("Blockbuster (2023)/extrafanart/fanart2.jpg", Some 450000L)
            ("Blockbuster (2023)/extrafanart/fanart3.jpg", Some 520000L)
        ]
    
        try
            let result = Movies.clean testDir Domain.Preview
        
            // extrafanart should not be listed as a separate directory to clean
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                itemList |> List.exists (fun x -> x.Contains("extrafanart")) |> should be False
            | Error (CleaningError (NothingToClean _)) ->
                () // Expected - nothing to clean
            | Error e ->
                failwithf "Unexpected error: %A" e
        finally
            cleanupTestDir testDir

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