﻿module Directory

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

/// ignore special folders starting with "."
let private ignoreSpecialDirectories (directory : DirectoryInfo) = not (directory.Name.StartsWith("."))

/// Does directory path contain subdirectories?
let isLeafNode path = 
    let filterSpecialDirectories (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
        let directories = listDirectories |> Seq.filter ignoreSpecialDirectories
        if Seq.isEmpty directories then fail SubdirectoriesDoNotExist
        else succeed directories
    
    let foundSubdirectories = 
        path
        |> getTopDirectoriesList
        |> bindR filterSpecialDirectories
    
    match foundSubdirectories with
    | Success _ -> false
    | Failure _ -> true

/// Get list of folders that are leaf nodes
let filterDirectoriesByLeafNodes (listDirectories : Collections.Generic.IEnumerable<DirectoryInfo>) = 
    let filtered = 
        listDirectories
        |> Seq.filter ignoreSpecialDirectories
        |> Seq.map (fun x -> x.FullName)
        |> Seq.filter isLeafNode
    if Seq.isEmpty filtered then fail NoLeafNodesFound
    else succeed filtered

/// Print paths
let printPathList (pathList : seq<string>) = pathList |> Seq.iter (fun x -> printfn "%s" x)

/// Delete folders in list of paths
let deleteFolders (pathList : seq<string>) = 
    //pathList 
    //|> Seq.iter (fun x -> Directory.Delete(x, true))
    printPathList pathList

/// Delete files in list of paths
let deleteFiles (pathList : seq<string>) = 
    //pathList 
    //|> Seq.iter File.Delete
    printPathList pathList

//-------------------------------------------------------------------
/// Movies
(* 
Folders with movie files will have size above threshold.

Main movie folder may contain set folders with subdirectories.
Delete leaf directories below threshold.

Any left over directories will become leaf directories for next run

Expected folder structure:

Movies
   |---- Some Movie (2015)
   |       |---- <ignore>
   |
   |---- Movie Set
   |       |---- Another Movie 1 (2010)
   |       |        |---- <ignore>
   |       |
   |       |---- Another Movie 2 (2011)


*)
module Movies = 
    [<Literal>]
    let thresholdFolderSize = 100L<MB>
    
    /// Get list of folders below size threshold size
    let private filterDirectoriesBySize (listDirectories : seq<string>) = 
        let filtered = 
            listDirectories |> Seq.choose (fun x -> 
                                   let folderSize = getDirectorySize x
                                   if folderSize < thresholdFolderSize then Some(x)
                                   else None)
        if Seq.isEmpty filtered then fail SubdirectoriesBelowThresholdDoNotExist
        else succeed filtered
    
    let cleanDirectory (path : string) = 
        path
        |> pathExists
        |> bindR getAllDirectoriesList
        |> bindR filterDirectoriesByLeafNodes
        |> bindR filterDirectoriesBySize
        |> successTee (fun (x, _) -> deleteFolders x)

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
    [<Literal>]
    let thresholdFileSize = 100L<MB>
    
    /// Separate file list into two based on file size
    let private partitionFilesBySize (listFiles : Collections.Generic.IEnumerable<FileInfo>) = 
        let mainFiles, extraFiles = 
            listFiles |> Utility.partition (fun x -> 
                             let fileSize = x.Length |> int64ToMB
                             fileSize > thresholdFileSize)
        (mainFiles, extraFiles)
    
    /// Get list of extra files with no corresponding main file
    let private getOrphanExtraFiles ((mainFiles : seq<FileInfo>), (extraFiles : seq<FileInfo>)) = 
        let removeSubtitleSuffix (fileName : string) = 
            match (fileName.EndsWith(".en")) with
            | false -> fileName
            | true -> fileName.Substring(0, fileName.Length - 3)
        
        let doesNotHaveCorrespondingMainFile (extraFile : FileInfo) = 
            let getFileName = Path.GetFileNameWithoutExtension >> removeSubtitleSuffix
            mainFiles
            |> Seq.exists (fun x -> x.Name.Contains(getFileName extraFile.Name))
            |> not
        
        // skip checking if no main files found
        let orphans = 
            if Seq.isEmpty mainFiles then extraFiles |> Seq.map (fun x -> x.FullName)
            else 
                extraFiles
                |> Seq.filter doesNotHaveCorrespondingMainFile
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
    
    let cleanDirectory (path : string) = 
        path
        |> pathExists
        |> bindR getAllDirectoriesList
        |> bindR filterDirectoriesByLeafNodes
        |> bindR getSubDirectoryFiles
        |> successTee (fun (x, _) -> deleteFiles x)
