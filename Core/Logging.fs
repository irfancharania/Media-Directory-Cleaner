module Logging

open System
open System.IO

// ============================================================================
// Simple Logging Functions
// ============================================================================

/// Simple console logging for informational messages
let logInfo message =
    printfn "[INFO] %s" message

/// Log cleaned items to file with custom format
let logListToFile (logFilePath: string) (list: seq<string>) = 
    try
        let timestamp = DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss")
        let header = sprintf "Cleaned on: %s" timestamp
        let separator = "---------------------------------------"
        
        let items = 
            list 
            |> Seq.map (fun path -> sprintf "    %s" path)
            |> String.concat Environment.NewLine
        
        let logEntry = 
            sprintf "%s%s%s%s%s%s%s" 
                header 
                Environment.NewLine 
                separator 
                Environment.NewLine 
                items 
                Environment.NewLine
                Environment.NewLine
        
        // Append to file (create if doesn't exist)
        File.AppendAllText(logFilePath, logEntry)
    with
    | ex ->
        eprintfn "Failed to write to log file: %s" ex.Message
