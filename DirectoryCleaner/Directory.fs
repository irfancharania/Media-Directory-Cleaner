module Directory

open System
open System.IO
open ROP
open Size

let int64ToMB = Size.int64ToBytes >> Size.bytesToMegaBytes

type FailureMessage = 
    | PathNameCannotBeEmpty
    | DirectoryNotFound
    | NoLeafNodesFound
    | FilesNotFound
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
let getFilesList path = 
    let files = DirectoryInfo(path).EnumerateFiles("*", SearchOption.TopDirectoryOnly)
    if Seq.isEmpty files then fail FilesNotFound
    else succeed files

/// Get directory size based on top-level files only
let getDirectorySize path = 
    let total (listFiles : Collections.Generic.IEnumerable<FileInfo>) = 
        let sum = listFiles |> Seq.sumBy (fun x -> x.Length)
        succeed (sum |> int64ToMB)
    path
    |> getFilesList
    |> bindR total

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
    
    let private filterDirectoriesBySize (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
        let filtered = 
            listDirectories
            |> Seq.choose (fun x -> 
                   let folderSize = getDirectorySize x.FullName
                   match folderSize with
                   | Success(y, _) when y < thresholdFolderSize -> Some(x)
                   | _ -> None)
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
    
    let private filterDirectoriesByLeafNodes (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
        let filtered = listDirectories |> Seq.filter (fun x -> isLeafNode x.FullName)
        if Seq.isEmpty filtered then fail NoLeafNodesFound
        else succeed filtered
    
    let private partitionFilesBySize (listFiles : Collections.Generic.IEnumerable<FileInfo>) = 
        let mainFiles, extraFiles = 
            listFiles |> Utility.partition (fun x -> 
                             let fileSize = x.Length |> int64ToMB
                             fileSize > thresholdFileSize)
        succeed (mainFiles, extraFiles)
    
    let private getOrphanExtraFiles ((mainFiles : seq<FileInfo>), (extraFiles : seq<FileInfo>)) = 
        let hasCorrespondingMainFile extraFile = 
            let fileName = Path.GetFileNameWithoutExtension extraFile
            mainFiles |> Seq.exists (fun (x : FileInfo) -> x.Name.Contains(fileName))
        
        // skip checking if no main files found
        let orphan = 
            if Seq.isEmpty mainFiles then extraFiles |> Seq.map (fun x -> x.FullName)
            else 
                extraFiles
                |> Seq.filter (fun x -> hasCorrespondingMainFile x.Name)
                |> Seq.map (fun x -> x.FullName)
        
        if (Seq.isEmpty orphan) then fail FilesNotFound
        else succeed orphan
    
    let private getDirectoryFiles (path : string) = 
        path
        |> getFilesList
        |> bindR partitionFilesBySize
        |> bindR getOrphanExtraFiles
    
    let private getSubdirectories (path : string) = 
        path
        |> pathExists
        |> bindR getAllDirectoriesList
        |> bindR filterDirectoriesByLeafNodes
    
    let filePathsToDelete (path : string) =
        let directories = getDirectoryFiles path
        
        match directories with
        | Failure errors -> fail errors
        | Success (x, _) -> succeed x
        
        