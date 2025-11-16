open System
open Argu
open Domain

// ============================================================================
// CLI Argument Definitions using Argu
// ============================================================================

type CleanCommand =
    | [<Mandatory>] [<AltCommandLine("-p")>] Path of path:string
    | [<AltCommandLine("--execute")>] Execute
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "specify the directory path to clean"
            | Execute -> "execute mode - actually delete items (default is preview only)"

type CliArguments =
    | [<CliPrefix(CliPrefix.None)>] Tv of ParseResults<CleanCommand>
    | [<CliPrefix(CliPrefix.None)>] Movies of ParseResults<CleanCommand>
    | [<CliPrefix(CliPrefix.None)>] Music of ParseResults<CleanCommand>
    | [<Hidden>] [<AltCommandLine("-v", "--version")>] Version
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Tv _ -> "clean TV show directories"
            | Movies _ -> "clean movie directories"
            | Music _ -> "clean music directories"
            | Version -> "display version information"

// ============================================================================
// Application Logic
// ============================================================================

let runClean (cleanFn: string -> PreviewMode -> Result<seq<string>, DomainError>) 
             (results: ParseResults<CleanCommand>) =
    let path = results.GetResult(CleanCommand.Path)
    let previewMode = 
        if results.Contains(CleanCommand.Execute) then 
            Domain.Execute 
        else 
            Domain.Preview  // Preview is now the DEFAULT
    
    match cleanFn path previewMode with
    | Ok items ->
        if Seq.isEmpty items then
            printfn "No items to clean."
        else
            if previewMode = Domain.Preview then
                printfn "PREVIEW MODE - Nothing will be deleted. Use --execute to actually delete."
                printfn ""
            printfn "Items processed:"
            items |> Seq.iter (printfn "  %s")
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
    let version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
    printfn "DirectoryCleaner v%A" version
    0

// ============================================================================
// Entry Point
// ============================================================================

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CliArguments>(programName = "DirectoryCleaner.exe", errorHandler = errorHandler)
    
    // If no arguments, show usage and exit
    if argv.Length = 0 then
        printfn "%s" (parser.PrintUsage())
        0
    else
        try
            let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
            
            // Handle version flag
            if results.Contains(Version) then
                printVersion()
            // Handle subcommands
            elif results.Contains(Tv) then
                let tvResults = results.GetResult(Tv)
                runClean TVShows.clean tvResults
            elif results.Contains(Movies) then
                let moviesResults = results.GetResult(Movies)
                runClean Movies.clean moviesResults
            elif results.Contains(Music) then
                let musicResults = results.GetResult(Music)
                runClean Music.clean musicResults
            else
                // No valid command provided, show usage
                printfn "Error: You must specify one of: tv, movies, or music"
                printfn ""
                printfn "%s" (parser.PrintUsage())
                1
        with
        | :? ArguParseException as ex ->
            printfn "%s" ex.Message
            1
        | ex ->
            eprintfn "Unexpected error: %s" ex.Message
            1