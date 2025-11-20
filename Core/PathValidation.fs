module PathValidation

open System
open System.IO
open Domain

// ============================================================================
// Path Validation - Infrastructure Layer (I/O Operations)
// ============================================================================

/// Validate a path string by checking the file system
/// This is where I/O happens - in the infrastructure layer
let validate (path: string) : Result<ValidatedPath, ValidationError> =
    if String.IsNullOrWhiteSpace(path) then
        Error PathEmpty
    elif not (Directory.Exists(path)) then
        if File.Exists(path) then
            Error (PathNotDirectory path)
        else
            Error (PathNotFound path)
    else
        Ok (ValidatedPath.createUnchecked path)