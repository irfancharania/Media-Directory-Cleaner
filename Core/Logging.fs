module Logging

open System
open System.IO

// ============================================================================
// File Logging Functions
// ============================================================================

/// Log cleaned items to file with custom format
let logListToFile (logFilePath: string) (list: seq<string>) = 
    let items = list |> Seq.toList
    
    if not (List.isEmpty items) then        
        try
            let timestamp = DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss")
            let header = $"Cleaned on: {timestamp}"
            let separator = "---------------------------------------"
        
            let items = 
                list 
                |> Seq.map (fun path -> $"    {path}")
                |> String.concat Environment.NewLine
        
            let logEntry = 
                String.concat Environment.NewLine [
                    header
                    separator
                    items
                    ""
                    ""
                ]
        
            File.AppendAllText(logFilePath, logEntry)
        with
        | ex ->
            Progress.error $"Failed to write to log file: {ex.Message}"