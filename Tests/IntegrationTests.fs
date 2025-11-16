module IntegrationTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Domain

// ============================================================================
// Test Helpers
// ============================================================================

/// Create a temporary test directory structure
let createTestStructure basePath (structure: (string * int64 option) list) =
    for (path, size) in structure do
        let fullPath = Path.Combine(basePath, path)
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

/// Get all files and directories under a path
let getAllItems basePath =
    let dirs = 
        if Directory.Exists(basePath) then
            Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories)
            |> Array.toList
        else []
    
    let files = 
        if Directory.Exists(basePath) then
            Directory.GetFiles(basePath, "*", SearchOption.AllDirectories)
            |> Array.toList
        else []
    
    (dirs, files)

// ============================================================================
// Movie Tests
// ============================================================================

let movieTests =
    testList "Movie Integration Tests" [
        
        test "Movie without video file - entire folder should be deleted" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                // Structure based on Space Jam example - movie with no video file
                createTestStructure testDir [
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
                
                // Run cleaning in preview mode
                let result = Movies.clean testDir Domain.Preview
                
                // Should identify the entire movie folder for deletion (< 100 MB, no video)
                match result with
                | Ok items ->
                    let movieFolder = Path.Combine(testDir, "Classic Sports Movie (1996)")
                    items |> Expect.contains "" movieFolder
                | Error _ ->
                    failtest "Should have found folder to delete"
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
        
        test "Movie with video file - keep folder but delete non-English subtitles" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                // Structure based on Playdate example - movie with video file
                createTestStructure testDir [
                    // Main video file (1.87 GB)
                    ("Comedy Movie (2025)/Comedy.Movie.2025.1080p.mp4", Some 1871531057L)
                    ("Comedy Movie (2025)/Comedy.Movie.2025.1080p.nfo", Some 15025L)
                    // English subtitles - should keep
                    ("Comedy Movie (2025)/English.srt", Some 129119L)
                    ("Comedy Movie (2025)/SDH.eng.HI.srt", Some 153335L)
                    // Non-English subtitles - should delete
                    ("Comedy Movie (2025)/ara.srt", Some 132965L)
                    ("Comedy Movie (2025)/spa.srt", Some 97349L)
                    ("Comedy Movie (2025)/fre.srt", Some 93784L)
                    ("Comedy Movie (2025)/ger.srt", Some 97813L)
                    // Metadata - should keep
                    ("Comedy Movie (2025)/poster.jpg", Some 286220L)
                    ("Comedy Movie (2025)/fanart.jpg", Some 244720L)
                    ("Comedy Movie (2025)/.actors/Actor_One.jpg", Some 25840L)
                    ("Comedy Movie (2025)/extrafanart/fanart0.jpg", Some 343521L)
                ]
                
                let result = Movies.clean testDir Domain.Preview
                
                match result with
                | Ok items ->
                    let itemList = items |> Seq.toList
                    // Should NOT delete the folder
                    let movieFolder = Path.Combine(testDir, "Comedy Movie (2025)")

                    Expect.isFalse "" (itemList |> List.contains movieFolder)
                    
                    // Should delete non-English subtitles
                    itemList |> Expect.contains "" (Path.Combine(testDir, "Comedy Movie (2025)/ara.srt"))
                    itemList |> Expect.contains "" (Path.Combine(testDir, "Comedy Movie (2025)/spa.srt"))
                    itemList |> Expect.contains "" (Path.Combine(testDir, "Comedy Movie (2025)/fre.srt"))
                    
                    // Should NOT delete English subtitles
                    Expect.isFalse "" (itemList |> List.exists (fun x -> x.Contains("English.srt")))
                    Expect.isFalse "" (itemList |> List.exists (fun x -> x.Contains("SDH.eng.HI.srt")))
                | Error _ ->
                    () // No items to clean is OK
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
    ]

// ============================================================================
// TV Show Tests
// ============================================================================

let tvShowTests =
    testList "TV Show Integration Tests" [
        
        test "TV show with valid season - keep everything" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                // Structure based on Pluribus example - valid TV show
                createTestStructure testDir [
                    ("Drama Series/banner.jpg", Some 18811L)
                    ("Drama Series/fanart.jpg", Some 125482L)
                    ("Drama Series/folder.jpg", Some 63078L)
                    ("Drama Series/poster.jpg", Some 63078L)
                    ("Drama Series/tvshow.nfo", Some 1790L)
                    ("Drama Series/.actors/Lead_Actress.jpg", Some 29347L)
                    ("Drama Series/.actors/Supporting_Actress.jpg", Some 33759L)
                    // Season 1 with video files (664 MB and 730 MB)
                    ("Drama Series/Season 01/Drama.S01E01.Episode One.mkv", Some 664624081L)
                    ("Drama Series/Season 01/Drama.S01E01.Episode One.eng.srt", Some 36816L)
                    ("Drama Series/Season 01/Drama.S01E01.Episode One.nfo", Some 2424L)
                    ("Drama Series/Season 01/Drama.S01E01.Episode One-thumb.jpg", Some 37050L)
                    ("Drama Series/Season 01/Drama.S01E02.Episode Two.mkv", Some 730031885L)
                    ("Drama Series/Season 01/Drama.S01E02.Episode Two.eng.srt", Some 38139L)
                    ("Drama Series/Season 01/Drama.S01E02.Episode Two.nfo", Some 2604L)
                    ("Drama Series/Season 01/Drama.S01E02.Episode Two-thumb.jpg", Some 42952L)
                ]
                
                let result = TVShows.clean testDir Domain.Preview
                
                // Should find nothing to delete (or only orphaned files if any)
                match result with
                | Ok items ->
                    let itemList = items |> Seq.toList
                    // Season folder should NOT be in the list (has video files)
                    let season1 = Path.Combine(testDir, "Drama Series/Season 01")
                    Expect.isFalse "" (itemList |> List.contains season1)
                | Error (DirectoryError (NoLeafNodes _)) 
                | Error (CleaningError (NothingToClean _)) ->
                    () // Expected - nothing to clean
                | Error e ->
                    failtestf "Unexpected error: %A" e
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
        
        test "Empty season folder should be deleted" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                createTestStructure testDir [
                    ("Mystery Show/banner.jpg", Some 18811L)
                    ("Mystery Show/poster.jpg", Some 63078L)
                    // Empty season folder with only metadata
                    ("Mystery Show/Season 02/episode.nfo", Some 2424L)
                    ("Mystery Show/Season 02/thumb.jpg", Some 37050L)
                    // No video files!
                ]
                
                let result = TVShows.clean testDir Domain.Preview
                
                match result with
                | Ok items ->
                    // Season folder should be marked for deletion
                    let season2 = Path.Combine(testDir, "Mystery Show/Season 02")
                    items |> Expect.contains "" season2
                | Error _ ->
                    failtest "Should have found empty season to delete"
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
        
        test "Orphaned metadata files should be deleted" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                createTestStructure testDir [
                    ("SciFi Show/poster.jpg", Some 63078L)
                    ("SciFi Show/.actors/Actor_Name.jpg", Some 29347L)
                    // Video file exists for episode 1
                    ("SciFi Show/Season 01/SciFi.S01E01.mkv", Some 664624081L)
                    ("SciFi Show/Season 01/SciFi.S01E01.eng.srt", Some 36816L)
                    ("SciFi Show/Season 01/SciFi.S01E01.nfo", Some 2424L)
                    ("SciFi Show/Season 01/SciFi.S01E01-thumb.jpg", Some 37050L)
                    // Orphaned metadata for episode 2 (no video file!)
                    ("SciFi Show/Season 01/SciFi.S01E02.eng.srt", Some 38139L)
                    ("SciFi Show/Season 01/SciFi.S01E02.nfo", Some 2604L)
                    ("SciFi Show/Season 01/SciFi.S01E02-thumb.jpg", Some 42952L)
                    // Orphaned metadata for episode 3 (no video file!)
                    ("SciFi Show/Season 01/SciFi.S01E03.eng.srt", Some 35000L)
                    ("SciFi Show/Season 01/SciFi.S01E03.nfo", Some 2500L)
                ]
                
                let result = TVShows.clean testDir Domain.Preview
                
                match result with
                | Ok items ->
                    let itemList = items |> Seq.toList
                    // Should delete orphaned metadata for episode 2
                    items |> Expect.contains "" (Path.Combine(testDir, "SciFi Show/Season 01/SciFi.S01E02.eng.srt"))
                    items |> Expect.contains "" (Path.Combine(testDir, "SciFi Show/Season 01/SciFi.S01E02.nfo"))
                    items |> Expect.contains "" (Path.Combine(testDir, "SciFi Show/Season 01/SciFi.S01E02-thumb.jpg"))
                    
                    // Should delete orphaned metadata for episode 3
                    items |> Expect.contains "" (Path.Combine(testDir, "SciFi Show/Season 01/SciFi.S01E03.eng.srt"))
                    items |> Expect.contains "" (Path.Combine(testDir, "SciFi Show/Season 01/SciFi.S01E03.nfo"))
                    
                    // Should NOT delete season folder (has video for episode 1)
                    let season1 = Path.Combine(testDir, "SciFi Show/Season 01")
                    Expect.isFalse "" (itemList |> List.contains season1)
                    
                    // Should NOT delete matched metadata for episode 1
                    Expect.isFalse "" (itemList |> List.exists (fun x -> x.Contains("SciFi.S01E01")))
                | Error _ ->
                    failtest "Should have found orphaned metadata files"
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
        
        test ".actors folder should not be processed separately" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                createTestStructure testDir [
                    ("Action Show/poster.jpg", Some 63078L)
                    ("Action Show/.actors/actor1.jpg", Some 29347L)
                    ("Action Show/.actors/actor2.jpg", Some 33759L)
                    // Season with video
                    ("Action Show/Season 01/Action.S01E01.mkv", Some 664624081L)
                ]
                
                let result = TVShows.clean testDir Domain.Preview
                
                // .actors files should not be in the results
                match result with
                | Ok items ->
                    let itemList = items |> Seq.toList
                    Expect.isFalse "" (itemList |> List.exists (fun x -> x.Contains(".actors")))
                | Error (CleaningError (NothingToClean _)) ->
                    () // Expected - nothing to clean
                | Error e ->
                    failtestf "Unexpected error: %A" e
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
    ]

// ============================================================================
// Music Tests
// ============================================================================

let musicTests =
    testList "Music Integration Tests" [
        
        test "Empty album folder should be deleted" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                createTestStructure testDir [
                    ("Rock Artist/Empty Album/cover.jpg", Some 50000L)
                    ("Rock Artist/Empty Album/info.nfo", Some 1000L)
                    // No audio files!
                ]
                
                let result = Music.clean testDir Domain.Preview
                
                match result with
                | Ok items ->
                    // Album should be marked for deletion
                    let album = Path.Combine(testDir, "Rock Artist/Empty Album")
                    items |> Expect.contains "" album
                | Error _ ->
                    failtest "Should have found empty album"
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
        
        test "Album with audio files should be kept" {
            let testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            
            try
                createTestStructure testDir [
                    ("Jazz Artist/Good Album/track01.mp3", Some 5000000L) // 5 MB
                    ("Jazz Artist/Good Album/track02.mp3", Some 4500000L)
                    ("Jazz Artist/Good Album/cover.jpg", Some 50000L)
                ]
                
                let result = Music.clean testDir Domain.Preview
                
                match result with
                | Ok items ->
                    // Album should NOT be in the list
                    let album = Path.Combine(testDir, "Jazz Artist/Good Album")
                    Expect.isFalse "" (items |> Seq.contains album)
                | Error (CleaningError (NothingToClean _)) ->
                    () // Expected - nothing to clean
                | Error e ->
                    failtestf "Unexpected error: %A" e
            finally
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
        }
    ] 
 
// ============================================================================
// All Tests
// ============================================================================

[<Tests>]
let tests =
    testList "Integration Tests" [
        movieTests
        tvShowTests
        musicTests
    ]