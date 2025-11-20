F# Development Guide

> This document provides context for AI assistants working on this F# project. It defines architectural principles, coding standards, and project-specific patterns.

## Project Context

**Purpose:** Tool to help keep Kodi/XBMC media directories clean by removing orphaned metadata files left after media deletion.

**Target Framework:** .NET 10 / F# 10

## Architectural Principles

### 1. Domain-Driven Design (DDD)
We follow tactical DDD patterns with clear layer separation:

- **Domain Layer** - Pure business logic, no I/O or infrastructure dependencies
- **Infrastructure Layer** - I/O operations (file system, external services)
- **Application Layer** - Workflow orchestration, use case coordination
- **Entry Point** - Minimal Program.fs that delegates to application layer

**Critical:** Domain layer must never perform I/O directly. Use infrastructure layer for all file system operations.

### 2. Railway-Oriented Programming (ROP)
Error handling follows Scott Wlaschin's Railway-Oriented Programming:

- Use `Result<'T, 'Error>` for all fallible operations
- Compose with `Result.bind`, `Result.map`, `Result.mapError`
- Never use exceptions for business logic control flow
- Side effects via `Result.tee` and `Result.teeError` at pipeline edges

### 3. Functional Core, Imperative Shell
- **Pure functions** - All domain logic, calculations, transformations
- **I/O at boundaries** - File, database, HTTP operations in infrastructure layer, wrapped in Result
- **Side effects at edges** - Logging, telemetry only at pipeline endpoints

### 4. Type-Driven Design
- Make illegal states unrepresentable with constrained types
- Use single-case discriminated unions for type safety
- Smart constructors with validation return `Result<T, Error>`
- Private constructors prevent invalid instances
- Use `internal` for infrastructure-only constructors

## Technology Stack

### Core Libraries
- **FsToolkit.ErrorHandling** - utility library to work with the Result type in F#
- **Argu** - declarative CLI argument parser for F# console applications

### Testing
- **Unquote** - F#-idiomatic assertions with quotations, provides step-by-step failure messages
- **FsCheck** - Property-based testing for invariants and edge cases

## Coding Standards

### DO ✅

** NuGet package/Library use **
- Use built-in libraries where applicable (e.g. for email validation with System.Net.Mail.MailAddress)
- Use standard libraries for functions where applicable (e.g. FsToolkit.ErrorHandling, Argu, etc)

**Type Design**
- Use constrained types for domain primitives
- Single-case DUs for IDs and value objects
- Smart constructors with validation
- Explicit type annotations on public functions
- Use `internal` constructors when type should only be created by infrastructure layer

**Function Design**
- Keep functions small (< 20 lines ideal)
- Single responsibility per function
- Pure functions for business logic
- Descriptive names over anonymous lambdas
- Prefer idiomatic F#
- **Prefer explicit piping over computation expressions** for readability
- Strive for functional core, imperative shell

**Error Handling**
- Return `Result<T, Error>` for operations that can fail
- Handle all error cases explicitly
- Never catch exceptions in domain logic
- Isolate exception handling at I/O boundaries

**Module Organization**
- One module per file (except where it makes sense like Domain)
- Public API at bottom of file
- Private helpers at top
- Clear separation of concerns
- Group related functionality (e.g., CLI types in CliArguments.fs)

**Testing**
- Test pure functions without mocking
- Use Unquote for clear, quotation-based assertions
- Property-based tests (FsCheck) for business invariants
- Integration tests for I/O boundaries
- Mirror production structure in tests
- Use test helpers for common fixtures

### DON'T ❌

**Never generate summary or explanation documents unless explicitly requested.**

**Anti-Patterns**
- Mix try-catch with Result types
- Put side effects in pure functions
- Use exceptions for control flow
- Create artificial wrapper types
- Access mutable state in domain logic
- **Perform I/O operations in domain layer** (use infrastructure layer)
- Duplicate type definitions (use shared modules)

**Code Smells**
- Deeply nested if-else or match expressions
- Functions longer than one screen
- Mixed error handling paradigms
- Inline complex logic without names
- God objects or modules

**Testing**
- Test implementation details
- Use excessive mocking
- Ignore property-based testing opportunities
- Skip edge cases and boundary conditions

## Code Patterns

### Layer Separation: Domain vs Infrastructure

