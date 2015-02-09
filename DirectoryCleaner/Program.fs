open System
open ROP
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
        printfn "  example <command> [--<flag> ...] [-<setting> value ...]"
        printfn ""
        printfn "Commands:"
        printfn "  tv -path <TV Shows path>"
        printfn "  movies -path <Movies path>"
    
    ///A handler which prints the usage to the console
    let exec = handler (fun _ -> print())

module Cleaner = 
    ///The key used for the folder path setting
    [<Literal>]
    let PathKey = "path"
    
    let private exec f = 
        handler (fun args -> 
            match (App.tryGetSetting PathKey args) with
            | Some path -> 
                f path
                |> mapMessagesR Directory.convertFailureMessage
                |> failureTee (fun x -> 
                       let err = x |> Seq.fold (+) ""
                       if err <> "" then failwith err)
                |> ignore
            | _ -> Usage.print())
    
    let execTV = exec Directory.TV.cleanDirectory
    let execMovies = exec Directory.Movies.cleanDirectory

///Contains literals of commands
module Commands = 
    [<Literal>]
    let TV = "tv"
    
    [<Literal>]
    let Movies = "movies"

///Application entry point
[<EntryPoint>]
let main argv = 
    App.run Usage.exec [ (Commands.TV, Cleaner.execTV)
                         (Commands.Movies, Cleaner.execMovies) ] argv
