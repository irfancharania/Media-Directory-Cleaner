F# Development Guide

> This document provides context for AI assistants (Claude, GitHub Copilot, etc.) working on this F# project. It defines architectural principles, coding standards, and project-specific patterns.

## Project Context

**Purpose:** Tool to help keep Kodi/XBMC media directories clean.

Using supplemental tools like Media Companion, users can download media meta content (such as artwork and subtitles) and store it locally. Kodi/XBMC won't scrape the internet if the information it needs is present locally alongside the media.

Unfortunately, when media is deleted from within the Kodi/XBMC interface, the local meta files are left behind on the file system. Hence, the need for this tool.

**Target Framework:** .NET 10 / F# 10

## Architectural Principles

### 1. Domain-Driven Design (DDD)
We follow tactical DDD patterns with clear layer separation:

- **Domain Layer** - Pure business logic, no infrastructure dependencies
- **Infrastructure Layer** - I/O operations, external service integration
- **Application Layer** - Workflow orchestration, use case coordination
- **Entry Point** - Minimal Program.fs that delegates to application layer

### 2. Railway-Oriented Programming (ROP)
Error handling follows Scott Wlaschin's Railway-Oriented Programming:

- Use `Result<'T, 'Error>` for all fallible operations
- Compose with `Result.bind`, `Result.map`, `Result.mapError`
- Never use exceptions for business logic control flow
- Side effects via `Result.tee` and `Result.teeError` at pipeline edges

### 3. Functional Core, Imperative Shell
- **Pure functions** - All domain logic, calculations, transformations
- **I/O at boundaries** - File, database, HTTP operations wrapped in Result
- **Side effects at edges** - Logging, telemetry only at pipeline endpoints

### 4. Type-Driven Design
- Make illegal states unrepresentable with constrained types
- Use single-case discriminated unions for type safety
- Smart constructors with validation return `Result<T, Error>`
- Private constructors prevent invalid instances

## Technology Stack

### Core Libraries
- **FsToolkit.ErrorHandling** - utility library to work with the Result type in F#, and allows you to do clear, simple and powerful error handling
- **Argu** - declarative CLI argument parser for F# console applications

### Testing
- **FsUnit** - F#-first unit testing framework
- **Unquote** - Write F# unit test assertions as quoted expressions, get step-by-step failure messages for free

## Coding Standards

### DO ✅

**Type Design**
- Use constrained types for domain primitives
- Single-case DUs for IDs and value objects
- Smart constructors with validation
- Explicit type annotations on public functions

**Function Design**
- Keep functions small (< 20 lines ideal)
- Single responsibility per function
- Pure functions for business logic
- Descriptive names over anonymous lambdas
- Prefer idiomatic F#
- Prefer explicit piping over computation expressions for readability
- Strive for functional core, imperative shell

**Error Handling**
- Return `Result<T, Error>` for operations that can fail
- Use computation expressions for readability
- Handle all error cases explicitly
- Never catch exceptions in domain logic

**Module Organization**
- One module per file (except where it makes sense like Domain)
- Public API at bottom of file
- Private helpers at top
- Clear separation of concerns

**Testing**
- Test pure functions without mocking
- Property-based tests for business invariants
- Integration tests for I/O boundaries
- Mirror production structure in tests

### DON'T ❌

Don't generate summary or explanation documents unless I ask for it.

**Anti-Patterns**
- Mix try-catch with Result types
- Put side effects in pure functions
- Use exceptions for control flow
- Create artificial wrapper types
- Access mutable state in domain logic

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

