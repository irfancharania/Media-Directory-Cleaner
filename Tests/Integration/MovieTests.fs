namespace MediaDirectoryCleaner.Tests.Integration

open System.IO
open Xunit
open Swensen.Unquote
open Domain
open TestHelpers
open Errors

module MovieTests = 

    // ============================================================================
    // Movie Without Video Tests
    // ============================================================================

    [<Fact>]
    let ``Movie without video file - entire folder should be deleted``() =
        withTestDir (movieWithoutVideo "Classic Movie (1996)") (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
        
            match result with
            | Ok items ->
                let movieFolder = Path.Combine(testDir, "Classic Movie (1996)")
                test <@ containsDirectory movieFolder items @>
                test <@ not (containsFile movieFolder items) @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Small movie folder under 100MB threshold is deleted``() =
        let structure = [
            ("Tiny Movie/poster.jpg", Some 50000000L)
            ("Tiny Movie/fanart.jpg", Some 30000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok items ->
                let movieFolder = Path.Combine(testDir, "Tiny Movie")
                test <@ containsDirectory movieFolder items @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Extrafanart folder is not evaluated separately``() =
        let structure = [
            ("Movie/movie.mp4", Some 50000000L)
            ("Movie/extrafanart/fanart1.jpg", Some 343521L)
            ("Movie/extrafanart/fanart2.jpg", Some 450000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok items ->
                test <@ not (containsPathSubstring "extrafanart" items) @>
            | Error (CleaningError (NothingToClean _)) ->
                ()
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Subtitle Cleaning Tests
    // ============================================================================

    [<Fact>]
    let ``Movie with video - keeps folder but deletes non-English-French subtitles``() =
        withTestDir (movieWithVideoAndSubtitles "Adventure (2024)" "Adventure.2024.1080p") (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
        
            match result with
            | Ok items ->
                let movieFolder = Path.Combine(testDir, "Adventure (2024)")
                
                test <@ not (containsDirectory movieFolder items) @>
            
                let araSub = Path.Combine(testDir, "Adventure (2024)", "ara.srt")
                let spaSub = Path.Combine(testDir, "Adventure (2024)", "spa.srt")
                let gerSub = Path.Combine(testDir, "Adventure (2024)", "ger.srt")
                let porSub = Path.Combine(testDir, "Adventure (2024)", "por.srt")
                
                test <@ containsFile araSub items @>
                test <@ containsFile spaSub items @>
                test <@ containsFile gerSub items @>
                test <@ containsFile porSub items @>
            
                test <@ not (containsPathSubstring "English.srt" items) @>
                test <@ not (containsPathSubstring "SDH.eng.HI.srt" items) @>
                test <@ not (containsPathSubstring "fre.srt" items) @>
                test <@ not (containsPathSubstring "SDH.fre.srt" items) @>
                
                let files, dirs = countItems items
                test <@ files = 4 @>
                test <@ dirs = 0 @>
            | Error (CleaningError (NothingToClean _)) ->
                failwith "Should have found subtitles to delete"
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Subtitle matching video filename is kept even with language codes in name``() =
        let structure = [
            ("Movie/The.Spanish.Prisoner.1997.1080p.mp4", Some 50000000L)
            ("Movie/The.Spanish.Prisoner.1997.1080p.srt", Some 50000L)
            ("Movie/ara.srt", Some 45000L)
            ("Movie/ger.srt", Some 48000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok items ->
                test <@ not (containsPathSubstring "The.Spanish.Prisoner.1997.1080p.srt" items) @>
                
                let araSub = Path.Combine(testDir, "Movie", "ara.srt")
                let gerSub = Path.Combine(testDir, "Movie", "ger.srt")
                test <@ containsFile araSub items @>
                test <@ containsFile gerSub items @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Subtitle in subdirectory matching video name is kept``() =
        let structure = [
            ("Movie/Movie.2020.1080p.mp4", Some 1000000000L)
            ("Movie/Subs/Movie.2020.1080p.srt", Some 50000L)
            ("Movie/Subs/spa.srt", Some 45000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok items ->
                test <@ not (containsPathSubstring "Movie.2020.1080p.srt" items) @>
                
                let spaSub = Path.Combine(testDir, "Movie", "Subs", "spa.srt")
                test <@ containsFile spaSub items @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Scan All Mode Tests
    // ============================================================================

    [<Fact>]
    let ``Scan all mode bypasses optimization``() =
        let structure = [
            ("Movie/movie.mp4", Some 1000000000L)
            ("Movie/spa.srt", Some 45000L)
        ]
        
        withTestDir structure (fun testDir ->
            // First run with optimization - creates .lastrun
            let _ = Movies.clean testDir Domain.Execute Domain.Optimized
            
            // Second run with optimization - should skip (no changes)
            let resultOptimized = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match resultOptimized with
            | Error (CleaningError (NothingToClean _)) ->
                ()  // Expected - optimization skips unchanged directories
            | Ok _ ->
                failwith "Optimized mode should skip unchanged directories"
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
            
            // Run with ScanAll - should find the subtitle again
            let resultScanAll = Movies.clean testDir Domain.Preview Domain.ScanAll
            
            match resultScanAll with
            | Ok items ->
                let spaSub = Path.Combine(testDir, "Movie", "spa.srt")
                test <@ containsFile spaSub items @>
            | Error e ->
                failwithf $"ScanAll mode should bypass optimization, got: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Scan all mode shows uncertain subtitles again``() =
        let structure = [
            ("Movie/movie.mp4", Some 1000000000L)
            ("Movie/subtitle.srt", Some 45000L)  // Uncertain - no language code
        ]
        
        withTestDir structure (fun testDir ->
            // First run
            let _ = Movies.clean testDir Domain.Preview Domain.Optimized
            
            // Second run with optimization - skips unchanged
            let resultOptimized = Movies.clean testDir Domain.Preview Domain.Optimized
            match resultOptimized with
            | Error (CleaningError (NothingToClean _)) -> ()
            | _ -> ()
            
            // ScanAll should process everything again
            let resultScanAll = Movies.clean testDir Domain.Preview Domain.ScanAll
            
            // Should not error (uncertain subtitle doesn't get deleted)
            match resultScanAll with
            | Ok _ -> ()  // No items to delete, but processed
            | Error (CleaningError (NothingToClean _)) -> ()  // Also valid
            | Error e -> failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Mixed Scenarios
    // ============================================================================

    [<Fact>]
    let ``Multiple small folders and unwanted subtitles in large folders``() =
        let structure = [
            ("Small Movie/poster.jpg", Some 50000L)
            ("Big Movie/movie.mp4", Some 1000000000L)
            ("Big Movie/eng.srt", Some 50000L)
            ("Big Movie/spa.srt", Some 48000L)
            ("Big Movie/ger.srt", Some 47000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok items ->
                let smallFolder = Path.Combine(testDir, "Small Movie")
                test <@ containsDirectory smallFolder items @>
                
                let bigFolder = Path.Combine(testDir, "Big Movie")
                test <@ not (containsDirectory bigFolder items) @>
                
                let spaSub = Path.Combine(testDir, "Big Movie", "spa.srt")
                let gerSub = Path.Combine(testDir, "Big Movie", "ger.srt")
                test <@ containsFile spaSub items @>
                test <@ containsFile gerSub items @>
                
                test <@ not (containsPathSubstring "eng.srt" items) @>
                
                let files, dirs = countItems items
                test <@ files = 2 @>
                test <@ dirs = 1 @>
            | Error e ->
                failwithf $"Expected Ok with items, got: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Execute Mode Tests
    // ============================================================================

    [<Fact>]
    let ``Execute mode actually deletes items``() =
        let structure = [
            ("DeleteMe/poster.jpg", Some 50000L)
            ("DeleteMe/fanart.jpg", Some 60000L)
        ]
        
        withTestDir structure (fun testDir ->
            let movieFolder = Path.Combine(testDir, "DeleteMe")
            
            test <@ Directory.Exists(movieFolder) @>
            
            let result = Movies.clean testDir Domain.Execute Domain.Optimized
            
            match result with
            | Ok items ->
                test <@ containsDirectory movieFolder items @>
                test <@ not (Directory.Exists(movieFolder)) @>
            | Error e ->
                failwithf $"Unexpected error: {DomainError.toMessage e}"
        )

    // ============================================================================
    // Error Cases
    // ============================================================================

    [<Fact>]
    let ``No items to clean returns appropriate error``() =
        let structure = [
            ("Good Movie/movie.mp4", Some 1000000000L)
            ("Good Movie/eng.srt", Some 50000L)
            ("Good Movie/fre.srt", Some 48000L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = Movies.clean testDir Domain.Preview Domain.Optimized
            
            match result with
            | Ok _ ->
                failwith "Should return error when nothing to clean"
            | Error (CleaningError (NothingToClean _)) ->
                ()
            | Error e ->
                failwithf $"Unexpected error type: {DomainError.toMessage e}"
        )

    [<Fact>]
    let ``Invalid path returns validation error``() =
        let result = Movies.clean "V:\\NonExistent\\Path\\12345" Domain.Preview Domain.Optimized
        
        match result with
        | Error (ValidationError (PathNotFound _)) ->
            ()
        | Error e ->
            failwithf $"Unexpected error type: {DomainError.toMessage e}"
        | Ok _ ->
            failwith "Should return error for invalid path"