```fsharp
// ❌ BAD - I/O in domain
module ValidatedPath =
    let create (path: string) : Result<ValidatedPath, ValidationError> =
        if Directory.Exists(path) then  // ❌ I/O in domain!
            Ok (ValidatedPath path)
        else
            Error (PathNotFound path)

// ✅ GOOD - Pure domain, I/O in infrastructure
// Domain.fs
module ValidatedPath =
    let internal createUnchecked (path: string) : ValidatedPath =
        ValidatedPath path

// FileSystem.fs (Infrastructure)
let validatePath (path: string) : Result<ValidatedPath, ValidationError> =
    if Directory.Exists(path) then  // ✅ I/O in infrastructure
        Ok (ValidatedPath.createUnchecked path)
    else
        Error (PathNotFound path)
```

### Smart Constructor Pattern
```fsharp
type PostalCode = private PostalCode of string
    with
        static member Create(value: string) : Result<PostalCode, string> =
            if System.String.IsNullOrWhiteSpace(value) then
                Error "Postal code cannot be empty"
            else
                let cleaned = value.Trim().ToUpperInvariant()
                let pattern = System.Text.RegularExpressions.Regex(@"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$")
                if pattern.IsMatch(cleaned) then
                    let normalized =
                        if cleaned.Length = 6 then
                            cleaned.Insert(3, " ")
                        else
                            cleaned
                    Ok (PostalCode normalized)
                else
                    Error "Postal code must match format: A1A 1A1"

        static member Value (PostalCode code) = code

// Alternative: Email validation using proper library
type Email = private Email of string
    with
        static member Create(value: string) : Result<Email, string> =
            if System.String.IsNullOrWhiteSpace(value) then
                Error "Email cannot be empty"
            else
                try
                    let addr = System.Net.Mail.MailAddress(value)
                    // Use the normalized address from MailAddress
                    Ok (Email addr.Address)
                with
                | :? System.FormatException ->
                    Error "Invalid email format"
                | ex ->
                    Error (sprintf "Email validation failed: %s" ex.Message)

        static member Value (Email email) = email
```

### Railway-Oriented Pipeline
```fsharp
let processOrder (input: OrderInput) : Result<Order, OrderError> =
    validateInput input
    |> Result.bind loadCustomer
    |> Result.bind checkInventory
    |> Result.bind calculatePrice
    |> Result.bind createOrder
    |> Result.map enrichOrder
    |> Result.teeError (fun err -> Log.Error($"Order failed: {err}"))
    |> Result.tee (fun order -> Log.Information($"Order created: {order.Id}"))
```

### Pure Function with I/O at Boundary
```fsharp
// Pure - easily testable
let private calculateDiscount (total: decimal) (customerTier: Tier) : decimal =
    match customerTier with
    | Gold -> total * 0.9m
    | Silver -> total * 0.95m
    | Bronze -> total

// I/O boundary - wrapped in Result
let private loadCustomer (id: CustomerId) : Result<Customer, DbError> =
    try
        // Database access
        Ok customer
    with ex ->
        Error (DbError.LoadFailed ex.Message)
```

### Type-Safe DeletableItem Pattern
```fsharp
// Domain type captures what we know
type DeletableItem =
    | File of path: string
    | Directory of path: string

module DeletableItem =
    let path item =
        match item with
        | File path -> path
        | Directory path -> path

    let fromFile path = File path
    let fromDirectory path = Directory path

// Usage preserves type information
let items = [
    DeletableItem.fromFile "movie.srt"
    DeletableItem.fromDirectory "Empty Folder"
]
```

## Error Handling Reference

### Result Composition
```fsharp
// Transform success value
|> Result.map (fun x -> x + 1)

// Chain dependent operations
|> Result.bind nextOperation

// Transform error
|> Result.mapError formatError

// Side effect on error (logging)
|> Result.teeError logError

// Side effect on success (logging)
|> Result.tee logSuccess

// Provide default value
|> Result.defaultValue fallback
|> Result.defaultWith (fun error -> handleError error)
```

## Testing Guidelines

### Unit Tests with Unquote
```fsharp
[<Fact>]
let ``ValidatedPath combine works``() =
    let basePath = ValidatedPath.createUnchecked "C:\\base"
    let combined = ValidatedPath.combine basePath "sub"
    test <@ combined = "C:\\base\\sub" @>

[<Fact>]
let ``DeletableItem distinguishes File from Directory``() =
    let file = DeletableItem.File "path"
    let dir = DeletableItem.Directory "path"
    test <@ file <> dir @>
```

