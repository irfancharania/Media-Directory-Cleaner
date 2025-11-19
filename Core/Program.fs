open System
open System.IO
open Argu
open Domain

// ============================================================================
// CLI Argument Definitions using Argu SubCommands
// ============================================================================

type CleanArgs =
    | [<AltCommandLine("-p"); Mandatory; Unique>] Path of string
    | [<Unique>] Execute
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "specify the directory path to clean"
            | Execute -> "execute mode - actually delete items (default is preview only)"

type CliArguments =
    | [<CliPrefix(CliPrefix.None)>] Movies of ParseResults<CleanArgs>
    | [<CliPrefix(CliPrefix.None)>] Tv of ParseResults<CleanArgs>
    | [<CliPrefix(CliPrefix.None)>] Music of ParseResults<CleanArgs>
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Movies _ -> "clean movie directories"
            | Tv _ -> "clean TV show directories"
            | Music _ -> "clean music directories"

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
            
            // Pattern match on the subcommand to extract path and execute flag
            match results.GetAllResults() with
            | [Movies cleanArgs] ->
                let path = cleanArgs.GetResult(CleanArgs.Path)
                let previewMode = if cleanArgs.Contains(CleanArgs.Execute) then Domain.Execute else Domain.Preview
                runClean Movies.clean path previewMode
                
            | [Tv cleanArgs] ->
                let path = cleanArgs.GetResult(CleanArgs.Path)
                let previewMode = if cleanArgs.Contains(CleanArgs.Execute) then Domain.Execute else Domain.Preview
                runClean TVShows.clean path previewMode
                
            | [Music cleanArgs] ->
                let path = cleanArgs.GetResult(CleanArgs.Path)
                let previewMode = if cleanArgs.Contains(CleanArgs.Execute) then Domain.Execute else Domain.Preview
                runClean Music.clean path previewMode
                
            | [] ->
                eprintfn "Error: Please specify a command (movies, tv, or music)"
                printfn ""
                printfn "%s" (parser.PrintUsage())
                1
                
            | _ ->
                eprintfn "Error: Please specify only one command"
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