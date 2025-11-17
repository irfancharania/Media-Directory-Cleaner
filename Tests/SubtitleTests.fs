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
        Subtitle.shouldDelete "Movie.2025.1080p.WEBRip.x264.AAC5.1-[YTS.MX].srt" |> should be False

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