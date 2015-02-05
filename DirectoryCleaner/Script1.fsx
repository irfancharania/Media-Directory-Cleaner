#load "Utility.fs"
#load "ROP.fs"
#load "Size.fs"
#load "Directory.fs"

open System
open System.IO
open Utility
open ROP
open Size
open Directory

let test = "D:\Temp\New folder"
let t = TV.filePathsToDelete test
let m = Movies.folderPathsToDelete test

let a l =
    match l with
    | Success (x, _) -> x
    | _ -> Seq.empty

sprintf "\nm\n"
a m |> Seq.iter (fun x -> Console.WriteLine(sprintf "%s" x))
sprintf "\nt\n"
a t |> Seq.toList
    |> Seq.iter (fun x -> Console.WriteLine(sprintf "%s" x))

