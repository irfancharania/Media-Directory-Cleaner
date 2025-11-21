namespace MediaDirectoryCleaner.Tests

open Xunit
open FsCheck
open FsCheck.Xunit
open Domain

/// Property-based tests for subtitle language detection.
type SubtitlePropertyTests() =

    // ============================================================================
    // Language Code Lists
    // ============================================================================
    
    static let englishCodes = [| "english"; "eng"; "en" |]
    static let frenchCodes = [| "french"; "francais"; "fre"; "fra"; "fr"; "fr-ca"; "frc"; "canadien"; "quebec" |]
    static let deleteCodes = [| "spa"; "ger"; "ara"; "hin"; "por"; "ita"; "rus"; "chi"; "jpn"; "kor"; "dut"; "pol"; "swe" |]
    static let safePrefixes = [| "movie"; "film"; "video"; "show"; "episode"; "clip"; "media"; "title" |]
    static let forcedSuffixes = [| ".forced"; ".sdh"; ".hi"; ".cc" |]

    // ============================================================================
    // Property: English Subtitles Are Never Deleted
    // ============================================================================
    
    [<Fact>]
    member _.``English codes with dot separator are never deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``English codes with underscore separator are never deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let filename = $"{prefix}_{code}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``English codes with dash separator are never deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let filename = $"{prefix}-{code}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: French Subtitles Are Never Deleted
    // ============================================================================
    
    [<Fact>]
    member _.``French codes with dot separator are never deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = frenchCodes.[int codeIdx % frenchCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``French codes with underscore separator are never deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = frenchCodes.[int codeIdx % frenchCodes.Length]
            let filename = $"{prefix}_{code}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Delete Languages Are Always Deleted
    // ============================================================================
    
    [<Fact>]
    member _.``Delete codes with dot separator are always deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete codes with underscore separator are always deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}_{code}.srt"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete codes with dash separator are always deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}-{code}.srt"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Case Insensitivity
    // ============================================================================
    
    [<Fact>]
    member _.``English detection is case insensitive``() =
        let prop (prefixIdx: byte) (codeIdx: byte) (useUpper: bool) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let cased = if useUpper then code.ToUpperInvariant() else code.ToLowerInvariant()
            let filename = $"{prefix}.{cased}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete language detection is case insensitive``() =
        let prop (prefixIdx: byte) (codeIdx: byte) (useUpper: bool) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let cased = if useUpper then code.ToUpperInvariant() else code.ToLowerInvariant()
            let filename = $"{prefix}.{cased}.srt"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Uncertain Files Are Never Deleted
    // ============================================================================
    
    [<Fact>]
    member _.``Files with only year are uncertain and not deleted``() =
        let prop (prefixIdx: byte) (year: uint16) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let validYear = 1950 + (int year % 80)
            let filename = $"{prefix}.{validYear}.srt"
            Subtitle.isUncertain filename && not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Mutual Exclusivity
    // ============================================================================
    
    [<Fact>]
    member _.``English files are not uncertain``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            not (Subtitle.isUncertain filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``French files are not uncertain``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = frenchCodes.[int codeIdx % frenchCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            not (Subtitle.isUncertain filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete language files are not uncertain``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            not (Subtitle.isUncertain filename)
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Forced/SDH Suffixes Don't Change Language Decision
    // ============================================================================
    
    [<Fact>]
    member _.``English with forced suffix is still kept``() =
        let prop (prefixIdx: byte) (codeIdx: byte) (suffixIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let suffix = forcedSuffixes.[int suffixIdx % forcedSuffixes.Length]
            let filename = $"{prefix}.{code}{suffix}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``French with forced suffix is still kept``() =
        let prop (prefixIdx: byte) (codeIdx: byte) (suffixIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = frenchCodes.[int codeIdx % frenchCodes.Length]
            let suffix = forcedSuffixes.[int suffixIdx % forcedSuffixes.Length]
            let filename = $"{prefix}.{code}{suffix}.srt"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete language with forced suffix is still deleted``() =
        let prop (prefixIdx: byte) (codeIdx: byte) (suffixIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let suffix = forcedSuffixes.[int suffixIdx % forcedSuffixes.Length]
            let filename = $"{prefix}.{code}{suffix}.srt"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Both Extensions Work
    // ============================================================================
    
    [<Fact>]
    member _.``English works with sub extension``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = englishCodes.[int codeIdx % englishCodes.Length]
            let filename = $"{prefix}.{code}.sub"
            not (Subtitle.shouldDelete filename)
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``Delete codes work with sub extension``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}.{code}.sub"
            Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop

    // ============================================================================
    // Property: Consistency (Determinism)
    // ============================================================================
    
    [<Fact>]
    member _.``shouldDelete returns same result when called twice``() =
        let prop (prefixIdx: byte) (codeIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let code = deleteCodes.[int codeIdx % deleteCodes.Length]
            let filename = $"{prefix}.{code}.srt"
            Subtitle.shouldDelete filename = Subtitle.shouldDelete filename
        Check.QuickThrowOnFailure prop
    
    [<Fact>]
    member _.``isUncertain returns same result when called twice``() =
        let prop (prefixIdx: byte) =
            let prefix = safePrefixes.[int prefixIdx % safePrefixes.Length]
            let filename = $"{prefix}.2020.srt"
            Subtitle.isUncertain filename = Subtitle.isUncertain filename
        Check.QuickThrowOnFailure prop