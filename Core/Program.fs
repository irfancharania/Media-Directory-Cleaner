open System
open Domain
open Directory
open FSharp.ConsoleApp

///Convert a function to a console application handler e.g. - returns 0 for success or 1 for errors.
let handler f = 
    fun args -> 
        try 
            printfn ""
            f args
            0
        with e -> 
            printfn "Error: %s" e.Message
            1

///Contains a handler that prints the application usage
module Usage = 
    ///Prints the usage to the console
    let print() = 
        printfn "Usage:"
        printfn "  DirectoryCleaner.exe <command> [--<flag> ...] [-<setting> value ...]"
        printfn ""
        printfn "Commands:"
        printfn "  tv -path <TV Shows path> [--preview]"
        printfn "  movies -path <Movies path> [--preview]"
        printfn "  music -path <Music path> [--preview]"
    
    ///A handler which prints the usage to the console
    let exec = handler (fun _ -> print())

module Cleaner = 
    ///The key used for the folder path setting
    [<Literal>]
    let PathKey = "path"
    
    ///The key used for the preview flag
    [<Literal>]
    let PreviewFlag = "preview"
    
    let private exec f = 
        handler (fun args -> 
            let path = App.tryGetSetting PathKey args
            let previewFlag = App.isFlagged PreviewFlag args
            let previewMode = if previewFlag then Domain.Preview else Domain.Execute
            
            match path with
            | Some path -> 
                match f path previewMode with
                | Ok items -> 
                    items |> Seq.iter (printfn "%s")
                | Error error -> 
                    match Domain.DomainError.toOptionalMessage error with
                    | Some msg -> failwith msg
                    | None -> () // Silent for non-critical errors
            | None -> 
                Usage.print())
    
    let execTV = exec Directory.TV.clean
    let execMovies = exec Directory.Movies.clean
    let execMusic = exec Directory.Music.clean

///Contains literals of commands
module Commands = 
    [<Literal>]
    let TV = "tv"
    
    [<Literal>]
    let Movies = "movies"

    [<Literal>]
    let Music = "music"

///Application entry point
[<EntryPoint>]
let main argv = 
    App.run Usage.exec 
        [ (Commands.TV, Cleaner.execTV)
          (Commands.Movies, Cleaner.execMovies)
          (Commands.Music, Cleaner.execMusic) ] 
        argv