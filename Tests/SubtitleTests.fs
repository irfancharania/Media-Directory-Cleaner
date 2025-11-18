namespace MediaDirectoryCleaner.Tests

open Xunit
open FsUnit.Xunit
open Domain

module SubtitleTests =

    // ============================================================================
    // English Subtitle Tests
    // ============================================================================

    [<Fact>]
    let ``English.srt should be kept``() =
        Subtitle.shouldDelete "English.srt" |> should be False

    [<Fact>]
    let ``english.srt should be kept (case insensitive)``() =
        Subtitle.shouldDelete "english.srt" |> should be False

    [<Fact>]
    let ``Movie.eng.srt should be kept``() =
        Subtitle.shouldDelete "Movie.eng.srt" |> should be False

    [<Fact>]
    let ``Movie.en.srt should be kept``() =
        Subtitle.shouldDelete "Movie.en.srt" |> should be False

    [<Fact>]
    let ``SDH.eng.HI.srt should be kept (hearing impaired)``() =
        Subtitle.shouldDelete "SDH.eng.HI.srt" |> should be False

    // ============================================================================
    // French Subtitle Tests
    // ============================================================================

    [<Fact>]
    let ``fre.srt should be kept (French)``() =
        Subtitle.shouldDelete "fre.srt" |> should be False

    [<Fact>]
    let ``French.srt should be kept``() =
        Subtitle.shouldDelete "French.srt" |> should be False

    [<Fact>]
    let ``Movie.fra.srt should be kept``() =
        Subtitle.shouldDelete "Movie.fra.srt" |> should be False

    [<Fact>]
    let ``SDH.fre.srt should be kept``() =
        Subtitle.shouldDelete "SDH.fre.srt" |> should be False

    [<Fact>]
    let ``fr-ca.srt should be kept (Canadian French)``() =
        Subtitle.shouldDelete "fr-ca.srt" |> should be False

    // ============================================================================
    // Non-English/French Tests
    // ============================================================================

    [<Fact>]
    let ``spa.srt should be deleted (Spanish)``() =
        Subtitle.shouldDelete "spa.srt" |> should be True

    [<Fact>]
    let ``ger.srt should be deleted (German)``() =
        Subtitle.shouldDelete "ger.srt" |> should be True

    [<Fact>]
    let ``Movie.ger.srt should be deleted``() =
        Subtitle.shouldDelete "Movie.ger.srt" |> should be True

    [<Fact>]
    let ``ara.srt should be deleted (Arabic)``() =
        Subtitle.shouldDelete "ara.srt" |> should be True

    [<Fact>]
    let ``baq.srt should be deleted (Basque)``() =
        Subtitle.shouldDelete "baq.srt" |> should be True

    [<Fact>]
    let ``hin.srt should be deleted (Hindi)``() =
        Subtitle.shouldDelete "hin.srt" |> should be True

    [<Fact>]
    let ``kan.srt should be deleted (Kannada)``() =
        Subtitle.shouldDelete "kan.srt" |> should be True

    [<Fact>]
    let ``por.srt should be deleted (Portuguese)``() =
        Subtitle.shouldDelete "por.srt" |> should be True

    [<Fact>]
    let ``ita.srt should be deleted (Italian)``() =
        Subtitle.shouldDelete "ita.srt" |> should be True

    // ============================================================================
    // Real-World Examples
    // ============================================================================

    [<Fact>]
    let ``Brazilian.por.srt should be deleted``() =
        Subtitle.shouldDelete "Brazilian.por.srt" |> should be True

    [<Fact>]
    let ``Latin American.spa.srt should be deleted``() =
        Subtitle.shouldDelete "Latin American.spa.srt" |> should be True

    [<Fact>]
    let ``SDH.ger.HI.srt should be deleted (German even with SDH)``() =
        Subtitle.shouldDelete "SDH.ger.HI.srt" |> should be True

    [<Fact>]
    let ``SDH.spa.HI.srt should be deleted (Spanish even with SDH)``() =
        Subtitle.shouldDelete "SDH.spa.HI.srt" |> should be True

    [<Fact>]
    let ``4_Russian.srt should be deleted``() =
        Subtitle.shouldDelete "4_Russian.srt" |> should be True

    [<Fact>]
    let ``5_English.srt should be kept``() =
        Subtitle.shouldDelete "5_English.srt" |> should be False

    [<Fact>]
    let ``8_French.srt should be kept``() =
        Subtitle.shouldDelete "8_French.srt" |> should be False

    // ============================================================================
    // Ambiguous Cases - Conservative (Keep)
    // ============================================================================

    [<Fact>]
    let ``movie.srt without language code should be kept (uncertain)``() =
        Subtitle.shouldDelete "movie.srt" |> should be False

    [<Fact>]
    let ``subs.srt without language code should be kept``() =
        Subtitle.shouldDelete "subs.srt" |> should be False

    [<Fact>]
    let ``Movie.1996.srt should be kept (no language indicator)``() =
        Subtitle.shouldDelete "Movie.1996.srt" |> should be False

    [<Fact>]
    let ``Movie.2025.1080p.WEBRip.srt should be kept (uncertain)``() =
        Subtitle.shouldDelete "Movie.2025.1080p.WEBRip.x264.srt" |> should be False

    // ============================================================================
    // Various Filename Patterns
    // ============================================================================

    [<Fact>]
    let ``movie_spa.srt with underscore should be deleted``() =
        Subtitle.shouldDelete "movie_spa.srt" |> should be True

    [<Fact>]
    let ``movie-ger.srt with dash should be deleted``() =
        Subtitle.shouldDelete "movie-ger.srt" |> should be True

    [<Fact>]
    let ``movie.ger.forced.srt should be deleted``() =
        Subtitle.shouldDelete "movie.ger.forced.srt" |> should be True

    [<Fact>]
    let ``movie.eng.forced.srt should be kept``() =
        Subtitle.shouldDelete "movie.eng.forced.srt" |> should be False

    [<Fact>]
    let ``subtitle_eng.srt with underscore should be kept``() =
        Subtitle.shouldDelete "subtitle_eng.srt" |> should be False

    [<Fact>]
    let ``subtitle_ara_forced.srt should be deleted``() =
        Subtitle.shouldDelete "subtitle_ara_forced.srt" |> should be True

    [<Fact>]
    let ``eng.srt at start should be kept``() =
        Subtitle.shouldDelete "eng.srt" |> should be False

    [<Fact>]
    let ``movie_eng_forced.srt with underscores should be kept``() =
        Subtitle.shouldDelete "movie_eng_forced.srt" |> should be False

    [<Fact>]
    let ``movie_spa_forced.srt with underscores should be deleted``() =
        Subtitle.shouldDelete "movie_spa_forced.srt" |> should be True

    // ============================================================================
    // Edge Cases with Multiple Language Indicators
    // ============================================================================

    [<Fact>]
    let ``English with forced flag should be kept``() =
        Subtitle.shouldDelete "movie.eng.forced.srt" |> should be False

    [<Fact>]
    let ``French with forced flag should be kept``() =
        Subtitle.shouldDelete "movie.fre.forced.srt" |> should be False

    [<Fact>]
    let ``SDH.eng should be kept (English accessibility)``() =
        Subtitle.shouldDelete "SDH.eng.srt" |> should be False

    [<Fact>]
    let ``SDH.fre should be kept (French accessibility)``() =
        Subtitle.shouldDelete "SDH.fre.srt" |> should be False

    [<Fact>]
    let ``eng.SDH.HI should be kept (English with multiple accessibility tags)``() =
        Subtitle.shouldDelete "movie.eng.SDH.HI.srt" |> should be False

    [<Fact>]
    let ``Movie title with language-like words should be kept when uncertain``() =
        // "Rush" contains "rus" (Russian) but as part of a word
        Subtitle.shouldDelete "Rush.2013.BluRay.srt" |> should be False

    [<Fact>]
    let ``Multiple dots with language code should be detected``() =
        Subtitle.shouldDelete "Movie.Name.2024.1080p.spa.srt" |> should be True

    [<Fact>]
    let ``Language code at start of filename should be detected``() =
        Subtitle.shouldDelete "spa.Movie.2024.srt" |> should be True

    [<Fact>]
    let ``Simplified Chinese variants should be deleted``() =
        Subtitle.shouldDelete "Simplified.chi.srt" |> should be True

    [<Fact>]
    let ``Traditional Chinese variants should be deleted``() =
        Subtitle.shouldDelete "Traditional.chi.srt" |> should be True

    [<Fact>]
    let ``Brazilian Portuguese should be deleted``() =
        Subtitle.shouldDelete "Brazilian.por.srt" |> should be True

    [<Fact>]
    let ``Latin American Spanish should be deleted``() =
        Subtitle.shouldDelete "Latin American.spa.srt" |> should be True
