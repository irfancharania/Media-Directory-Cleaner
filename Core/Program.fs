open System
open System.IO
open Argu
open Domain

// ============================================================================
// CLI Argument Definitions using Argu
// ============================================================================

type CleanMode =
    | [<First; ExactlyOnce>] Tv
    | [<First; ExactlyOnce>] Movies
    | [<First; ExactlyOnce>] Music

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Tv -> "clean TV show directories"
            | Movies -> "clean movie directories"
            | Music -> "clean music directories"

type CliArguments =
    | [<AltCommandLine("-p"); Unique; Mandatory>] Path of string
    | [<Unique>] Execute
    | Mode of CleanMode

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "specify the directory path to clean"
            | Mode _ -> "cleaning mode: tv, movies, or music"
            | Execute -> "execute mode - actually delete items (default is preview only)"

// ============================================================================
// Console Color Helpers
// ============================================================================

let printColored color text =
    let oldColor = Console.ForegroundColor
    Console.ForegroundColor <- color
    printfn "%s" text
    Console.ForegroundColor <- oldColor

let printItem item =
    if Directory.Exists(item) then
        printColored ConsoleColor.Yellow $"  {item}"
    else
        printColored ConsoleColor.White $"  {item}"

// ============================================================================
// Application Logic
// ============================================================================

let runClean (cleanFn: string -> PreviewMode -> Result<seq<string>, DomainError>) 
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
            
            // Get required arguments for cleaning operations
            match results.TryGetResult(Mode), results.TryGetResult(Path) with
            | Some mode, Some path ->
                let previewMode = 
                    if results.Contains(Execute) then 
                        Domain.Execute 
                    else 
                        Domain.Preview
                    
                // Select cleaning function based on mode
                let cleanFn = 
                    match mode with
                    | CleanMode.Tv -> TVShows.clean
                    | CleanMode.Movies -> Movies.clean
                    | CleanMode.Music -> Music.clean
                    
                runClean cleanFn path previewMode
            | None, _ ->
                eprintfn "Error: mode is required for cleaning operations"
                printfn ""
                printfn "%s" (parser.PrintUsage())
                1
            | _, None ->                    
                eprintfn "Error: --path is required for cleaning operations"
                printfn ""
                printfn "%s" (parser.PrintUsage())
                1
        with
        | :? ArguParseException as ex ->
            printfn "%s" ex.Message
            printfn ""
            printfn "%s" (parser.PrintUsage())
            1
        | ex ->
            eprintfn $"Unexpected error: {ex.Message}"
            1