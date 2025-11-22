module Errors

open System

// ============================================================================
// Error Types - Rich, contextual errors
// ============================================================================

type ValidationError =
    | PathEmpty
    | PathNotFound of path: string
    | PathNotDirectory of path: string

type DirectoryError =
    | NoSubdirectories of path: string
    | NoLeafNodes of path: string
    | NoFilesFound of path: string
    | AccessDenied of path: string * exn: Exception

type CleaningError =
    | NothingToClean of reason: string
    | DeletionFailed of path: string * exn: Exception

type DomainError =
    | ValidationError of ValidationError
    | DirectoryError of DirectoryError
    | CleaningError of CleaningError

// ============================================================================
// Error Formatting
// ============================================================================

module DomainError =
    let toMessage error =
        match error with
        | ValidationError ve ->
            match ve with
            | PathEmpty -> "Path cannot be empty"
            | PathNotFound path -> $"Directory not found: {path}"
            | PathNotDirectory path -> $"Path is not a directory: {path}"
        
        | DirectoryError de ->
            match de with
            | NoSubdirectories path -> $"No subdirectories found in: {path}"
            | NoLeafNodes path -> $"No leaf nodes found in: {path}"
            | NoFilesFound path -> $"No files found in: {path}"
            | AccessDenied (path, ex) -> $"Access denied to: {path} ({ex.Message})"
        
        | CleaningError ce ->
            match ce with
            | NothingToClean reason -> $"Nothing to clean: {reason}"
            | DeletionFailed (path, ex) -> $"Failed to delete: {path} ({ex.Message})"
    
    /// Convert to an optional message (None for non-critical errors)
    let toOptionalMessage error =
        match error with
        | DirectoryError (NoSubdirectories _)
        | DirectoryError (NoLeafNodes _)
        | DirectoryError (NoFilesFound _)
        | CleaningError (NothingToClean _) -> 
            None  // Expected conditions, not errors to report
        | _ -> 
            Some (toMessage error)