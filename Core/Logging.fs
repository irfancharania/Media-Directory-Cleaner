module Logging

open System
open System.IO
open Serilog
open Serilog.Core

// ============================================================================
// Serilog Configuration
// ============================================================================

let mutable private logger: ILogger option = None

/// Initialize the logger with console and file sinks
let initialize (logFilePath: string) =
    let log =
        LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path = logFilePath,
                outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}",
                rollingInterval = RollingInterval.Day,
                retainedFileCountLimit = Nullable(30))
            .CreateLogger()
    
    logger <- Some log
    Log.Logger <- log
    log

/// Get the current logger, or create a default one if not initialized
let getLogger() =
    match logger with
    | Some log -> log
    | None -> 
        // Fallback to console-only logger
        let log = 
            LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger()
        logger <- Some log
        log

/// Close and flush the logger
let close() =
    //match logger with
    //| Some log -> log.Dispose()
    //| None -> ()
    Log.CloseAndFlush()

// ============================================================================
// Logging Functions
// ============================================================================

/// Log information message
let info message =
    getLogger().Information(message)

/// Log information with structured data
let infoWith message (properties: (string * obj) list) =
    let log = getLogger()
    let mutable l = log.ForContext("SourceContext", "DirectoryCleaner")
    for (key, value) in properties do
        l <- l.ForContext(key, value)
    l.Information(message)

/// Log warning message
let warn message =
    getLogger().Warning(message)

/// Log error message
let error message =
    getLogger().Error(message)

/// Log error with exception
let errorWithException message (ex: Exception) =
    getLogger().Error(ex, message)

/// Log a list of items that were cleaned
let logCleanedItems (items: seq<string>) =
    let count = Seq.length items
    if count > 0 then
        infoWith "Cleaned {Count} items" ["Count", box count]
        items |> Seq.iter (fun item -> 
            getLogger().Information("  {Item}", item))

// ============================================================================
// Legacy Support (for backward compatibility)
// ============================================================================

/// Legacy function for logging to file (now uses Serilog)
let logListToFile (logFilePath: string) (list: seq<string>) = 
    let log = initialize logFilePath
    logCleanedItems list
    close()