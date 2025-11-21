namespace MediaDirectoryCleaner.Tests

open Xunit
open Swensen.Unquote
open Domain

/// Edge case and specific behavior tests for subtitle language detection.
/// General patterns are covered by property-based tests in SubtitlePropertyTests.fs
module SubtitleTests =

    // ============================================================================
    // Uncertain/Ambiguous Cases - Conservative Behavior
    // ============================================================================

    [<Fact>]
    let ``Plain filename without language code is uncertain``() =
        test <@ Subtitle.isUncertain "movie.srt" @>
        test <@ not (Subtitle.shouldDelete "movie.srt") @>

    [<Fact>]
    let ``Filename with only year is uncertain``() =
        test <@ Subtitle.isUncertain "Movie.1996.srt" @>
        test <@ not (Subtitle.shouldDelete "Movie.1996.srt") @>

    [<Fact>]
    let ``Filename with quality info but no language is uncertain``() =
        test <@ Subtitle.isUncertain "Movie.2025.1080p.WEBRip.x264.srt" @>
        test <@ not (Subtitle.shouldDelete "Movie.2025.1080p.WEBRip.x264.srt") @>

    [<Fact>]
    let ``Generic subs filename is uncertain``() =
        test <@ Subtitle.isUncertain "subs.srt" @>
        test <@ not (Subtitle.shouldDelete "subs.srt") @>

    // ============================================================================
    // Numbered Prefix Patterns (Real-World Edge Cases)
    // ============================================================================

    [<Fact>]
    let ``Numbered prefix with Russian is deleted``() =
        test <@ Subtitle.shouldDelete "4_Russian.srt" @>

    [<Fact>]
    let ``Numbered prefix with English is kept``() =
        test <@ not (Subtitle.shouldDelete "5_English.srt") @>

    [<Fact>]
    let ``Numbered prefix with French is kept``() =
        test <@ not (Subtitle.shouldDelete "8_French.srt") @>

    // ============================================================================
    // Language Code Position Edge Cases
    // ============================================================================

    [<Fact>]
    let ``Language code at very start of filename``() =
        test <@ Subtitle.shouldDelete "spa.Movie.2024.srt" @>
        test <@ not (Subtitle.shouldDelete "eng.Movie.2024.srt") @>

    [<Fact>]
    let ``Language code with multiple dots before it``() =
        test <@ Subtitle.shouldDelete "Movie.Name.2024.1080p.spa.srt" @>
        test <@ not (Subtitle.shouldDelete "Movie.Name.2024.1080p.eng.srt") @>

    [<Fact>]
    let ``Bare language code as entire filename``() =
        test <@ not (Subtitle.shouldDelete "eng.srt") @>
        test <@ Subtitle.shouldDelete "spa.srt" @>

    // ============================================================================
    // isUncertain vs shouldDelete Relationship
    // ============================================================================

    [<Fact>]
    let ``English files are not uncertain``() =
        test <@ not (Subtitle.isUncertain "movie.eng.srt") @>

    [<Fact>]
    let ``French files are not uncertain``() =
        test <@ not (Subtitle.isUncertain "movie.fre.srt") @>

    [<Fact>]
    let ``Delete language files are not uncertain``() =
        test <@ not (Subtitle.isUncertain "movie.spa.srt") @>

    // ============================================================================
    // Regional Variant Edge Cases
    // ============================================================================

    [<Fact>]
    let ``Canadian French fr-ca is kept``() =
        test <@ not (Subtitle.shouldDelete "movie.fr-ca.srt") @>

    [<Fact>]
    let ``Brazilian Portuguese pt-br is deleted``() =
        test <@ Subtitle.shouldDelete "movie.pt-br.srt" @>

    [<Fact>]
    let ``Quebec French variant is kept``() =
        test <@ not (Subtitle.shouldDelete "movie.quebec.srt") @>

    // ============================================================================
    // Accessibility Tag Combinations
    // ============================================================================

    [<Fact>]
    let ``SDH prefix with language code``() =
        test <@ not (Subtitle.shouldDelete "SDH.eng.srt") @>
        test <@ not (Subtitle.shouldDelete "SDH.fre.srt") @>
        test <@ Subtitle.shouldDelete "SDH.ger.srt" @>

    [<Fact>]
    let ``Multiple accessibility tags with English``() =
        test <@ not (Subtitle.shouldDelete "movie.eng.SDH.HI.srt") @>

    [<Fact>]
    let ``Hearing impaired suffix``() =
        test <@ not (Subtitle.shouldDelete "SDH.eng.HI.srt") @>
        test <@ Subtitle.shouldDelete "SDH.ger.HI.srt" @>

    // ============================================================================
    // Mixed Separator Edge Cases
    // ============================================================================

    [<Fact>]
    let ``Language code with trailing underscore before forced``() =
        test <@ Subtitle.shouldDelete "subtitle_ara_forced.srt" @>
        test <@ not (Subtitle.shouldDelete "subtitle_eng_forced.srt") @>

    [<Fact>]
    let ``Dash separator before extension``() =
        test <@ Subtitle.shouldDelete "movie-ger.srt" @>
        test <@ not (Subtitle.shouldDelete "movie-eng.srt") @>