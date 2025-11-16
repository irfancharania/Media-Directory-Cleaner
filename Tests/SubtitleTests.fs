module SubtitleTests

open Expecto
open Expecto.Flip
open Domain

let englishSubtitleTests =
    testList "English Subtitle Detection" [
        
        testList "Obvious English indicators" [
            test "English.srt is English" {
                let result = Subtitle.isNonEnglish "English.srt"
                Expect.isFalse "" result
            }
            
            test "english.srt is English (case insensitive)" {
                let result = Subtitle.isNonEnglish "english.srt"
                Expect.isFalse "" result
            }
            
            test "Movie.eng.srt is English" {
                let result = Subtitle.isNonEnglish "Movie.eng.srt"
                Expect.isFalse "" result
            }
            
            test "Movie.en.srt is English" {
                let result = Subtitle.isNonEnglish "Movie.en.srt"
                Expect.isFalse "" result
            }
            
            test "SDH.eng.HI.srt is English (hearing impaired)" {
                let result = Subtitle.isNonEnglish "SDH.eng.HI.srt"
                Expect.isFalse "" result
            }
        ]
        
        testList "Common non-English patterns" [
            test "spa.srt is NOT English (Spanish)" {
                let result = Subtitle.isNonEnglish "spa.srt"
                Expect.isTrue "" result
            }
            
            test "fre.srt is NOT English (French)" {
                let result = Subtitle.isNonEnglish "fre.srt"
                Expect.isTrue "" result
            }
            
            test "ger.srt is NOT English (German)" {
                let result = Subtitle.isNonEnglish "ger.srt"
                Expect.isTrue "" result
            }
            
            test "Movie.fre.srt is NOT English" {
                let result = Subtitle.isNonEnglish "Movie.fre.srt"
                Expect.isTrue "" result
            }
        ]
        
        testList "Real-world examples from provided file list" [
            test "ara.srt is NOT English (Arabic)" {
                let result = Subtitle.isNonEnglish "ara.srt"
                Expect.isTrue "" result
            }
            
            test "baq.srt is NOT English (Basque)" {
                let result = Subtitle.isNonEnglish "baq.srt"
                Expect.isTrue "" result
            }
            
            test "hin.srt is NOT English (Hindi)" {
                let result = Subtitle.isNonEnglish "hin.srt"
                Expect.isTrue "" result
            }
            
            test "kan.srt is NOT English (Kannada)" {
                let result = Subtitle.isNonEnglish "kan.srt"
                Expect.isTrue "" result
            }
            
            test "English.srt is English" {
                let result = Subtitle.isNonEnglish "English.srt"
                Expect.isFalse "" result
            }
            
            test "Playdate.2025.1080p.WEBRip.x264.AAC5.1-[YTS.MX].srt is uncertain - keep it" {
                let result = Subtitle.isNonEnglish "Playdate.2025.1080p.WEBRip.x264.AAC5.1-[YTS.MX].srt"
                Expect.isFalse "" result  // No language code = keep it
            }
        ]
        
        testList "Ambiguous cases - err on side of caution" [
            test "movie.srt without language code - keep it (uncertain)" {
                let result = Subtitle.isNonEnglish "movie.srt"
                Expect.isFalse "" result  // Don't delete if unsure
            }
            
            test "subs.srt without language code - keep it (uncertain)" {
                let result = Subtitle.isNonEnglish "subs.srt"
                Expect.isFalse "" result
            }
            
            test "Space.Jam.1996.srt - keep it (no language indicator)" {
                let result = Subtitle.isNonEnglish "Space.Jam.1996.srt"
                Expect.isFalse "" result
            }
        ]
        
        testList "Various filename patterns" [
            test "movie_spa.srt with underscore" {
                let result = Subtitle.isNonEnglish "movie_spa.srt"
                Expect.isTrue "" result
            }
            
            test "movie-fre.srt with dash" {
                let result = Subtitle.isNonEnglish "movie-fre.srt"
                Expect.isTrue "" result
            }
            
            test "movie.ger.forced.srt with multiple dots" {
                let result = Subtitle.isNonEnglish "movie.ger.forced.srt"
                Expect.isTrue "" result
            }
            
            test "movie.eng.forced.srt is English" {
                let result = Subtitle.isNonEnglish "movie.eng.forced.srt"
                Expect.isFalse "" result
            }
            
            test "subtitle_eng.srt with underscore is English" {
                let result = Subtitle.isNonEnglish "subtitle_eng.srt"
                Expect.isFalse "" result
            }
            
            test "subtitle_ara_forced.srt is NOT English" {
                let result = Subtitle.isNonEnglish "subtitle_ara_forced.srt"
                Expect.isTrue "" result
            }
            
            test "eng.srt at start is English" {
                let result = Subtitle.isNonEnglish "eng.srt"
                Expect.isFalse "" result
            }
            
            test "english.srt at start is English" {
                let result = Subtitle.isNonEnglish "english.srt"
                Expect.isFalse "" result
            }
            
            test "movie_eng_forced.srt with underscores is English" {
                let result = Subtitle.isNonEnglish "movie_eng_forced.srt"
                Expect.isFalse "" result
            }
            
            test "movie_spa_forced.srt with underscores is NOT English" {
                let result = Subtitle.isNonEnglish "movie_spa_forced.srt"
                Expect.isTrue "" result
            }
        ]
    ]

[<Tests>]
let tests = englishSubtitleTests