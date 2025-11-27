module Utility

open FsToolkit.ErrorHandling
open Errors

// ============================================================================
// Sequence Utilities
// ============================================================================

module Seq =
    /// Partition a sequence based on a predicate
    /// Returns (matching, not matching) sequences
    let partition predicate source =
        let pairs = 
            seq { 
                for item in source do
                    if predicate item then yield Some(item), None
                    else yield None, Some(item)
            }
        pairs |> Seq.choose fst, pairs |> Seq.choose snd

// ============================================================================
// Result Extensions
// ============================================================================

module Result =
    /// Lift a validation error to a domain error
    let liftValidationError result =
        result |> Result.mapError ValidationError
    
    /// Lift a directory error to a domain error
    let liftDirectoryError result =
        result |> Result.mapError DirectoryError
    
    /// Lift a cleaning error to a domain error
    let liftCleaningError result =
        result |> Result.mapError CleaningError
