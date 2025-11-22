open System
open Argu
open Errors
open Domain
open CliArguments

// ============================================================================
// Console Output Helpers (stdout for results only)
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
        printColored ConsoleColor.Yellow $"  [DIR]  {path}"
    | DeletableItem.File path ->
        printColored ConsoleColor.White $"  [FILE] {path}"

// ============================================================================
// Application Logic
// ============================================================================

let runClean (cleanFn: string -> PreviewMode -> Result<seq<DeletableItem>, DomainError>) 
             (path: string)
             (previewMode: PreviewMode) =
    
    // Blank line after progress output
    Progress.info ""
    
    match cleanFn path previewMode with
    | Ok items ->
        if Seq.isEmpty items then
            printfn "No items to clean."
        else
            if previewMode = Domain.Preview then
                printColored ConsoleColor.Cyan "PREVIEW MODE - The following items would be deleted with --execute"
                printfn ""
            else
                printColored ConsoleColor.Green "Items deleted:"
                printfn ""
            
            items |> Seq.iter printItem
            
            // Summary
            let dirs = items |> Seq.filter (function DeletableItem.Directory _ -> true | _ -> false) |> Seq.length
            let files = items |> Seq.filter (function DeletableItem.File _ -> true | _ -> false) |> Seq.length
            printfn ""
            printfn $"Total: {dirs} directories, {files} files"
        0
    | Error error ->
        match DomainError.toOptionalMessage error with
        | Some msg -> 
            Progress.error msg
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
    let parser = createParser (Some errorHandler)
    
    let assembly = Reflection.Assembly.GetExecutingAssembly()
    let version = assembly.GetName().Version
    let versionString = version.ToString()
    
    // If no arguments, show usage with version and exit
    if argv.Length = 0 then
        printfn $"DirectoryCleaner v{versionString} - Kodi/XBMC Media Directory Cleaner"
        printfn ""
        printfn "%s" (parser.PrintUsage())
        0
    else
        try
            let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
            let mode = results.GetResult(CliArguments.Mode)
            let path = results.GetResult(CliArguments.Path)

            let cleanFn = 
                match mode with
                | CleanMode.Tv -> TVShows.clean
                | CleanMode.Movies -> Movies.clean
                | CleanMode.Music -> Music.clean

            let previewMode = 
                if results.Contains(CliArguments.Execute) then 
                    Domain.Execute 
                else 
                    Domain.Preview
                    
            runClean cleanFn path previewMode
        with
        | :? ArguParseException as ex ->
            Progress.error ex.Message
            printfn ""
            printfn "%s" (parser.PrintUsage())
            1
        | ex ->
            Progress.error $"Unexpected error: {ex.Message}"
            1