﻿module Utility

// http://www.fssnip.net/4K
let partition condition values =     
    let pairs = seq {
        for i in values do
            if condition i then
                yield Some(i), None
            else
                yield None, Some(i) }

    pairs |> Seq.choose fst, pairs |> Seq.choose snd