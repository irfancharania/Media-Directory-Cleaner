namespace MediaDirectoryCleaner.Tests.Unit

open Xunit
open Swensen.Unquote
open Errors

module ErrorTests =

    // ============================================================================
    // Error Message Formatting
    // ============================================================================

    [<Fact>]
    let ``PathEmpty error has non-empty message``() =
        let error = ValidationError PathEmpty |> DomainError.toMessage
        test <@ not (System.String.IsNullOrWhiteSpace(error)) @>

    [<Fact>]
    let ``PathNotFound error includes path``() =
        let testPath = "V:\\test"
        let error = ValidationError (PathNotFound testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>
    
    [<Fact>]
    let ``PathNotDirectory error includes path``() =
        let testPath = "C:\\file.txt"
        let error = ValidationError (PathNotDirectory testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>

    [<Fact>]
    let ``NoSubdirectories error includes path``() =
        let testPath = "C:\\empty"
        let error = DirectoryError (NoSubdirectories testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>

    [<Fact>]
    let ``NoLeafNodes error includes path``() =
        let testPath = "C:\\nested"
        let error = DirectoryError (NoLeafNodes testPath) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>

    [<Fact>]
    let ``AccessDenied error includes path and exception message``() =
        let testPath = "C:\\protected"
        let ex = System.Exception("Permission denied")
        let error = DirectoryError (AccessDenied (testPath, ex)) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>
        test <@ error.Contains("Permission denied") @>

    [<Fact>]
    let ``DeletionFailed error includes path and exception message``() =
        let testPath = "C:\\locked\\file.txt"
        let ex = System.Exception("File in use")
        let error = CleaningError (DeletionFailed (testPath, ex)) |> DomainError.toMessage
        test <@ error.Contains(testPath) @>
        test <@ error.Contains("File in use") @>

    // ============================================================================
    // Optional Message (Non-Critical vs Critical Errors)
    // ============================================================================

    [<Fact>]
    let ``NoLeafNodes returns None for optional message``() =
        let error = DirectoryError (NoLeafNodes "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>
    
    [<Fact>]
    let ``NoSubdirectories returns None for optional message``() =
        let error = DirectoryError (NoSubdirectories "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>

    [<Fact>]
    let ``NoFilesFound returns None for optional message``() =
        let error = DirectoryError (NoFilesFound "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg = None @>

    [<Fact>]
    let ``PathEmpty returns Some for optional message``() =
        let error = ValidationError PathEmpty
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>

    [<Fact>]
    let ``PathNotFound returns Some for optional message``() =
        let error = ValidationError (PathNotFound "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>

    [<Fact>]
    let ``PathNotDirectory returns Some for optional message``() =
        let error = ValidationError (PathNotDirectory "test")
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>
    
    [<Fact>]
    let ``AccessDenied returns Some for optional message``() =
        let error = DirectoryError (AccessDenied ("test", System.Exception("test")))
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>
    
    [<Fact>]
    let ``DeletionFailed returns Some for optional message``() =
        let error = CleaningError (DeletionFailed ("test", System.Exception("test")))
        let msg = DomainError.toOptionalMessage error
        test <@ msg <> None @>