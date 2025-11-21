module CliArguments

open Argu

// ============================================================================
// CLI Argument Types - Shared between Program and Tests
// ============================================================================

type CleanMode =
    | Tv
    | Movies
    | Music

type CliArguments =
    | [<MainCommand; ExactlyOnce>] Mode of CleanMode
    | [<AltCommandLine("-p"); Unique; Mandatory>] Path of string
    | [<Unique>] Execute
    
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Mode _ -> "cleaning mode: tv, movies, or music"
            | Path _ -> "specify the directory path to clean"
            | Execute -> "execute mode - actually delete items (default is preview only)"

/// Create the argument parser
let createParser (errorHandler:IExiter option) =
    let name = "DirectoryCleaner.exe"
    match errorHandler with
    | Some handler -> 
        ArgumentParser.Create<CliArguments>(programName = name, errorHandler = handler)
    | None -> 
        ArgumentParser.Create<CliArguments>(programName = name)
