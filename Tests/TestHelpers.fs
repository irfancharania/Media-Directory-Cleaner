namespace MediaDirectoryCleaner.Tests

open System
open System.IO
open Domain

/// Common test utilities and helpers
module TestHelpers =
    
    // ============================================================================
    // File System Test Fixtures
    // ============================================================================
    
    /// Create a temporary test directory structure
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
    
    /// Execute test with automatic cleanup
    let withTestDir structure testFn =
        let testDir = createTestStructure structure
        try
            testFn testDir
        finally
            cleanupTestDir testDir
    
    // ============================================================================
    // Common Test File Structures
    // ============================================================================
    
    /// Movie with no video file (orphaned metadata only)
    let movieWithoutVideo movieName = [
        ($"{movieName}/fanart.jpg", Some 290368L)
        ($"{movieName}/poster.jpg", Some 181504L)
        ($"{movieName}/movie.nfo", Some 33902L)
        ($"{movieName}/.actors/Actor1.jpg", Some 135064L)
        ($"{movieName}/extrafanart/fanart0.jpg", Some 122924L)
    ]
    
    /// Movie with video file and various subtitles
    let movieWithVideoAndSubtitles movieName videoName = [
        // Main video (870 MB)
        ($"{movieName}/{videoName}.mp4", Some 871531057L)
        ($"{movieName}/{videoName}.nfo", Some 15025L)
        // English subtitles
        ($"{movieName}/English.srt", Some 129119L)
        ($"{movieName}/SDH.eng.HI.srt", Some 153335L)
        // French subtitles
        ($"{movieName}/fre.srt", Some 93784L)
        ($"{movieName}/SDH.fre.srt", Some 89000L)
        // Non-English/French subtitles
        ($"{movieName}/ara.srt", Some 132965L)
        ($"{movieName}/spa.srt", Some 97349L)
        ($"{movieName}/ger.srt", Some 97813L)
        ($"{movieName}/por.srt", Some 95000L)
        // Metadata
        ($"{movieName}/poster.jpg", Some 286220L)
        ($"{movieName}/fanart.jpg", Some 244720L)
        ($"{movieName}/.actors/Actor1.jpg", Some 25840L)
        ($"{movieName}/extrafanart/fanart0.jpg", Some 343521L)
    ]
    
    /// TV show season with episodes
    let tvSeasonWithEpisodes showName seasonName episodes = [
        yield! [
            ($"{showName}/banner.jpg", Some 18811L)
            ($"{showName}/fanart.jpg", Some 125482L)
            ($"{showName}/poster.jpg", Some 63078L)
            ($"{showName}/tvshow.nfo", Some 1790L)
            ($"{showName}/.actors/Actor1.jpg", Some 29347L)
        ]
        
        for (episodeNum, hasVideo) in episodes do
            let episodeName = $"Show.S01E{episodeNum:D2}"
            if hasVideo then
                yield ($"{showName}/{seasonName}/{episodeName}.mkv", Some 664624081L)
            yield ($"{showName}/{seasonName}/{episodeName}.eng.srt", Some 36816L)
            yield ($"{showName}/{seasonName}/{episodeName}.nfo", Some 2424L)
            yield ($"{showName}/{seasonName}/{episodeName}-thumb.jpg", Some 37050L)
    ]
    
    /// Music album structure
    let musicAlbum artistName albumName hasAudio = [
        if hasAudio then
            yield ($"{artistName}/{albumName}/track01.mp3", Some 5000000L)
            yield ($"{artistName}/{albumName}/track02.mp3", Some 4500000L)
        yield ($"{artistName}/{albumName}/cover.jpg", Some 50000L)
        yield ($"{artistName}/{albumName}/info.nfo", Some 1000L)
    ]
    
    // ============================================================================
    // DeletableItem Test Helpers
    // ============================================================================
    
    /// Check if a file path exists in the deletable items
    let containsFile (path: string) (items: seq<DeletableItem>) =
        items |> Seq.exists (function
            | DeletableItem.File p when p = path -> true
            | _ -> false)
    
    /// Check if a directory path exists in the deletable items
    let containsDirectory (path: string) (items: seq<DeletableItem>) =
        items |> Seq.exists (function
            | DeletableItem.Directory p when p = path -> true
            | _ -> false)
    
    /// Check if any item contains the given substring in its path
    let containsPathSubstring (substring: string) (items: seq<DeletableItem>) =
        items |> Seq.exists (fun item ->
            let path = DeletableItem.path item
            path.Contains(substring))
    
    /// Get all file paths from deletable items
    let getFiles (items: seq<DeletableItem>) =
        items |> Seq.choose (function
            | DeletableItem.File path -> Some path
            | _ -> None)
    
    /// Get all directory paths from deletable items
    let getDirectories (items: seq<DeletableItem>) =
        items |> Seq.choose (function
            | DeletableItem.Directory path -> Some path
            | _ -> None)
    
    /// Count items of each type
    let countItems (items: seq<DeletableItem>) =
        let files = items |> Seq.filter (function DeletableItem.File _ -> true | _ -> false) |> Seq.length
        let dirs = items |> Seq.filter (function DeletableItem.Directory _ -> true | _ -> false) |> Seq.length
        (files, dirs)