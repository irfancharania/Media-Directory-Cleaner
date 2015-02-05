module Directory

open System
open System.IO
open ROP
open Size

let int64ToMB = Size.int64ToBytes >> Size.bytesToMegaBytes

type FailureMessage = 
    | PathNameCannotBeEmpty
    | DirectoryNotFound
    | FilesNotFound
    | NoLeafNodesFound
    | SubdirectoriesDoNotExist
    | SubdirectoriesBelowThresholdDoNotExist

/// Validate input path
let pathExists (path : string) = 
    let pathNotBlank path = 
        if not (String.IsNullOrEmpty(path)) then succeed path
        else fail PathNameCannotBeEmpty
    
    let directoryFound path = 
        if Directory.Exists(path) then succeed (path)
        else fail DirectoryNotFound
    
    path
    |> pathNotBlank
    |> bindR directoryFound

/// Get subdirectories for path
let private getDirectoriesList option path = 
    let directories = DirectoryInfo(path).EnumerateDirectories("*.*", option)
    if Seq.isEmpty directories then fail SubdirectoriesDoNotExist
    else succeed directories

let getTopDirectoriesList = getDirectoriesList SearchOption.TopDirectoryOnly
let getAllDirectoriesList = getDirectoriesList SearchOption.AllDirectories

/// Get list of files in directory
let getFilesList path = DirectoryInfo(path).EnumerateFiles("*", SearchOption.TopDirectoryOnly)

/// Get directory size based on top-level files only
let getDirectorySize = 
    getFilesList
    >> Seq.sumBy (fun x -> x.Length)
    >> int64ToMB

/// Does directory path contain subdirectories?
let isLeafNode path = 
    let subDirectories = getTopDirectoriesList path
    match subDirectories with
    | Success(x, _) -> false
    | Failure _ -> true

//-------------------------------------------------------------------
/// Movies
(* 
If folder size below threshold, movie file may have been deleted.
In this case, delete the folder.

Main movie files are expected to be 1 level deep.
Expected folder structure:

Movies
   |---- Some Movie (2015)
   |       |---- <ignore>
   |
   |---- Some Movie2 (2015)

*)
module Movies = 
    let thresholdFolderSize = 1L<MB>
    
    /// Get list of folders below size threshold size
    let private filterDirectoriesBySize (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
        let filtered = 
            listDirectories
            |> Seq.choose (fun x -> 
                   let folderSize = getDirectorySize x.FullName
                   if folderSize < thresholdFolderSize then Some(x)
                   else None)
            |> Seq.map (fun x -> x.FullName)
        if Seq.isEmpty filtered then fail SubdirectoriesBelowThresholdDoNotExist
        else succeed filtered
    
    let folderPathsToDelete (path : string) = 
        path
        |> pathExists
        |> bindR getTopDirectoriesList
        |> bindR filterDirectoriesBySize

//-------------------------------------------------------------------
/// TV
(* 
TV show files consist of 
1. main video file (large size)
2. extra info/artwork files (small size)

All episode files for season/year are contained within same folder.
If files sized below threshold do not have a corresponding large file, delete them

TV show files are expected to be in leaf nodes.
Expected folder structure:

TV Shows
   |----TV Show 1
   |       |----Season #
   |            |--Files
   |----TV Show 2 (year)
   |       |--Files
   |----TV Show 3
   |       |----2008
   |            |--Files

*)
module TV = 
    let thresholdFileSize = 1L<MB>
    
    /// Get list of folders that are leaf nodes
    let private filterDirectoriesByLeafNodes (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
        let filtered = 
            listDirectories
            |> Seq.filter (fun x -> isLeafNode x.FullName)
            |> Seq.map (fun x -> x.FullName)
        if Seq.isEmpty filtered then fail NoLeafNodesFound
        else succeed filtered
    
    /// Separate file list into two based on file size
    let private partitionFilesBySize (listFiles : Collections.Generic.IEnumerable<FileInfo>) = 
        let mainFiles, extraFiles = 
            listFiles |> Utility.partition (fun x -> 
                             let fileSize = x.Length |> int64ToMB
                             fileSize > thresholdFileSize)
        (mainFiles, extraFiles)
    
    /// Get list of extra files with no corresponding main file
    let private getOrphanExtraFiles ((mainFiles : seq<FileInfo>), (extraFiles : seq<FileInfo>)) = 
        let hasCorrespondingMainFile extraFile = 
            let fileName = Path.GetFileNameWithoutExtension extraFile
            mainFiles |> Seq.exists (fun (x : FileInfo) -> x.Name.Contains(fileName))
        
        // skip checking if no main files found
        let orphans = 
            if Seq.isEmpty mainFiles then extraFiles |> Seq.map (fun x -> x.FullName)
            else 
                extraFiles
                |> Seq.filter (fun x -> not (hasCorrespondingMainFile x.Name))
                |> Seq.map (fun x -> x.FullName)
        
        orphans
    
    /// Get list of files from all subdirectories
    let private getSubDirectoryFiles (subdirectories : seq<string>) = 
        let getOrphansPerDirectory = 
            getFilesList
            >> partitionFilesBySize
            >> getOrphanExtraFiles
        
        let orphans = 
            subdirectories
            |> Seq.map getOrphansPerDirectory
            |> Seq.concat
        
        if (Seq.isEmpty orphans) then fail FilesNotFound
        else succeed orphans
    
    let filePathsToDelete (path : string) = 
        path
        |> pathExists
        |> bindR getAllDirectoriesList
        |> bindR filterDirectoriesByLeafNodes
        |> bindR getSubDirectoryFiles
