module Logging

open System
open System.IO
open Serilog
open Serilog.Core

// ============================================================================
// Serilog Configuration
// ============================================================================

/// Initialize the logger with console and file sinks
/// Returns a Logger (concrete type) that implements IDisposable
let initialize (logFilePath: string) : Logger =
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

// ============================================================================
// Logging Functions
// ============================================================================

/// Log information message (requires logger instance)
let info (logger: ILogger) message =
    logger.Information(message)

/// Log information with structured data (requires logger instance)
let infoWith (logger: ILogger) message (properties: (string * obj) list) =
    let mutable l = logger.ForContext("SourceContext", "DirectoryCleaner")
    for (key, value) in properties do
        l <- l.ForContext(key, value)
    l.Information(message)

/// Log warning message (requires logger instance)
let warn (logger: ILogger) message =
    logger.Warning(message)

/// Log error message (requires logger instance)
let error (logger: ILogger) message =
    logger.Error(message)

/// Log error with exception (requires logger instance)
let errorWithException (logger: ILogger) message (ex: Exception) =
    logger.Error(ex, message)

/// Log a list of items that were cleaned (requires logger instance)
let logCleanedItems (logger: ILogger) (items: seq<string>) =
    let count = Seq.length items
    if count > 0 then
        infoWith logger "Cleaned {Count} items" ["Count", box count]
        items |> Seq.iter (fun item -> 
            logger.Information("  {Item}", item))

/// Simple console logging for informational messages
let logInfo message =
    printfn "[INFO] %s" message

// ============================================================================
// Legacy Support (for backward compatibility)
// ============================================================================

/// Legacy function for logging to file (now uses Serilog with proper disposal)
let logListToFile (logFilePath: string) (list: seq<string>) = 
    use logger = initialize logFilePath
    logCleanedItems logger list
    // Logger will be disposed automatically here