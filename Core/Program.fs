open System
open System.IO
open Argu
open Domain

// ============================================================================
// CLI Argument Definitions using Argu SubCommands
// ============================================================================

type CleanMode =
    | Tv
    | Movies
    | Music

type CliArguments =
    | [<MainCommand; ExactlyOnce>] Mode of CleanMode
    | [<AltCommandLine("-p") ; Unique ; Mandatory>] Path of string
    | [<Unique>] Execute
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Mode _ -> "cleaning mode: tv, movies, or music"
            | Path _ -> "specify the directory path to clean"
            | Execute -> "execute mode - actually delete items (default is preview only)"

// ============================================================================
// Console Color Helpers
// ============================================================================

let printColored color text =
    let oldColor = Console.ForegroundColor
    try
        Console.ForegroundColor <- color
        printfn "%s" text
    finally
        Console.ForegroundColor <- oldColor

let printItem item =
    match item with
    | DeletableItem.Directory path ->
        printColored ConsoleColor.Yellow $"  {path}"
    | DeletableItem.File path ->
        printColored ConsoleColor.White $"  {path}"

// ============================================================================
// Application Logic
// ============================================================================

let runClean (cleanFn: string -> PreviewMode -> Result<seq<DeletableItem>, DomainError>) 
             (path: string)
             (previewMode: PreviewMode) =
    match cleanFn path previewMode with
    | Ok items ->
        if Seq.isEmpty items then
            printfn "No items to clean."
        else
            if previewMode = Domain.Preview then
                printfn "PREVIEW MODE - The following files will be deleted when run with --execute"
                printfn ""
                printfn "Items found:"
            else
                printfn "Items deleted:"
            
            items |> Seq.iter printItem
        0
    | Error error ->
        match Domain.DomainError.toOptionalMessage error with
        | Some msg -> 
            eprintfn $"Error: {msg}"
            1
        | None -> 
            printfn "Nothing to clean."
            0

// ============================================================================
// Entry Point
// ============================================================================

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CliArguments>(programName = "DirectoryCleaner.exe", errorHandler = errorHandler)
    
    let assembly = Reflection.Assembly.GetExecutingAssembly()
    let version = assembly.GetName().Version
    let versionString = version.ToString(3)
    
    // If no arguments, show usage with version and exit
    if argv.Length = 0 then
        printfn $"DirectoryCleaner v{versionString} - Kodi/XBMC Media Directory Cleaner"
        printfn ""
        printfn "%s" (parser.PrintUsage())
        0
    else
        try
            let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
            let mode = results.GetResult(Mode)
            let path = results.GetResult(Path)

            // Select cleaning function based on mode
            let cleanFn = 
                match mode with
                | Tv -> TVShows.clean
                | Movies -> Movies.clean
                | Music -> Music.clean

            let previewMode = 
                if results.Contains(Execute) then 
                    Domain.Execute 
                else 
                    Domain.Preview
            runClean cleanFn path previewMode
        with
        | :? ArguParseException as ex ->
            printfn "%s" ex.Message
            printfn ""
            printfn "%s" (parser.PrintUsage())
            1
        | ex ->
            eprintfn $"Unexpected error: {ex.Message}"
            1