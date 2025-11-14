module Directory

open System
open System.IO
open System.Text.RegularExpressions
open Size

let int64ToMB = Size.int64ToBytes >> Size.bytesToMegaBytes
let int64ToKB = Size.int64ToBytes >> Size.bytesToKiloBytes
let filesVideo = [ ".avi"; ".flv"; ".mkv"; ".mp4"; ".mpeg"; ".mpg"; ".wmv"; ".3gp" ]
let filesAudio = [ ".mp3"; ".m4a"; ".flac"; ".wav"; ".wma"; ".aac"; ".aiff"; ".m4b"; ".m4p"; ".ogg" ]

[<Literal>]
let logFileName = "cleanLog.log"

type FailureMessage = 
    | PathNameCannotBeEmpty
    | DirectoryNotFound
    | FilesNotFound
    | NoLeafNodesFound
    | SubdirectoriesDoNotExist
    | SubdirectoriesBelowThresholdDoNotExist

let convertFailureMessage = 
    function 
    | PathNameCannotBeEmpty -> "Path name cannot be empty"
    | DirectoryNotFound -> "Directory not found"
    | FilesNotFound | NoLeafNodesFound | SubdirectoriesDoNotExist | SubdirectoriesBelowThresholdDoNotExist -> 
        String.Empty

// Replace ROP with Result type
type CleanerResult<'T> = Result<'T, FailureMessage>

/// Validate input path
let pathExists (path : string) : CleanerResult<string> = 
    if String.IsNullOrEmpty(path) then 
        Error PathNameCannotBeEmpty
    elif not (Directory.Exists(path)) then 
        Error DirectoryNotFound
    else 
        Ok path

/// Get subdirectories for path
let private getDirectoriesList (option:SearchOption) (path: string) : CleanerResult<seq<DirectoryInfo>> = 
    let directories = DirectoryInfo(path).EnumerateDirectories("*.*", option)
    if Seq.isEmpty directories then 
        Error SubdirectoriesDoNotExist
    else 
        Ok directories

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
let private ignoreSpecialDirectories (directory : DirectoryInfo) = 
    not (directory.Name.StartsWith("."))

/// Does directory path contain subdirectories?
let isLeafNode path = 
    let filterSpecialDirectories (listDirectories : seq<DirectoryInfo>) = 
        let directories = listDirectories |> Seq.filter ignoreSpecialDirectories
        if Seq.isEmpty directories then 
            Error SubdirectoriesDoNotExist
        else 
            Ok directories
    
    match getTopDirectoriesList path |> Result.bind filterSpecialDirectories with
    | Ok _ -> false
    | Error _ -> true

/// Get list of folders that are leaf nodes
let filterDirectoriesByLeafNodes (listDirectories : seq<DirectoryInfo>) : CleanerResult<seq<string>> = 
    let filtered = 
        listDirectories
        |> Seq.filter ignoreSpecialDirectories
        |> Seq.map (fun x -> x.FullName)
        |> Seq.filter isLeafNode
    
    if Seq.isEmpty filtered then 
        Error NoLeafNodesFound
    else 
        Ok filtered

/// Print paths
let printPathList (pathList : seq<string>) = 
    pathList |> Seq.iter (fun x -> printfn "%s" x)

/// Delete folders in list of paths
let deleteFolders (pathList : seq<string>) = 
    pathList |> Seq.iter (fun x -> Directory.Delete(x, true))

/// Delete files in list of paths
let deleteFiles (pathList : seq<string>) = 
    pathList |> Seq.iter File.Delete

//-------------------------------------------------------------------
/// Movies
module Movies = 
    [<Literal>]
    let thresholdFolderSize = 100L<MB>
    
    /// Get list of folders below size threshold size
    let private filterDirectoriesBySize (listDirectories : seq<string>) : CleanerResult<seq<string>> = 
        let filtered = 
            listDirectories 
            |> Seq.choose (fun x -> 
                let folderSize = getDirectorySize x
                if folderSize < thresholdFolderSize then Some(x)
                else None)
        
        if Seq.isEmpty filtered then 
            Error SubdirectoriesBelowThresholdDoNotExist
        else 
            Ok filtered
    
    let cleanDirectory (path : string) (preview : bool) : CleanerResult<seq<string>> = 
        let logFilePath = Path.Combine(path, logFileName)
        let log = Logging.logListToFile logFilePath
        
        pathExists path
        |> Result.bind getAllDirectoriesList
        |> Result.bind filterDirectoriesByLeafNodes
        |> Result.bind filterDirectoriesBySize
        |> Result.map (fun toDelete ->
            if not preview then
                log toDelete
                deleteFolders toDelete
            toDelete)

