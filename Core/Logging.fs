module Logging

open System
open System.IO

// ============================================================================
// Simple Logging Functions
// ============================================================================

/// Simple console logging for informational messages
let logInfo message =
    printfn $"[INFO] {message}"

/// Log cleaned items to file with custom format
let logListToFile (logFilePath: string) (list: seq<string>) = 
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
                ""  // Extra newline at end
                ""  // Extra newline for separation between entries
            ]
        
        // Append to file (create if doesn't exist)
        File.AppendAllText(logFilePath, logEntry)
    with
    | ex ->
        eprintfn $"Failed to write to log file: {ex.Message}"