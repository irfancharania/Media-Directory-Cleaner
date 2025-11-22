module Utility

// ============================================================================
// Sequence Utilities
// ============================================================================

/// Seq.partition for better naming consistency
/// Returns tuple of sequences matching F# List.partition signature
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
