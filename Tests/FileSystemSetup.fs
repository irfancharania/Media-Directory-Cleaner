namespace MediaDirectoryCleaner.Tests

open System
open System.IO

module FileSystemSetup = 

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

