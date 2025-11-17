# Kodi/XBMC Media Directory Cleaner

Tool to help keep Kodi/XBMC media directories clean.

## Background

Using [supplemental tools](http://kodi.wiki/view/Library_managers) like [Media Companion](http://kodi.wiki/view/Media_Companion), users can download media meta content (such as artwork and subtitles) and store it locally. Kodi/XBMC won't scrape the internet if the information it needs is present with the media.

Unfortunately, when media is deleted from within the Kodi/XBMC interface, the local meta files are left behind. Hence, the need for this tool.

This Media Directory Cleaner is a modernized rewrite of a AutoIT script from 2005, rebuilt using modern F# 10 practices.

## Features

- **Safe by default** - Preview mode is default, requires explicit `--execute` flag
- **Smart cleaning** - Identifies orphaned metadata files and small directories
- **Subtitle cleaning** - Automatically removes non-English subtitle files (movies only)
- **Structured logging** - Detailed logs with Serilog (properly disposed)

## Usage

```
DirectoryCleaner.exe --mode <mode> --path "<path>" [--execute]
```

### Important

**By default, the tool runs in PREVIEW mode** - it will only show what would be deleted without actually deleting anything. This is the safe way to verify what will happen.

To actually delete files, you must add the `--execute` flag.

### Examples

```bash
# Preview what would be cleaned (safe, default behavior)
DirectoryCleaner.exe --mode movies --path "C:\Movies"

# Preview with short flags
DirectoryCleaner.exe -m tv -p "C:\TV Shows"

# Actually execute the cleaning (requires --execute flag)
DirectoryCleaner.exe --mode movies --path "C:\Movies" --execute

# Preview music cleaning
DirectoryCleaner.exe -m music -p "C:\Music"

# Display version
DirectoryCleaner.exe --version
DirectoryCleaner.exe -v
```

### Scheduled Task

For automated daily cleaning, create a scheduled task:

```bash
DirectoryCleaner.exe --mode movies --path "C:\Movies" --execute
```

> **Tip:** Run in preview mode first to verify what will be deleted!

## Modes

### movies
Cleans movie directories by:
- Deleting directories < **100 MB** (orphaned metadata folders without video files)
- Removing **unwanted subtitle files** (keeps English and French, removes all others)
- Preserving `.actors`, `extrafanart` and other metadata for valid movies

**Subtitle Management:**
- **Keeps**: English (eng, en, english), French (fre, fra, fr, français), Canadian French (fr-ca, québec)
- **Keeps**: SDH (hearing impaired), HI, CC (closed captions)
- **Removes**: 40+ other languages (spa, ger, ara, hin, ita, por, etc.)
- **Conservative**: If language is uncertain, file is kept

### tv
Cleans TV show directories by:
- Deleting orphaned files < **100 MB** without corresponding video files
- Preserving `folder.jpg` and `poster.jpg` images
- Handling episode naming variations and ripping group suffixes

### music
Cleans music directories by:
- Deleting leaf directories < **500 KB** without audio files
- Preserving valid albums and tracks

## Arguments

### Required

| Argument | Short | Description |
|----------|-------|-------------|
| `--mode` | `-m` | Cleaning mode: `tv`, `movies`, or `music` |
| `--path` | `-p` | Directory path to clean |

### Optional

| Argument | Short | Description |
|----------|-------|-------------|
| `--execute` | | Execute mode - actually delete items (default is preview only) |
| `--version` | `-v` | Display version information |
| `--help` | | Display help information |

## Folder Structures

### Movies

The main movie folder may contain set folders with subdirectories.

Leaf-nodes sized below **100 MB** will be subject for deletion, as movie files are generally greater than this size. Any leftover directories will become leaf directories for the next run.

**Non-English subtitles** in movie folders are also removed (regardless of folder size).

#### Expected folder structure:

```
Movies
   |---- Some Movie (2015)
   |       |---- movie.mp4              (kept - main video)
   |       |---- English.srt            (kept - English subtitle)
   |       |---- French.srt             (kept - French subtitle)
   |       |---- fr-ca.srt              (kept - Canadian French)
   |       |---- spa.srt                (deleted - Spanish)
   |       |---- ger.srt                (deleted - German)
   |       |---- poster.jpg             (kept - metadata)
   |       |---- .actors/               (kept - starts with .)
   |
   |---- Movie Set
   |       |---- Another Movie 1 (2010)
   |       |        |---- <video and metadata>
   |       |
   |       |---- Another Movie 2 (2011)
   |
   |---- Old Movie (No Video)           (deleted - entire folder < 100MB)
```

### TV Shows

All episode files for season/year are contained within the same folder.
Delete all files sized below **100 MB** that do not have a corresponding large file, and are not named "folder" or "poster".

TV show files are expected to be in leaf nodes.

#### Expected folder structure:

```
TV Shows
   |----TV Show 1
   |       |----Season 01
   |            |---- episode.mkv       (kept - video file)
   |            |---- episode.srt       (kept - has corresponding video)
   |            |---- folder.jpg        (kept - folder image)
   |            |---- orphan.srt        (deleted - no corresponding video)
   |
   |----TV Show 2 (2020)
   |       |---- episode.mp4
   |       |---- episode.nfo
   |
   |----TV Show 3
   |       |----2008
   |            |--Files
```

### Music

Music folder will contain folders with subdirectories.

Leaf-nodes without identifiable audio files sized below **500 KB** will be subject for deletion. Any leftover directories will become leaf directories for the next run.

#### Expected folder structure:

```
Music
   |----Artist
   |       |----Album
   |            |---- track.mp3         (kept - audio file)
   |            |---- cover.jpg         (kept - has audio in folder)
   |
   |----Artist 2
   |       |---- album.flac
   |
   |----Empty Album Folder              (deleted - no audio files)
```

## Logging

The tool creates a log file named `cleanLog.log` in the specified path directory.

Logs include:
- Timestamp of cleaning operation
- List of all deleted items

**Technical Note:** The logger is properly disposed using F#'s `use` binding, ensuring all logs are flushed to disk.

## Notes

> - **Dot-prefixed directories** (like `.actors`) are never deleted themselves and are not recursed into when cleaning - their fate is determined by their parent folder
> - **extrafanart directories** are treated similarly - they're not recursed into, but preserved with their parent
> - Preview mode is the default. Use `--execute` to actually delete
> - When in doubt, files are kept (conservative approach)
> - Subtitle language detection uses ISO 639-2/3 language codes
> - Accessibility subtitles (SDH/HI/CC) with English or French are always kept

## Supported Subtitle Languages

### Kept (English and French variants)
- **English**: English, eng, en
- **French**: French, français, fre, fra, fr
- **Canadian French**: fr-ca, frc, québec, canadien
- **Accessibility**: SDH (Subtitles for Deaf/Hard of hearing), HI (Hearing Impaired), CC (Closed Captions)

### Removed (40+ other languages)
Arabic (ara), Basque (baq), Catalan (cat), Czech (cze), Danish (dan), Dutch (dut), Finnish (fin), German (ger), Galician (glg), Greek (gre), Hindi (hin), Hungarian (hun), Italian (ita), Norwegian (nor), Polish (pol), Portuguese (por), Romanian (rum), Spanish (spa), Swedish (swe), Turkish (tur), Chinese (chi), Japanese (jpn), Korean (kor), Kannada (kan), Malayalam (mal), Tamil (tam), Telugu (tel), and many more...

## Building

### Prerequisites
- .NET 10.0 SDK
- F# 10.0

### Build Commands

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run tests
dotnet test

# Run the application
dotnet run --project src/DirectoryCleaner.fsproj -- --mode movies -p "C:\Movies"
```

## License

This project is provided as-is for personal use.