open System
open System.IO
open Argu
open Domain

// ============================================================================
// CLI Argument Definitions using Argu (Simplified)
// ============================================================================

type CleanMode =
    | Tv
    | Movies
    | Music

type CliArguments =
    | [<AltCommandLine("-p") ; Unique ; Mandatory>] Path of string
    | [<AltCommandLine("-m") ; Unique ; Mandatory>] Mode of CleanMode
    | [<Unique>] Execute
    
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
        printColored ConsoleColor.Yellow (sprintf "  %s" item)
    else
        printColored ConsoleColor.White (sprintf "  %s" item)

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
            eprintfn "Error: %s" msg
            1
        | None -> 
            printfn "Nothing to clean."
            0

let printVersion() =
    let assembly = Reflection.Assembly.GetExecutingAssembly()
    let version = assembly.GetName().Version
    let name = assembly.GetName().Name
    printfn "%s v%s" name (version.ToString(3))
    printfn "Kodi/XBMC Media Directory Cleaner"
    0

// ============================================================================
// Entry Point
// ============================================================================

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CliArguments>(programName = "DirectoryCleaner.exe", errorHandler = errorHandler)
    
    // Show version in help header
    let assembly = Reflection.Assembly.GetExecutingAssembly()
    let version = assembly.GetName().Version
    let versionString = version.ToString(3)
    
    // If no arguments, show usage with version and exit
    if argv.Length = 0 then
        printfn "DirectoryCleaner v%s - Kodi/XBMC Media Directory Cleaner" versionString
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
                    | Tv -> TVShows.clean
                    | Movies -> Movies.clean
                    | Music -> Music.clean
                    
                runClean cleanFn path previewMode
            | None, _ ->
                eprintfn "Error: --mode is required for cleaning operations"
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
            eprintfn "Unexpected error: %s" ex.Message
            1