### Smart Constructor Pattern
```fsharp
type PostalCode = private PostalCode of string
    with
        static member Create(value: string) : Result<PostalCode, string> =
            if System.String.IsNullOrWhiteSpace(value) then
                Error "Postal code cannot be empty"
            else
                let cleaned = value.Trim().ToUpperInvariant()
                // Canadian postal code format: A1A 1A1
                let pattern = System.Text.RegularExpressions.Regex(@"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$")
                if pattern.IsMatch(cleaned) then
                    // Normalize format with space
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
    |> Result.teeError (fun err -> Log.Error("Order failed: {Error}", err))
    |> Result.tee (fun order -> Log.Information("Order created: {Id}", order.Id))
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

### Computation Expression Usage
```fsharp
let createLocation data = result {
    let! name = LocationName.Create data.Name
    let! population = Population.Create data.Pop
    let! coordinates = Coordinates.Create data.Lat data.Lon

    return {
        Id = LocationId.New()
        Name = name
        Population = population
        Coordinates = coordinates
    }
}
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

### Common Utilities
```fsharp
// Partition results (not in FsToolkit.ErrorHandling)
let partition (results: Result<'a, 'b> list) : 'a list * 'b list =
    let oks = results |> List.choose Result.toOption
    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
    (oks, errors)

// Test helper
let expectOk result msg =
    match result with
    | Ok value -> value
    | Error err -> failtestf "%s: %A" msg err
```

## Testing Guidelines


## Development Workflow

### Build and Run
```
# Restore packages
dotnet restore

# Build
dotnet build

# Run application
dotnet run --project [ProjectName]

# Run with arguments
dotnet run --project [ProjectName] -- [args]

# Watch mode
dotnet watch run --project [ProjectName]
```

### Testing
```
# Run all tests
dotnet run --project Tests/[ProjectName].Tests.fsproj

# Filter tests
dotnet run --project Tests/[ProjectName].Tests.fsproj -- --filter "[pattern]"

# Watch mode
dotnet watch --project Tests/[ProjectName].Tests.fsproj run

# With detailed output
dotnet run --project Tests/[ProjectName].Tests.fsproj -- --debug
```

### Code Quality
```
# Format code
dotnet fantomas [file or directory]

# Lint
dotnet fsharplint lint [ProjectName].sln
```

## Common Pitfalls

### ❌ Mixing Error Handling Paradigms
```fsharp
// BAD - mixing try-catch with Result
result {
    try
        let! value = someOperation()
        return value
    with ex ->
        return! Error ex.Message
}

// GOOD - isolate exceptions at I/O boundary
let someOperation() : Result<T, Error> =
    try
        // I/O operation
        Ok result
    with ex ->
        Error (formatException ex)
```

### ❌ Side Effects in Pure Functions
```fsharp
// BAD - logging in pure function
let calculateTotal items =
    Log.Information("Calculating total")  // ❌
    items |> List.sumBy (_.Price)

// GOOD - side effects at edges
let processOrder order =
    order
    |> calculateTotal
    |> applyDiscount
    |> Result.tee (fun total -> Log.Information("Total: {Total}", total))
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

## .NET 10 / F# 10 Specific Features

### Use When Appropriate
- **Collection expressions** - Unified syntax for lists, arrays, sequences
- **Discriminated union improvements** - Better pattern matching
- **Enhanced type inference** - Less verbose code
- **Performance improvements** - Faster compilation and runtime

### Migration Notes
- Test all Result/Option operations for behavioral changes

## Resources

### Essential Reading
- [F# for Fun and Profit](https://fsharpforfunandprofit.com) - Comprehensive F# guide
- [Domain Modeling Made Functional](https://pragprog.com/titles/swdddf/) - DDD in F#
- [Stylish F#](https://link.springer.com/book/10.1007/978-1-4842-7205-3) - Code style guide

### Library Documentation
- [FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling/)
- [Argu](https://fsprojects.github.io/Argu/)
- [Unquote](https://github.com/SwensenSoftware/unquote)


---

## Key Principles Summary

1. **Readability over cleverness** - Code is read more than written
2. **Pure core, impure shell** - Isolate side effects
3. **Railway-oriented programming** - Explicit error handling
4. **Type-driven design** - Make invalid states impossible
5. **Test the domain** - Pure functions are inherently testable
6. **Small, composable functions** - Unix philosophy
7. **Explicit over implicit** - Clear intent in code

---
