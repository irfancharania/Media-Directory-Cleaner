module Utility

// http://www.fssnip.net/4K
// Partition that returns two sequences
let partition condition values = 
    let pairs = 
        seq { 
            for i in values do
                if condition i then yield Some(i), None
                else yield None, Some(i)
        }
    pairs |> Seq.choose fst, pairs |> Seq.choose snd

// Additional Seq.partition for better naming consistency
// Returns tuple of sequences matching F# List.partition signature
module Seq =
    let partition predicate source =
        let pairs = 
            seq { 
                for item in source do
                    if predicate item then yield Some(item), None
                    else yield None, Some(item)
            }
        pairs |> Seq.choose fst, pairs |> Seq.choose snd

type LocalDateTime = 
    | LocalDateTime of System.DateTime

let formatLocalDateTime formatString (LocalDateTime dt) = dt.ToString(format = formatString)