module LastRun

open System
open System.IO

[<Literal>]
let private lastRunFileName = ".lastrun"

/// Try to read the last run date from the tracking file
/// Returns UTC DateTime
let tryGetLastRunDate (basePath: string) : DateTime option =
    let filePath = Path.Combine(basePath, lastRunFileName)
    try
        if File.Exists(filePath) then
            let content = File.ReadAllText(filePath).Trim()
            // Try to parse as UTC using round-trip format
            match DateTime.TryParseExact(content, "o", null, 
                                        Globalization.DateTimeStyles.RoundtripKind) with
            | true, dt -> Some (dt.ToUniversalTime())
            | false, _ -> 
                // Fallback: try general parse and convert to UTC
                match DateTime.TryParse(content) with
                | true, dt -> Some (dt.ToUniversalTime())
                | false, _ -> None
        else
            None
    with
    | _ -> None

/// Save the current UTC date/time as the last run date
let saveLastRunDate (basePath: string) : Result<unit, string> =
    let filePath = Path.Combine(basePath, lastRunFileName)
    try
        // Use round-trip format ("o") which preserves UTC kind
        let nowUtc = DateTime.UtcNow.ToString("o")
        File.WriteAllText(filePath, nowUtc)
        Ok ()
    with
    | ex -> Error $"Failed to save last run date: {ex.Message}"

/// Check if a directory has been modified since the last run
/// Returns true if:
/// - No last run date exists (first run)
/// - Directory was created after last run
/// - Directory was modified after last run
/// All comparisons done in UTC to avoid DST/timezone issues
let hasChangedSinceLastRun (lastRunDate: DateTime option) (directoryPath: string) : bool =
    match lastRunDate with
    | None -> true  // No previous run, check everything
    | Some lastRun ->
        try
            let dirInfo = DirectoryInfo(directoryPath)
            // Convert directory times to UTC for comparison
            let creationUtc = dirInfo.CreationTimeUtc
            let modifiedUtc = dirInfo.LastWriteTimeUtc
            // Check both creation and modification times
            creationUtc > lastRun || modifiedUtc > lastRun
        with
        | _ -> true  // If we can't check, assume it needs scanning (safe default)

/// Filter directories to only those that have changed since last run
let filterChangedDirectories (lastRunDate: DateTime option) (directories: seq<string>) : seq<string> =
    directories
    |> Seq.filter (hasChangedSinceLastRun lastRunDate)