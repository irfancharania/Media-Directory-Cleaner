module Progress

open System

// ============================================================================
// Progress Reporting (stderr) - keeps stdout clean for results
// ============================================================================

/// Write to stderr (no newline)
let private writeError (message: string) =
    Console.Error.Write(message)

/// Write to stderr (with newline)
let private writeErrorLine (message: string) =
    Console.Error.WriteLine(message)

/// Run a function with progress indication
/// Shows "message... " before, "done" after
let run<'a> (message: string) (f: unit -> 'a) : 'a =
    writeError $"{message}... "
    let result = f ()
    writeErrorLine "done"
    result

/// Run a Result-returning function with progress indication
/// Shows "message... " before, "done" on success, "failed" on error
let runResult<'T, 'E> (message: string) (f: unit -> Result<'T, 'E>) : Result<'T, 'E> =
    writeError $"{message}... "
    let result = f ()
    match result with
    | Ok _ -> writeErrorLine "done"
    | Error _ -> writeErrorLine "failed"
    result

/// Report an error message to stderr
let error (message: string) =
    writeErrorLine $"Error: {message}"

/// Report an info message to stderr
let info (message: string) =
    writeErrorLine message