### Property-Based Tests with FsCheck
```fsharp
[<Property>]
let ``English subtitles are never deleted`` (NonNull filename) =
    let testFile = $"movie.eng.{filename}.srt"
    not (Subtitle.shouldDelete testFile)

[<Property>]
let ``Language detection is case insensitive`` (NonNull code) =
    let lower = $"movie.{code.ToLower()}.srt"
    let upper = $"movie.{code.ToUpper()}.srt"
    Subtitle.shouldDelete lower = Subtitle.shouldDelete upper
```

### Integration Tests with Test Helpers
```fsharp
[<Fact>]
let ``Movie without video - entire folder deleted``() =
    withTestDir (movieWithoutVideo "Test Movie") (fun testDir ->
        let result = Movies.clean testDir Preview

        match result with
        | Ok items ->
            let movieFolder = Path.Combine(testDir, "Test Movie")
            test <@ containsDirectory movieFolder items @>
        | Error _ ->
            failwith "Should have found folder"
    )
```

## Development Workflow

### Build and Run
```bash
# Build
dotnet build

# Run with arguments
dotnet run --project DirectoryCleaner.fsproj movies -p "Z:\Movies"

# Watch mode
dotnet watch run --project DirectoryCleaner.fsproj
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~SubtitleTests"

# Watch mode
dotnet watch test
```

## Common Pitfalls

### ❌ I/O in Domain Layer
```fsharp
// BAD
module ValidatedPath =
    let create path =
        if Directory.Exists(path) then  // ❌
            Ok (ValidatedPath path)
        else Error PathNotFound

// GOOD
// Domain.fs
module ValidatedPath =
    let internal createUnchecked path = ValidatedPath path

// FileSystem.fs
let validatePath path =
    if Directory.Exists(path) then  // ✅
        Ok (ValidatedPath.createUnchecked path)
    else Error PathNotFound
```

### ❌ Side Effects in Pure Functions
```fsharp
// BAD
let calculateTotal items =
    Log.Information("Calculating")  // ❌
    items |> List.sumBy (_.Price)

// GOOD
let processOrder order =
    order
    |> calculateTotal
    |> Result.tee (fun total -> Log.Information($"Total: {total}"))
```

### ❌ Duplicating Type Definitions
```fsharp
// BAD - Types in both Program.fs and Tests
type CliArguments = ...  // Program.fs
type CliArguments = ...  // Tests (duplicate!)

// GOOD - Shared module
// CliArguments.fs
type CliArguments = ...

// Program.fs
open CliArguments

// Tests.fs
open CliArguments
```

### ❌ Overly Complex Pipelines
```fsharp
// BAD - too much inline logic
data
|> List.filter (fun x -> x.Value > 10 && x.IsActive && not x.IsDeleted)
|> List.map (fun x -> { x with Value = x.Value * 1.1 })
|> List.groupBy (fun x -> x.Category)
|> List.map (fun (cat, items) -> cat, items |> List.sumBy (_.Value))

// GOOD - named functions
data
|> List.filter isValidAndActive
|> List.map applyMarkup
|> List.groupBy (_.Category)
|> List.map summarizeByCategory
```

## .NET 10 / F# 10 Features

### Use Appropriately
- **String interpolation** - `$"Value: {x}"` instead of `sprintf`
- **Collection expressions** - Unified syntax for lists, arrays, sequences
- **Enhanced type inference** - Less verbose code
- **Performance improvements** - Faster compilation and runtime

## Resources

### Essential Reading
- [F# for Fun and Profit](https://fsharpforfunandprofit.com) - Comprehensive F# guide
- [Domain Modeling Made Functional](https://pragprog.com/titles/swdddf/) - DDD in F#
- [Stylish F#](https://link.springer.com/book/10.1007/978-1-4842-7205-3) - Code style guide

### Library Documentation
- [FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling/)
- [Argu](https://fsprojects.github.io/Argu/)
- [Unquote](https://github.com/SwensenSoftware/unquote)
- [FsCheck](https://fscheck.github.io/FsCheck/)

## Key Principles Summary

1. **Separation of concerns** - Domain vs Infrastructure layers
2. **Pure core, impure shell** - Isolate side effects
3. **Railway-oriented programming** - Explicit error handling
4. **Type-driven design** - Make invalid states impossible
5. **DRY principle** - Share types, avoid duplication
6. **Testability** - Pure functions, property-based tests
7. **Readability** - Explicit piping, clear intent
8. **Small, composable functions** - Unix philosophy