module Logging

open System
open System.IO
open Utility
open ROP

let getCurrentDateTime() = 
    let format = "yyyy-MMM-dd HH:mm:ss"
    let dt = Utility.LocalDateTime(DateTime.Now)
    Utility.formatLocalDateTime format dt

let private separator = "---------------------------------------"

let private buildHeader text = 
    let format = "{0}: {1}
{2}"
    String.Format(format, text, getCurrentDateTime(), separator)

let private buildFooter() = "\n"

let logListToFile (logFilePath : string) (result : RopResult<seq<string>, string>) = 
    use stream = File.AppendText logFilePath
    let logItem item = stream.WriteLine(sprintf "\t%s" item)
    
    let logItems text items = 
        if (items
            |> Seq.filter ((<>) "")
            |> Seq.isEmpty
            |> not)
        then 
            buildHeader text |> stream.WriteLine
            items |> Seq.iter logItem
            buildFooter() |> stream.WriteLine
    
    let logSuccess = logItems "Cleaned on"
    let logFailure = logItems "Failed on"
    result
    |> failureTee logFailure
    |> successTee (fun (x, _) -> logSuccess x)
