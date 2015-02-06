module Logging

open System
open System.IO

let getCurrentDateTime() = 
    let format = "yyyy-MMM-dd HH:mm:ss"
    let dt = DateTime.Now
    dt.ToString(format)

let separator = "---------------------------------------"

let buildHeader() = 
    let format = "Cleaned on: {0}
{1}"
    String.Format(format, getCurrentDateTime(), separator)

let buildFooter() = 
    let format = "{0}

"
    String.Format(format, separator)

let logListToFile (logFilePath : string) (listDeletedItemPaths : seq<string>) = 
    use stream = File.AppendText logFilePath
    buildHeader() |> stream.WriteLine
    listDeletedItemPaths |> Seq.iter stream.WriteLine
    buildFooter() |> stream.WriteLine
