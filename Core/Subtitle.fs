module Subtitle

open System.IO
open Domain

// ============================================================================
// Subtitle Language Detection
// ============================================================================

/// ISO 639-2/3 language codes that should be DELETED (not kept)
/// Keeps English and French (including Canadian French)
/// Source: https://www.opensubtitles.org/
let private languagesToDelete = [
    // Arabic
    "ara"; "arb"; "ar"; "arabic" 
    // Asian languages
    "chi"; "zho"; "zh"; "cmn"; "yue"; "chinese"
    "jpn"; "ja"; "jp"; "japanese" 
    "kor"; "ko"; "kr"; "korean" 
    "tha"; "th"; "thai"
    "vie"; "vi"; "vietnamese"
    "hin"; "hi"; "hindi"
    "kan"; "kn"; "kannada"
    "mal"; "ml"; "malayalam"
    "tam"; "ta"; "tamil"
    "tel"; "te"; "telugu"
    "ben"; "bn"; "bengali"
    "mar"; "marathi"
    "pan"; "pa"; "punjabi"
    // European languages (excluding English and French)
    "spa"; "es"; "esp"; "spanish"
    "por"; "pt"; "pt-br"; "portuguese"
    "ger"; "deu"; "de"; "german"
    "ita"; "italian"
    "dut"; "nld"; "nl"; "dutch"
    "pol"; "pl"; "polish"
    "rus"; "ru"; "russian"
    "ukr"; "ukrainian"
    "cze"; "ces"; "cs"; "czech"
    "swe"; "sv"; "swedish"
    "dan"; "da"; "danish"
    "nor"; "no"; "nob"; "nno"; "norwegian"
    "fin"; "fi" ; "finnish"
    "gre"; "ell"; "el"; "greek"
    "tur"; "tr"; "turkish"
    "hun"; "hu"; "hungarian"
    "rum"; "ron"; "ro"; "romanian"
    "bul"; "bg"; "bulgarian"
    "hrv"; "hr"; "croatian"
    "srp"; "sr"; "serbian"
    "slv"; "sl"; "slovenian"
    "slo"; "slk"; "sk"; "slovak"
    "bos"; "bs"; "bosnian"
    "mac"; "mkd"; "mk"; "macedonian"
    "alb"; "sqi"; "sq"; "albanian"
    "est"; "et"; "estonian"
    "lav"; "lv"; "latvian"
    "lit"; "lt"; "lithuanian"
    "ice"; "isl"; "is"; "icelandic"
    // Other European
    "baq"; "eus"; "eu"; "basque"
    "cat"; "ca"; "catalan"
    "glg"; "gl"; "galician"
    "arm"; "hye"; "hy"; "armenian"
    "aze"; "az"; "azeri"; "azerbaijani"
    "khm"; "km"; "khmer"
    "kaz"; "kk"; "kazakh"
    "kir"; "ky"; "kyrgyz"
    "geo"; "kat"; "ka"; "georgian"
    // Middle Eastern
    "heb"; "hebrew"
    "per"; "fas"; "fa"; "persian"; "farsi"
    // Southeast Asian
    "may"; "msa"; "ms"; "malay"
    "ind"; "id"; "indonesian"
    "fil"; "tl"; "filipino"; "tagalog"
    // Other
    "swa"; "sw"; "swahili"
]

/// Language indicators to KEEP (English and French variants)
let private languagesToKeep = [
    // English variants
    "english"; "eng"; "en"
    // French variants
    "fra"; "fre"; "fr"; "french"; "francais"; "français"
    // Canadian French variants
    "fr-ca"; "frc"; "frca"; "french-canadian"; "canadien"; "quebec"; "québec"
]

/// Check if filename contains a language code from the given list
let private containsLanguageCode (codes: string list) (filename: string) =
    let lower = filename.ToLowerInvariant()
    codes
    |> List.exists (fun code -> 
        lower.Contains($".{code}.") || 
        lower.Contains($"_{code}_") || 
        lower.Contains($".{code}_") || 
        lower.Contains($"_{code}.") || 
        lower.Contains($"-{code}.") ||
        lower.Contains($"-{code}_") ||
        lower.EndsWith($".{code}.srt") ||
        lower.EndsWith($"_{code}.srt") ||
        lower.EndsWith($".{code}.sub") ||
        lower.EndsWith($"_{code}.sub") ||
        lower.StartsWith($"{code}.") ||
        lower.StartsWith($"{code}_") ||
        lower = $"{code}.srt" ||
        lower = $"{code}.sub")

/// Determine if a subtitle file should be deleted
/// Returns true if confident it should be DELETED (not English/French)
/// Returns false if English/French OR uncertain (err on side of caution)
let shouldDelete (filename: string) : bool =
    let hasLanguageToKeep = containsLanguageCode languagesToKeep filename
    let hasLanguageToDelete = containsLanguageCode languagesToDelete filename
    
    match hasLanguageToKeep, hasLanguageToDelete with
    | true, _ -> false      // Explicitly English/French - keep
    | false, true -> true   // Has other language code - delete
    | false, false -> false // Uncertain - keep (safe default)

/// Check if a subtitle file's language is uncertain (no recognizable language code)
let isUncertain (filename: string) : bool =
    let hasLanguageToKeep = containsLanguageCode languagesToKeep filename
    let hasLanguageToDelete = containsLanguageCode languagesToDelete filename
    
    match hasLanguageToKeep, hasLanguageToDelete with
    | false, false -> true  // No language code detected
    | _ -> false

/// Check if file is a subtitle by extension
let isSubtitleFile (file: ExistingFile) : bool =
    ExistingFile.classifyMediaType file = Subtitle

/// Check if subtitle filename matches a video file in the same directory
let matchesVideoFile (subtitlePath: string) (dirFiles: seq<ExistingFile>) : bool =
    let subtitleBase = Path.GetFileNameWithoutExtension(subtitlePath).ToLowerInvariant()
    
    dirFiles
    |> Seq.filter (fun f -> ExistingFile.classifyMediaType f = Video)
    |> Seq.exists (fun videoFile ->
        let videoBase = Path.GetFileNameWithoutExtension(videoFile.Name).ToLowerInvariant()
        subtitleBase = videoBase)