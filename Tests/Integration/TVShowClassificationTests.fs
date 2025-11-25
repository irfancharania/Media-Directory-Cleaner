namespace MediaDirectoryCleaner.Tests.Integration

open System.IO
open Xunit
open Swensen.Unquote
open TestHelpers

/// Tests for TV show folder classification (season folders vs show root folders)
module TVShowClassificationTests =

    // ============================================================================
    // Show Root Folder Detection (tvshow.nfo presence)
    // ============================================================================

    [<Fact>]
    let ``Folder with tvshow.nfo is detected as show root``() =
        let structure = [
            ("Show/tvshow.nfo", Some 1000L)
            ("Show/poster.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            test <@ TVShows.isShowRootFolder showPath @>
        )

    [<Fact>]
    let ``Folder without tvshow.nfo is not show root``() =
        let structure = [
            ("Show/Season 01/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let seasonPath = Path.Combine(testDir, "Show", "Season 01")
            test <@ not (TVShows.isShowRootFolder seasonPath) @>
        )

    [<Fact>]
    let ``Empty folder is not show root``() =
        withTestDir [] (fun testDir ->
            test <@ not (TVShows.isShowRootFolder testDir) @>
        )

    [<Fact>]
    let ``Folder with only tvshow.nfo is show root``() =
        let structure = [
            ("Show/tvshow.nfo", Some 500L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            test <@ TVShows.isShowRootFolder showPath @>
        )

    [<Fact>]
    let ``Case sensitivity of tvshow.nfo filename``() =
        let structure = [
            ("Show/TVSHOW.NFO", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            // File.Exists is case-insensitive on Windows, case-sensitive on Linux
            // This test documents the behavior
            let isRoot = TVShows.isShowRootFolder showPath
            // On Windows: true, On Linux: false
            test <@ isRoot = File.Exists(Path.Combine(showPath, "tvshow.nfo")) @>
        )

    [<Fact>]
    let ``Non-existent path is not show root``() =
        test <@ not (TVShows.isShowRootFolder "V:\\NonExistent\\Path") @>

    [<Fact>]
    let ``Season folder with tvshow.nfo is show root``() =
        // Edge case: someone might put tvshow.nfo in wrong place
        let structure = [
            ("Show/Season 01/tvshow.nfo", Some 1000L)
            ("Show/Season 01/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let seasonPath = Path.Combine(testDir, "Show", "Season 01")
            test <@ TVShows.isShowRootFolder seasonPath @>
        )

    // ============================================================================
    // TV Directory Classification
    // ============================================================================

    [<Fact>]
    let ``classifyTVDirectory identifies show root folder``() =
        let structure = [
            ("Show/tvshow.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            let classified = TVShows.classifyTVDirectory showPath
            
            match classified with
            | TVShows.ShowFolderWithoutSeasonsPath path -> 
                test <@ path = showPath @>
            | _ -> 
                failwith "Expected ShowFolderWithoutSeasons"
        )

    [<Fact>]
    let ``classifyTVDirectory identifies season folder``() =
        let structure = [
            ("Show/Season 01/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let seasonPath = Path.Combine(testDir, "Show", "Season 01")
            let classified = TVShows.classifyTVDirectory seasonPath
            
            match classified with
            | TVShows.SeasonFolderPath path -> 
                test <@ path = seasonPath @>
            | _ -> 
                failwith "Expected SeasonFolder"
        )

    // ============================================================================
    // separateTVDirectories Integration
    // ============================================================================

    [<Fact>]
    let ``separateTVDirectories correctly separates mixed directories``() =
        let structure = [
            ("Show1/tvshow.nfo", Some 1000L)
            ("Show2/Season 01/episode.mkv", Some 600000000L)
            ("Show3/tvshow.nfo", Some 1000L)
            ("Show3/poster.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let show1 = Path.Combine(testDir, "Show1")
            let season = Path.Combine(testDir, "Show2", "Season 01")
            let show3 = Path.Combine(testDir, "Show3")
            
            let paths = [show1; season; show3]
            let result = TVShows.classifyLeafDirectories paths
            
            test <@ result.SeasonFolders = [season] @>
            test <@ result.ShowFoldersWithoutSeasons |> List.contains show1 @>
            test <@ result.ShowFoldersWithoutSeasons |> List.contains show3 @>
            test <@ result.ShowFoldersWithoutSeasons.Length = 2 @>
        )

    [<Fact>]
    let ``separateTVDirectories with all season folders``() =
        let structure = [
            ("Show/Season 01/episode.mkv", Some 600000000L)
            ("Show/Season 02/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let s1 = Path.Combine(testDir, "Show", "Season 01")
            let s2 = Path.Combine(testDir, "Show", "Season 02")
            
            let result = TVShows.classifyLeafDirectories [s1; s2]
            
            test <@ result.SeasonFolders.Length = 2 @>
            test <@ result.ShowFoldersWithoutSeasons.Length = 0 @>
        )

    [<Fact>]
    let ``separateTVDirectories with all show root folders``() =
        let structure = [
            ("Show1/tvshow.nfo", Some 1000L)
            ("Show2/tvshow.nfo", Some 1000L)
            ("Show3/tvshow.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let show1 = Path.Combine(testDir, "Show1")
            let show2 = Path.Combine(testDir, "Show2")
            let show3 = Path.Combine(testDir, "Show3")
            
            let result = TVShows.classifyLeafDirectories [show1; show2; show3]
            
            test <@ result.SeasonFolders.Length = 0 @>
            test <@ result.ShowFoldersWithoutSeasons.Length = 3 @>
        )

    [<Fact>]
    let ``separateTVDirectories with empty input``() =
        let result = TVShows.classifyLeafDirectories []
        
        test <@ result.SeasonFolders.Length = 0 @>
        test <@ result.ShowFoldersWithoutSeasons.Length = 0 @>

    [<Fact>]
    let ``separateTVDirectories preserves order of season folders``() =
        let structure = [
            ("Show/Season 03/episode.mkv", Some 600000000L)
            ("Show/Season 01/episode.mkv", Some 600000000L)
            ("Show/Season 02/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let s3 = Path.Combine(testDir, "Show", "Season 03")
            let s1 = Path.Combine(testDir, "Show", "Season 01")
            let s2 = Path.Combine(testDir, "Show", "Season 02")
            
            // Input order: S3, S1, S2
            let result = TVShows.classifyLeafDirectories [s3; s1; s2]
            
            // Output should preserve input order
            test <@ result.SeasonFolders.[0] = s3 @>
            test <@ result.SeasonFolders.[1] = s1 @>
            test <@ result.SeasonFolders.[2] = s2 @>
        )

    // ============================================================================
    // Real-World Scenario Tests
    // ============================================================================

    [<Fact>]
    let ``Show folder without seasons is flagged for manual review``() =
        // This happens when all season folders were deleted (empty)
        // Should be reported to user for manual processing
        let structure = [
            ("Show Name/tvshow.nfo", Some 1000L)
            ("Show Name/poster.jpg", Some 50000L)
            ("Show Name/banner.jpg", Some 30000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show Name")
            test <@ TVShows.isShowRootFolder showPath @>
            
            let classified = TVShows.classifyTVDirectory showPath
            match classified with
            | TVShows.ShowFolderWithoutSeasonsPath path -> 
                // This is the expected classification
                test <@ path = showPath @>
            | _ -> 
                failwith "Should be classified as ShowFolderWithoutSeasons for manual review"
        )

    [<Fact>]
    let ``Show with mixed valid and invalid structure``() =
        // Show root exists but seasons were deleted - needs manual review
        // This folder should be separated out and reported
        let structure = [
            ("Show With No Seasons/tvshow.nfo", Some 1000L)
            ("Show With No Seasons/poster.jpg", Some 50000L)
            ("Normal Show/Season 01/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let problematic = Path.Combine(testDir, "Show With No Seasons")
            let normal = Path.Combine(testDir, "Normal Show", "Season 01")
            
            let result = TVShows.classifyLeafDirectories [problematic; normal]
            
            // Problematic show should be in ShowFoldersWithoutSeasons for reporting
            test <@ result.ShowFoldersWithoutSeasons = [problematic] @>
            test <@ result.SeasonFolders = [normal] @>
        )

    [<Fact>]
    let ``Standard TV show with seasons structure``() =
        let structure = [
            ("Standard Show/tvshow.nfo", Some 1000L)
            ("Standard Show/Season 01/episode.mkv", Some 600000000L)
            ("Standard Show/Season 02/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showRoot = Path.Combine(testDir, "Standard Show")
            let s1 = Path.Combine(testDir, "Standard Show", "Season 01")
            let s2 = Path.Combine(testDir, "Standard Show", "Season 02")
            
            // Show root has tvshow.nfo
            test <@ TVShows.isShowRootFolder showRoot @>
            
            // Seasons don't have tvshow.nfo
            test <@ not (TVShows.isShowRootFolder s1) @>
            test <@ not (TVShows.isShowRootFolder s2) @>
            
            // When processing leaf nodes, we'd only get season folders
            // (show root is not a leaf if it has subdirectories)
            let result = TVShows.classifyLeafDirectories [s1; s2]
            test <@ result.ShowFoldersWithoutSeasons.Length = 0 @>
            test <@ result.SeasonFolders.Length = 2 @>
        )

    [<Fact>]
    let ``Show root becomes leaf after seasons deleted``() =
        // After all season folders are deleted, the show root becomes a leaf node
        // This should be detected and reported for manual review
        let structure = [
            ("Cleaned Show/tvshow.nfo", Some 1000L)
            ("Cleaned Show/poster.jpg", Some 50000L)
            // No season folders - they were all deleted previously
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Cleaned Show")
            
            // This is now a leaf node (no subdirectories)
            test <@ TVShows.isShowRootFolder showPath @>
            
            let result = TVShows.classifyLeafDirectories [showPath]
            
            // Should be flagged for manual review
            test <@ result.ShowFoldersWithoutSeasons = [showPath] @>
            test <@ result.SeasonFolders.Length = 0 @>
        )

    [<Fact>]
    let ``Multiple shows without seasons all flagged``() =
        // Multiple shows that had their seasons deleted
        // All should be reported for manual review
        let structure = [
            ("Show A/tvshow.nfo", Some 1000L)
            ("Show A/poster.jpg", Some 50000L)
            ("Show B/tvshow.nfo", Some 1000L)
            ("Show B/banner.jpg", Some 30000L)
            ("Show C/tvshow.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showA = Path.Combine(testDir, "Show A")
            let showB = Path.Combine(testDir, "Show B")
            let showC = Path.Combine(testDir, "Show C")
            
            let result = TVShows.classifyLeafDirectories [showA; showB; showC]
            
            // All three should be flagged
            test <@ result.ShowFoldersWithoutSeasons.Length = 3 @>
            test <@ result.ShowFoldersWithoutSeasons |> List.contains showA @>
            test <@ result.ShowFoldersWithoutSeasons |> List.contains showB @>
            test <@ result.ShowFoldersWithoutSeasons |> List.contains showC @>
            test <@ result.SeasonFolders.Length = 0 @>
        )

    // ============================================================================
    // Edge Cases and Error Handling
    // ============================================================================

    [<Fact>]
    let ``isShowRootFolder handles path with special characters``() =
        let structure = [
            ("Show's Title!/tvshow.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show's Title!")
            test <@ TVShows.isShowRootFolder showPath @>
        )

    [<Fact>]
    let ``isShowRootFolder handles very long path``() =
        let longName = System.String('a', 100)
        let structure = [
            ($"{longName}/tvshow.nfo", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, longName)
            test <@ TVShows.isShowRootFolder showPath @>
        )

    [<Fact>]
    let ``isShowRootFolder returns false for file path not directory``() =
        let structure = [
            ("Show/episode.mkv", Some 600000000L)
        ]
        
        withTestDir structure (fun testDir ->
            let filePath = Path.Combine(testDir, "Show", "episode.mkv")
            test <@ not (TVShows.isShowRootFolder filePath) @>
        )

    [<Fact>]
    let ``Folder with similarly named file is not show root``() =
        let structure = [
            ("Show/tvshow.txt", Some 1000L)
            ("Show/poster.jpg", Some 50000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            test <@ not (TVShows.isShowRootFolder showPath) @>
        )

    [<Fact>]
    let ``Folder with tvshow.nfo.bak is not show root``() =
        let structure = [
            ("Show/tvshow.nfo.bak", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let showPath = Path.Combine(testDir, "Show")
            test <@ not (TVShows.isShowRootFolder showPath) @>
        )