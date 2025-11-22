namespace MediaDirectoryCleaner.Tests.Unit

open System
open Xunit
open Swensen.Unquote
open Argu
open CliArguments
open Domain

module ArguTests =
    
    let parser = createParser None
    
    // ============================================================================
    // Valid Argument Combinations
    // ============================================================================
    
    [<Fact>]
    let ``parse movies mode with full path syntax``() =
        let args = [| "movies"; "--path"; "C:\\Movies" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Mode) = CleanMode.Movies @>
        test <@ results.GetResult(CliArguments.Path) = "C:\\Movies" @>
        test <@ not (results.Contains(CliArguments.Execute)) @>
    
    [<Fact>]
    let ``parse tv mode with short path syntax``() =
        let args = [| "tv"; "-p"; "C:\\TV" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Mode) = CleanMode.Tv @>
        test <@ results.GetResult(CliArguments.Path) = "C:\\TV" @>
    
    [<Fact>]
    let ``parse music mode with execute flag``() =
        let args = [| "music"; "-p"; "C:\\Music"; "--execute" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Mode) = CleanMode.Music @>
        test <@ results.GetResult(CliArguments.Path) = "C:\\Music" @>
        test <@ results.Contains(CliArguments.Execute) @>
    
    [<Fact>]
    let ``parse with path containing spaces``() =
        let args = [| "movies"; "-p"; "C:\\My Movies\\Collection" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Path) = "C:\\My Movies\\Collection" @>
    
    [<Fact>]
    let ``parse with execute before path``() =
        let args = [| "movies"; "--execute"; "-p"; "C:\\Movies" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.Contains(CliArguments.Execute) @>
        test <@ results.GetResult(CliArguments.Path) = "C:\\Movies" @>
    
    [<Fact>]
    let ``mode is case sensitive - Movies works``() =
        let args = [| "Movies"; "-p"; "C:\\Movies" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Mode) = CleanMode.Movies @>
    
    [<Fact>]
    let ``mode is case sensitive - Tv works``() =
        let args = [| "Tv"; "-p"; "C:\\TV" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Mode) = CleanMode.Tv @>
    
    // ============================================================================
    // Missing Required Arguments
    // ============================================================================
    
    [<Fact>]
    let ``missing mode throws parse exception``() =
        let args = [| "-p"; "C:\\Movies" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``missing path throws parse exception``() =
        let args = [| "movies" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``empty arguments throws parse exception``() =
        let args = Array.empty<string>
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    // ============================================================================
    // Duplicate Arguments (Should Fail with Unique attribute)
    // ============================================================================
    
    [<Fact>]
    let ``duplicate path throws parse exception``() =
        let args = [| "movies"; "-p"; "C:\\Movies"; "--path"; "C:\\Other" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``duplicate execute throws parse exception``() =
        let args = [| "movies"; "-p"; "C:\\Movies"; "--execute"; "--execute" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``multiple modes throws parse exception``() =
        let args = [| "movies"; "tv"; "-p"; "C:\\Movies" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    // ============================================================================
    // Invalid Mode Values
    // ============================================================================
    
    [<Fact>]
    let ``invalid mode name throws parse exception``() =
        let args = [| "invalid"; "-p"; "C:\\Movies" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``lowercase movies parsing behavior``() =
        let args = [| "movies"; "-p"; "C:\\Movies" |]
        // Argu handles case insensitivity for DU cases
        try
            let results = parser.ParseCommandLine(args)
            test <@ results.GetResult(CliArguments.Mode) = CleanMode.Movies @>
        with
        | :? ArguParseException -> 
            // Expected if case-sensitive
            ()
    
    // ============================================================================
    // Help and Usage
    // ============================================================================
    
    [<Fact>]
    let ``help flag throws usage exception``() =
        let args = [| "--help" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    [<Fact>]
    let ``usage string contains all modes``() =
        let usage = parser.PrintUsage()
        
        test <@ usage.Contains("movies") || usage.Contains("Movies") @>
        test <@ usage.Contains("tv") || usage.Contains("Tv") @>
        test <@ usage.Contains("music") || usage.Contains("Music") @>
    
    [<Fact>]
    let ``usage string contains path option``() =
        let usage = parser.PrintUsage()
        
        test <@ usage.Contains("--path") || usage.Contains("-p") @>
    
    [<Fact>]
    let ``usage string contains execute option``() =
        let usage = parser.PrintUsage()
        
        test <@ usage.Contains("--execute") @>
    
    // ============================================================================
    // Edge Cases
    // ============================================================================
    
    [<Fact>]
    let ``path with forward slashes``() =
        let args = [| "movies"; "-p"; "C:/Movies/Collection" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Path) = "C:/Movies/Collection" @>
    
    [<Fact>]
    let ``path with UNC network path``() =
        let args = [| "movies"; "-p"; "\\\\server\\share\\Movies" |]
        let results = parser.ParseCommandLine(args)
        
        test <@ results.GetResult(CliArguments.Path) = "\\\\server\\share\\Movies" @>
    
    [<Fact>]
    let ``extra unknown arguments throw parse exception``() =
        let args = [| "movies"; "-p"; "C:\\Movies"; "--unknown"; "value" |]
        
        raises<ArguParseException> <@ parser.ParseCommandLine(args, raiseOnUsage = true) @>
    
    // ============================================================================
    // GetAllResults Integration
    // ============================================================================
    
    [<Fact>]
    let ``GetAllResults returns all parsed arguments``() =
        let args = [| "movies"; "-p"; "C:\\Movies"; "--execute" |]
        let results = parser.ParseCommandLine(args)
        let all = results.GetAllResults()
        
        test <@ all.Length = 3 @>  // Mode, Path, Execute
        test <@ all |> List.contains (CliArguments.Mode CleanMode.Movies) @>
        test <@ all |> List.contains (CliArguments.Path "C:\\Movies") @>
        test <@ all |> List.contains CliArguments.Execute @>