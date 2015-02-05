open System

type Mode = 
    | TV
    | Movie




[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    let pause = Console.ReadKey()
    0 // return an integer exit code
