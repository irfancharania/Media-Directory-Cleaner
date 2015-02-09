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

let logListToFile (logFilePath : string) (list : seq<string>) = 
    let logItems items = 
        if (items
            |> Seq.filter ((<>) "")
            |> Seq.isEmpty
            |> not)
        then 
            use stream = File.AppendText logFilePath
            let logItem item = stream.WriteLine(sprintf "\t%s" item)
            buildHeader "Cleaned on" |> stream.WriteLine
            items |> Seq.iter logItem
            buildFooter() |> stream.WriteLine
    logItems list
