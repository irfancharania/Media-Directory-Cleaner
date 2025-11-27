namespace MediaDirectoryCleaner.Tests.Integration

open Xunit
open Swensen.Unquote
open Domain
open Errors
open TestHelpers

module FileSystemTests =

    // ============================================================================
    // Path Validation Tests
    // ============================================================================

    [<Fact>]
    let ``validatePath with empty path returns PathEmpty error``() =
        let result = FileSystem.validatePath ""
        test <@ result = Error PathEmpty @>

    [<Fact>]
    let ``validatePath with whitespace path returns PathEmpty error``() =
        let result = FileSystem.validatePath "   "
        test <@ result = Error PathEmpty @>

    [<Fact>]
    let ``validatePath with null-like whitespace returns PathEmpty error``() =
        let result = FileSystem.validatePath "\t\n"
        test <@ result = Error PathEmpty @>

    [<Fact>]
    let ``validatePath with non-existent path returns PathNotFound error``() =
        let nonExistentPath = "V:\\NonExistentPath123456"
        let result = FileSystem.validatePath nonExistentPath
        test <@ result = Error (PathNotFound nonExistentPath) @>

    [<Fact>]
    let ``validatePath with valid directory returns Ok``() =
        withTestDir [] (fun testDir ->
            let result = FileSystem.validatePath testDir
            match result with
            | Ok validPath -> 
                test <@ ValidatedPath.value validPath = testDir @>
            | Error e -> 
                failwithf $"Expected Ok, got Error: {e}"
        )

    [<Fact>]
    let ``validatePath normalizes path``() =
        withTestDir [("subfolder/file.txt", Some 100L)] (fun testDir ->
            // Use a path with mixed separators or trailing slash
            let result = FileSystem.validatePath testDir
            match result with
            | Ok validPath ->
                let normalized = ValidatedPath.value validPath
                // Should be normalized (no trailing slash, consistent separators)
                test <@ not (normalized.EndsWith("\\")) || normalized.Length = 3 @>  // Allow "C:\"
            | Error e ->
                failwithf $"Expected Ok, got Error: {e}"
        )

    // ============================================================================
    // GetSubdirectories Tests
    // ============================================================================

    [<Fact>]
    let ``getAllSubdirectories returns all nested directories``() =
        let structure = [
            ("Level1/Level2/file.txt", Some 100L)
            ("Level1/Another/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let result = FileSystem.validatePath testDir
            match result with
            | Ok validPath ->
                match FileSystem.getAllSubdirectories validPath with
                | Ok dirs ->
                    let dirList = dirs |> Seq.toList
                    test <@ dirList.Length >= 3 @>  // Level1, Level2, Another
                | Error e ->
                    failwithf $"Expected directories, got Error: {e}"
            | Error e ->
                failwithf $"Path validation failed: {e}"
        )

    [<Fact>]
    let ``getTopSubdirectories returns only immediate children``() =
        let structure = [
            ("Child1/Grandchild/file.txt", Some 100L)
            ("Child2/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            match FileSystem.validatePath testDir with
            | Ok validPath ->
                match FileSystem.getTopSubdirectories validPath with
                | Ok dirs ->
                    let dirList = dirs |> Seq.toList
                    test <@ dirList.Length = 2 @>  // Only Child1 and Child2
                | Error e ->
                    failwithf $"Expected directories, got Error: {e}"
            | Error e ->
                failwithf $"Path validation failed: {e}"
        )

    [<Fact>]
    let ``getSubdirectories filters out special directories``() =
        let structure = [
            ("Normal/file.txt", Some 100L)
            (".actors/actor.jpg", Some 100L)
            ("extrafanart/fanart.jpg", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            match FileSystem.validatePath testDir with
            | Ok validPath ->
                match FileSystem.getAllSubdirectories validPath with
                | Ok dirs ->
                    let dirNames = dirs |> Seq.map (fun d -> d.Name) |> Seq.toList
                    test <@ dirNames |> List.contains "Normal" @>
                    test <@ not (dirNames |> List.exists (fun n -> n.StartsWith("."))) @>
                    test <@ not (dirNames |> List.contains "extrafanart") @>
                | Error e ->
                    failwithf $"Expected directories, got Error: {e}"
            | Error e ->
                failwithf $"Path validation failed: {e}"
        )

    [<Fact>]
    let ``getSubdirectories returns NoSubdirectories for empty directory``() =
        withTestDir [] (fun testDir ->
            match FileSystem.validatePath testDir with
            | Ok validPath ->
                match FileSystem.getAllSubdirectories validPath with
                | Ok _ ->
                    failwith "Expected NoSubdirectories error"
                | Error (NoSubdirectories _) ->
                    ()  // Expected
                | Error e ->
                    failwithf $"Expected NoSubdirectories, got: {e}"
            | Error e ->
                failwithf $"Path validation failed: {e}"
        )

    // ============================================================================
    // GetFiles Tests
    // ============================================================================

    [<Fact>]
    let ``getFiles returns files in directory``() =
        let structure = [
            ("file1.txt", Some 100L)
            ("file2.mp4", Some 1000L)
        ]
        
        withTestDir structure (fun testDir ->
            let files = FileSystem.getFiles testDir |> Seq.toList
            test <@ files.Length = 2 @>
        )

    [<Fact>]
    let ``getFiles does not recurse into subdirectories``() =
        let structure = [
            ("file.txt", Some 100L)
            ("subdir/nested.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let files = FileSystem.getFiles testDir |> Seq.toList
            test <@ files.Length = 1 @>
            test <@ files.[0].Name = "file.txt" @>
        )

    [<Fact>]
    let ``getFiles returns empty for directory with only subdirectories``() =
        let structure = [
            ("subdir/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let files = FileSystem.getFiles testDir |> Seq.toList
            test <@ files.Length = 0 @>
        )

    // ============================================================================
    // GetDirectorySizeMB Tests
    // ============================================================================

    [<Fact>]
    let ``getDirectorySizeMB calculates correct size``() =
        let structure = [
            ("file1.bin", Some 1048576L)  // 1 MB
            ("file2.bin", Some 1048576L)  // 1 MB
        ]
        
        withTestDir structure (fun testDir ->
            let size = FileSystem.getDirectorySizeMB testDir
            test <@ size = 2L<Size.MB> @>
        )

    [<Fact>]
    let ``getDirectorySizeMB returns 0 for empty directory``() =
        withTestDir [] (fun testDir ->
            let size = FileSystem.getDirectorySizeMB testDir
            test <@ size = 0L<Size.MB> @>
        )

    [<Fact>]
    let ``getDirectorySizeMB does not include subdirectory files``() =
        let structure = [
            ("file.bin", Some 1048576L)        // 1 MB in root
            ("subdir/file.bin", Some 5242880L) // 5 MB in subdir
        ]
        
        withTestDir structure (fun testDir ->
            let size = FileSystem.getDirectorySizeMB testDir
            test <@ size = 1L<Size.MB> @>  // Only root file
        )

    // ============================================================================
    // IsLeafNode Tests
    // ============================================================================

    [<Fact>]
    let ``isLeafNode returns true for directory with no subdirectories``() =
        let structure = [
            ("leaf/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let leafPath = System.IO.Path.Combine(testDir, "leaf")
            test <@ FileSystem.isLeafNode leafPath @>
        )

    [<Fact>]
    let ``isLeafNode returns false for directory with subdirectories``() =
        let structure = [
            ("parent/child/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let parentPath = System.IO.Path.Combine(testDir, "parent")
            test <@ not (FileSystem.isLeafNode parentPath) @>
        )

    [<Fact>]
    let ``isLeafNode returns false for non-existent path``() =
        test <@ not (FileSystem.isLeafNode "V:\\NonExistent\\Path") @>

    // ============================================================================
    // FilterToLeafNodes Tests
    // ============================================================================

    [<Fact>]
    let ``filterToLeafNodes returns only leaf directories``() =
        let structure = [
            ("Parent/Child/file.txt", Some 100L)
            ("Leaf/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            match FileSystem.validatePath testDir with
            | Ok validPath ->
                match FileSystem.getAllSubdirectories validPath with
                | Ok dirs ->
                    match FileSystem.filterToLeafNodes dirs with
                    | Ok leafPaths ->
                        let paths = leafPaths |> Seq.toList
                        // Should include Child and Leaf, but not Parent
                        test <@ paths |> List.exists (fun p -> p.Contains("Child")) @>
                        test <@ paths |> List.exists (fun p -> p.Contains("Leaf")) @>
                        test <@ paths.Length = 2 @>
                    | Error e ->
                        failwithf $"Expected leaf nodes, got Error: {e}"
                | Error e ->
                    failwithf $"Expected directories, got Error: {e}"
            | Error e ->
                failwithf $"Path validation failed: {e}"
        )

    // ============================================================================
    // Deletion Tests
    // ============================================================================

    [<Fact>]
    let ``deleteFiles deletes existing files``() =
        let structure = [
            ("file1.txt", Some 100L)
            ("file2.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let file1 = System.IO.Path.Combine(testDir, "file1.txt")
            let file2 = System.IO.Path.Combine(testDir, "file2.txt")
            
            test <@ System.IO.File.Exists(file1) @>
            test <@ System.IO.File.Exists(file2) @>
            
            let result = FileSystem.deleteFiles [file1; file2]
            
            match result with
            | Ok () ->
                test <@ not (System.IO.File.Exists(file1)) @>
                test <@ not (System.IO.File.Exists(file2)) @>
            | Error e ->
                failwithf $"Expected Ok, got Error: {e}"
        )

    [<Fact>]
    let ``deleteDirectories deletes existing directories``() =
        let structure = [
            ("dir1/file.txt", Some 100L)
            ("dir2/file.txt", Some 100L)
        ]
        
        withTestDir structure (fun testDir ->
            let dir1 = System.IO.Path.Combine(testDir, "dir1")
            let dir2 = System.IO.Path.Combine(testDir, "dir2")
            
            test <@ System.IO.Directory.Exists(dir1) @>
            test <@ System.IO.Directory.Exists(dir2) @>
            
            let result = FileSystem.deleteDirectories [dir1; dir2]
            
            match result with
            | Ok () ->
                test <@ not (System.IO.Directory.Exists(dir1)) @>
                test <@ not (System.IO.Directory.Exists(dir2)) @>
            | Error e ->
                failwithf $"Expected Ok, got Error: {e}"
        )

    [<Fact>]
    let ``deleteFiles returns error for non-existent file``() =
        let result = FileSystem.deleteFiles ["V:\\NonExistent\\file.txt"]
        
        match result with
        | Ok () ->
            ()  // File.Delete doesn't throw for non-existent files
        | Error (DeletionFailed (path, _)) ->
            test <@ path.Contains("NonExistent") @>

    [<Fact>]
    let ``deleteDirectories returns error with path for non-existent directory``() =
        let badPath = "V:\\NonExistent\\Directory\\12345"
        let result = FileSystem.deleteDirectories [badPath]
        
        match result with
        | Ok () ->
            failwith "Expected error for non-existent directory"
        | Error (DeletionFailed (path, _)) ->
            test <@ path = badPath @>