//-------------------------------------------------------------------
/// TV
module TV = 
    [<Literal>]
    let thresholdFileSize = 100L<MB>
    
    /// Ignore local folder image files as we want to keep these
    let private filterLocalFolderImageFiles (listFiles : seq<FileInfo>) = 
        let isNotLocalFolderImage (file : FileInfo) = 
            not (file.Name.StartsWith("folder") || file.Name.StartsWith("poster"))
        listFiles |> Seq.filter isNotLocalFolderImage
    
    /// Separate file list into two: video files and extra files
    let private partitionFilesByTypeOrSize (listFiles : seq<FileInfo>) = 
        let isMainFile (file : FileInfo) = 
            let sizeGreaterThanThreshold = 
                let fileSize = file.Length |> int64ToMB
                fileSize > thresholdFileSize
            
            let extensionIsVideo = 
                let fileExtension = Path.GetExtension file.Name
                filesVideo |> Seq.exists (fun x -> x = fileExtension)
            
            sizeGreaterThanThreshold || extensionIsVideo
        
        let mainFiles, extraFiles = listFiles |> Utility.partition isMainFile
        (mainFiles, extraFiles)

    /// Get list of extra files with no corresponding main file
    let private getOrphanExtraFiles ((mainFiles : seq<FileInfo>), (extraFiles : seq<FileInfo>)) = 
        let removeSubtitleSuffix (fileName : string) = 
            if fileName.EndsWith(".en") || fileName.EndsWith(".eng") || fileName.EndsWith(".english") then
                fileName.Substring(0, fileName.Length - 3)
            else
                fileName
        
        let removeThumbnailSuffix (fileName : string) = 
            if fileName.EndsWith("-thumb") then
                fileName.Substring(0, fileName.Length - 6)
            else
                fileName
        
        let removeRippingGroupSuffix (fileName : string) = 
            let exp = @"\s\([\w\.\-\s\,]+\)?$"
            Regex.Replace(fileName, exp, String.Empty)
        
        let hasNoCorrespondingMainFile (extraFile : FileInfo) = 
            let fileName = 
                extraFile.Name
                |> Path.GetFileNameWithoutExtension
                |> removeSubtitleSuffix
                |> removeThumbnailSuffix
                |> removeRippingGroupSuffix

            mainFiles
            |> Seq.exists (fun x -> x.Name.Contains(fileName))
            |> not
        
        let orphans = 
            if Seq.isEmpty mainFiles then 
                extraFiles
            else 
                extraFiles |> Seq.filter hasNoCorrespondingMainFile
        
        orphans |> Seq.map (fun x -> x.FullName)
    
    /// Get list of files from all subdirectories
    let private getSubDirectoryFiles (subdirectories : seq<string>) : CleanerResult<seq<string>> = 
        let getOrphansPerDirectory = 
            getFilesList
            >> filterLocalFolderImageFiles
            >> partitionFilesByTypeOrSize
            >> getOrphanExtraFiles
        
        let orphans = 
            subdirectories
            |> Seq.map getOrphansPerDirectory
            |> Seq.concat
        
        if Seq.isEmpty orphans then 
            Error FilesNotFound
        else 
            Ok orphans
    
    let cleanDirectory (path : string) (preview : bool) : CleanerResult<seq<string>> = 
        let logFilePath = Path.Combine(path, logFileName)
        let log = Logging.logListToFile logFilePath
        
        pathExists path
        |> Result.bind getAllDirectoriesList
        |> Result.bind filterDirectoriesByLeafNodes
        |> Result.bind getSubDirectoryFiles
        |> Result.map (fun toDelete ->
            if not preview then
                log toDelete
                deleteFiles toDelete
            toDelete)

//-------------------------------------------------------------------
/// Music
module Music = 
    [<Literal>]
    let thresholdFileSize = 500L<kB>
    
    /// Check if directory has orphan files (no main audio files)
    let private hasOrphanFiles (listFiles : seq<FileInfo>) = 
        let isMainFile (file : FileInfo) = 
            let sizeGreaterThanThreshold = 
                let fileSize = file.Length |> int64ToKB
                fileSize > thresholdFileSize
            
            let extensionIsAudio = 
                let fileExtension = Path.GetExtension file.Name
                filesAudio |> Seq.exists (fun x -> x = fileExtension)
            
            sizeGreaterThanThreshold || extensionIsAudio
        
        let hasMainFiles = listFiles |> Seq.exists isMainFile
        not hasMainFiles
    
    let private filterDirectoriesWithoutMainFiles (subdirectories : seq<string>) : CleanerResult<seq<string>> = 
        let getOrphanedDirectory = getFilesList >> hasOrphanFiles
        let orphans = subdirectories |> Seq.filter getOrphanedDirectory
        
        if Seq.isEmpty orphans then 
            Error SubdirectoriesBelowThresholdDoNotExist
        else 
            Ok orphans
    
    let cleanDirectory (path : string) (preview : bool) : CleanerResult<seq<string>> = 
        let logFilePath = Path.Combine(path, logFileName)
        let log = Logging.logListToFile logFilePath
        
        pathExists path
        |> Result.bind getAllDirectoriesList
        |> Result.bind filterDirectoriesByLeafNodes
        |> Result.bind filterDirectoriesWithoutMainFiles
        |> Result.map (fun toDelete ->
            if not preview then
                log toDelete
                deleteFolders toDelete
            toDelete)