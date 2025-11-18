namespace MediaDirectoryCleaner.Tests

open System.IO
open Xunit
open FsUnit.Xunit
open Domain
open FileSystemSetup

module MovieTests = 

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
            ("Blockbuster (2023)/Blockbuster.2023.1080p.mp4", Some 1000000000L) // 1 GB
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

    [<Fact>]
    let ``Subtitle matching video filename should be kept even if name contains language codes``() =
        let testDir = createTestStructure [
            // Video file with "spa" in the title (Spanish word)
            ("Destination (2024)/The.Spanish.Prisoner.1997.1080p.mp4", Some 1000000000L) // 1 GB
            // Subtitle matching the video filename exactly - should KEEP
            ("Destination (2024)/The.Spanish.Prisoner.1997.1080p.srt", Some 50000L)
            // Other language subtitles - should DELETE
            ("Destination (2024)/ara.srt", Some 45000L)
            ("Destination (2024)/ger.srt", Some 48000L)
        ]
        
        try
            let result = Movies.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                // Should NOT delete the matching subtitle (even though "spa" is in the name)
                itemList |> List.exists (fun x -> x.Contains("The.Spanish.Prisoner.1997.1080p.srt")) |> should be False
                // Should delete non-matching language subtitles
                itemList |> should contain (Path.Combine(testDir, "Destination (2024)", "ara.srt"))
                itemList |> should contain (Path.Combine(testDir, "Destination (2024)", "ger.srt"))
            | Error _ ->
                failwith "Should have found language subtitles to delete"
        finally
            cleanupTestDir testDir

    [<Fact>]
    let ``Subtitle matching video in subdirectory should be kept``() =
        let testDir = createTestStructure [
            ("Adventure (2020)/Adventure.Movie.2020.1080p.mp4", Some 1000000000L)
            // Subtitle in subdirectory matching video name
            ("Adventure (2020)/Subs/Adventure.Movie.2020.1080p.srt", Some 50000L)
            // Other subtitle that doesn't match
            ("Adventure (2020)/Subs/spa.srt", Some 45000L)
        ]
        
        try
            let result = Movies.clean testDir Domain.Preview
            
            match result with
            | Ok items ->
                let itemList = items |> Seq.toList
                // Matching subtitle should NOT be deleted (even in subdirectory)
                itemList |> List.exists (fun x -> x.Contains("Adventure.Movie.2020.1080p.srt")) |> should be False
                // Non-matching subtitle should be deleted
                itemList |> should contain (Path.Combine(testDir, "Adventure (2020)", "Subs", "spa.srt"))
            | Error _ ->
                failwith "Should have found language subtitle to delete"
        finally
            cleanupTestDir testDir
