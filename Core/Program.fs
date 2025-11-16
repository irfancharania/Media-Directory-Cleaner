open System
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
    | [<Mandatory>] [<AltCommandLine("-p")>] Path of path:string
    | [<Mandatory>] [<AltCommandLine("-m")>] Mode of mode:CleanMode
    | Execute
    | [<AltCommandLine("-v")>] Version
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "specify the directory path to clean"
            | Mode _ -> "cleaning mode: tv, movies, or music"
            | Execute -> "execute mode - actually delete items (default is preview only)"
            | Version -> "display version information"

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
            else
                // Get required arguments
                let path = results.GetResult(Path)
                let mode = results.GetResult(Mode)
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
        with
        | :? ArguParseException as ex ->
            printfn "%s" ex.Message
            1
        | ex ->
            eprintfn "Unexpected error: %s" ex.Message
            1