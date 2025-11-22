namespace MediaDirectoryCleaner.Tests.Unit

open Xunit
open Swensen.Unquote
open Domain
open Size

/// Edge case and specific behavior tests for subtitle language detection.
/// General patterns are covered by property-based tests in SubtitlePropertyTests.fs
module SubtitleTests =

    // ============================================================================
    // isSubtitleFile Tests
    // ============================================================================

    [<Fact>]
    let ``isSubtitleFile detects srt files``() =
        let file = { FullPath = "test.srt"; Name = "test.srt"; Extension = ".srt"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>
    
    [<Fact>]
    let ``isSubtitleFile detects sub files``() =
        let file = { FullPath = "test.sub"; Name = "test.sub"; Extension = ".sub"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>

    [<Fact>]
    let ``isSubtitleFile detects ass files``() =
        let file = { FullPath = "test.ass"; Name = "test.ass"; Extension = ".ass"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>

    [<Fact>]
    let ``isSubtitleFile detects ssa files``() =
        let file = { FullPath = "test.ssa"; Name = "test.ssa"; Extension = ".ssa"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>

    [<Fact>]
    let ``isSubtitleFile detects vtt files``() =
        let file = { FullPath = "test.vtt"; Name = "test.vtt"; Extension = ".vtt"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>

    [<Fact>]
    let ``isSubtitleFile detects sbv files``() =
        let file = { FullPath = "test.sbv"; Name = "test.sbv"; Extension = ".sbv"; SizeInBytes = 1000L<byte> }
        test <@ Subtitle.isSubtitleFile file @>
    
    [<Fact>]
    let ``isSubtitleFile rejects non-subtitle files``() =
        let file = { FullPath = "test.txt"; Name = "test.txt"; Extension = ".txt"; SizeInBytes = 1000L<byte> }
        test <@ not (Subtitle.isSubtitleFile file) @>

    [<Fact>]
    let ``isSubtitleFile rejects video files``() =
        let file = { FullPath = "test.mp4"; Name = "test.mp4"; Extension = ".mp4"; SizeInBytes = 1000L<byte> }
        test <@ not (Subtitle.isSubtitleFile file) @>

    // ============================================================================
    // matchesVideoFile Tests
    // ============================================================================

    [<Fact>]
    let ``matchesVideoFile returns true when subtitle matches video exactly``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie.2024.1080p.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.1080p.mp4"
            Name = "Movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``matchesVideoFile returns false when subtitle does not match any video``() =
        let subtitlePath = "V:\\Movies\\Test\\Different.Name.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.1080p.mp4"
            Name = "Movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ not (Subtitle.matchesVideoFile subtitlePath files) @>

    [<Fact>]
    let ``matchesVideoFile is case insensitive``() =
        let subtitlePath = "V:\\Movies\\Test\\MOVIE.2024.1080P.srt"
        let videoFile = { 
            FullPath = "V:\\Movies\\Test\\movie.2024.1080p.mp4"
            Name = "movie.2024.1080p.mp4"
            Extension = ".mp4"
            SizeInBytes = 2000000000L<byte>
        }
        let files = [videoFile]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``matchesVideoFile ignores non-video files``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie.2024.srt"
        let imageFile = { 
            FullPath = "V:\\Movies\\Test\\Movie.2024.jpg"
            Name = "Movie.2024.jpg"
            Extension = ".jpg"
            SizeInBytes = 500000L<byte>
        }
        let files = [imageFile]
        test <@ not (Subtitle.matchesVideoFile subtitlePath files) @>
    
    [<Fact>]
    let ``matchesVideoFile works with multiple videos``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie2.srt"
        let video1 = { 
            FullPath = "V:\\Movies\\Test\\Movie1.mp4"
            Name = "Movie1.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000000000L<byte>
        }
        let video2 = { 
            FullPath = "V:\\Movies\\Test\\Movie2.mp4"
            Name = "Movie2.mp4"
            Extension = ".mp4"
            SizeInBytes = 1000000000L<byte>
        }
        let files = [video1; video2]
        test <@ Subtitle.matchesVideoFile subtitlePath files @>

    [<Fact>]
    let ``matchesVideoFile returns false for empty file list``() =
        let subtitlePath = "V:\\Movies\\Test\\Movie.srt"
        test <@ not (Subtitle.matchesVideoFile subtitlePath []) @>

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

    [<Fact>]
    let ``isUncertain returns true when no language code detected``() =
        test <@ Subtitle.isUncertain "movie.srt" @>

    [<Fact>]
    let ``isUncertain returns false when English detected``() =
        test <@ not (Subtitle.isUncertain "movie.eng.srt") @>

    [<Fact>]
    let ``isUncertain returns false when other language detected``() =
        test <@ not (Subtitle.isUncertain "movie.spa.srt") @>
    
    [<Fact>]
    let ``isUncertain returns false when French detected``() =
        test <@ not (Subtitle.isUncertain "movie.fre.srt") @>

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