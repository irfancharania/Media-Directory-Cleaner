# Kodi/XBMC Media Directory Cleaner

Tool to help keep Kodi/XBMC media directories clean.

## Background

Using [supplemental tools](http://kodi.wiki/view/Library_managers) like [Media Companion](http://kodi.wiki/view/Media_Companion), users can download media meta content (such as artwork and subtitles) and store it locally. Kodi/XBMC won't scrape the internet if the information it needs is present locally alongside the media.

Unfortunately, when media is deleted from within the Kodi/XBMC interface, the local meta files are left behind on the file system. Hence, the need for this tool.

## Usage

```
DirectoryCleaner.exe <mode> --path "<path>" [--execute]
```

### Important

**By default, the tool runs in PREVIEW mode** - it will only show what would be deleted without actually deleting anything. This is the safe way to verify what will happen.

To actually delete files, you must add the `--execute` flag.

### Examples

```bash
# Preview what would be cleaned
> DirectoryCleaner.exe movies --path "Z:\Movies"

# Preview with short flags
> DirectoryCleaner.exe movies -p "Z:\Movies"

# Actually execute the cleaning
> DirectoryCleaner.exe movies -p "Z:\Movies" --execute
```

### Scheduled Task

For automated daily cleaning, create a scheduled task:

```
DirectoryCleaner.exe movies --path "Z:\Movies" --execute
```

> **Tip:** Run in preview mode first to verify what will be deleted!



## Arguments

### Mode:
There are 3 available modes:
* `tv`
* `movies`
* `music`

### Required

| Argument | Short | Description |
|----------|-------|-------------|
| `--path` | `-p` | Directory path to clean |

### Optional

| Argument | Description |
|----------|-------------|
| `--execute` | Execute mode - actually delete items (default is preview only) |
| `--help` | Display help information |

## Modes

### movies
Cleans movie directories by:
- Deleting **leaf directories** < **100 MB** (orphaned metadata folders without video files)
- Removing **unwanted subtitle files** (keeps English and French, removes all others)
- Preserving `.actors`, `extrafanart` and other metadata for valid movies

**Important:** The cleaner works **iteratively** on leaf directories only. After deleting small folders, previously nested directories may become new leaf nodes. Run multiple times until no more items are found.

**Subtitle Management:**
- **Keeps**: English (eng, en, english), French (fre, fra, fr, français), Canadian French (fr-ca, québec)
- **Removes**: 40+ other languages (spa, ger, ara, hin, ita, por, etc.)
- **Keeps**: If language is uncertain, file is kept

### tv
Cleans TV show directories by:
- Deleting orphaned files < **100 MB** without corresponding video files
- Deleting empty season folders (no video files)

### music
Cleans music directories by:
- Deleting leaf directories < **500 KB** without audio files


## Folder Structures

### Movies

The main movie folder may contain set folders with subdirectories.

Leaf-nodes sized below **100 MB** will be subject for deletion, as movie files are generally greater than this size. Any leftover directories will become leaf directories for the next run.

**Non-English/French subtitles** in movie folders are also removed (regardless of folder size).

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

The tool creates the following files in the specified path directory:

- **`cleanLog.log`** - Detailed log of all cleaning operations with timestamps
- **`.lastrun`** - Tracks the last successful run date (UTC) for optimizing subtitle cleaning for movies

The `.lastrun` file contains a UTC timestamp in ISO 8601 format (e.g., `2025-01-15T18:30:00.0000000Z`) and is used to skip directories that haven't changed since the last run.

## Output

Progress messages are written to stderr, allowing you to redirect clean results to a file:


Preview mode output:
```
Validating path... done
Scanning directories... done
Finding leaf nodes... done
Finding small directories... done
Classifying subtitles... done

PREVIEW MODE - The following items would be deleted with --execute

  [DIR]  Z:\Movies\Old Movie (2010)
  [FILE] Z:\Movies\Good Movie\subtitle.spa.srt

Total: 1 directories, 1 files
```

## Notes

> - **Dot-prefixed directories** (like `.actors`) and **extrafanart** folders are never evaluated for deletion - their fate is determined by their parent folder
> - **Iterative cleaning**: The tool processes leaf directories only. Run multiple times to clean newly exposed leaf nodes after deletions
> - Preview mode is the default. Use `--execute` to actually delete
> - When in doubt, files are kept (conservative approach)

## Supported Subtitle Languages

### Kept (English and French variants)
- **English**: English, eng, en
- **French**: French, français, fre, fra, fr
- **Canadian French**: fr-ca, frc, québec, canadien

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
dotnet run --project src/DirectoryCleaner.fsproj -- movies -p "C:\Movies"
```

## Project History

This tool has evolved through several iterations:
1. **Original**: AutoIT script (circa 2005)
2. **F# 3 rewrite**: First F# implementation (2015)
3. **F# 10 rewrite**: Current version with modern F# practices and comprehensive test coverage (2025)

## License

This project is provided as-is for personal use.