module Main

open Expecto

[<EntryPoint>]
let main args =
    // Run all tests
    runTestsWithCLIArgs [] args (
        testList "All Tests" [
            DomainTests.tests
            SubtitleTests.tests
            IntegrationTests.tests
        ]
    )