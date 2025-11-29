# Kodi Media Directory Cleaner

Tool to help keep Kodi media directories clean on Windows 10+.

## Background

Using [supplemental tools](https://kodi.wiki/view/Supplemental_tools#Library_managers) like [Media Companion](https://mediacompanion.sourceforge.io/), users can download media meta content (such as artwork and subtitles) and store it locally. Kodi/XBMC won't scrape the internet if the information it needs is present locally alongside the media.

Unfortunately, when media is deleted from within the Kodi/XBMC interface, the local meta files are left behind on the file system. Hence, the need for this tool.

## Usage

```
DirectoryCleaner.exe <mode> --path "<path>" [--execute] [--scan-all]
```

### Arguments

#### Mode:
There are 3 available modes:
* `tv`
* `movies`
* `music`

#### Required

| Argument | Short | Description |
|----------|-------|-------------|
| `--path` | `-p` | Directory path to clean |

#### Optional

| Argument | Short | Description |
|----------|-------|-------------|
| `--execute` | | Execute mode - actually delete items (default is preview only) |
| `--scan-all` | `-a` | Bypass optimization and scan all directories (movies mode only) |
| `--help` | | Display help information |

### Examples

```bash
# Preview what would be cleaned
> DirectoryCleaner.exe movies --path "Z:\Movies"

# Preview with short flags
> DirectoryCleaner.exe movies -p "Z:\Movies"

# Actually execute the cleaning
> DirectoryCleaner.exe movies -p "Z:\Movies" --execute

# Scan all directories (bypass optimization to see uncertain subtitles)
> DirectoryCleaner.exe movies -p "Z:\Movies" --scan-all

# Scan all directories and execute cleaning
> DirectoryCleaner.exe movies -p "Z:\Movies" --scan-all --execute
```

### Scheduled Task

This can be scheduled in Task Scheduler to run once a day:

```
DirectoryCleaner.exe movies --path "Z:\Movies" --execute
```

The tool is built to run repeatedly. What doesn't get cleaned on the first run may get cleaned on a subsequent run as it incrementally works through deleting unwanted files.

> **Important:** Run in preview mode first to verify what will be deleted!

## Modes

### Movies

The main movie folder may contain set folders with subdirectories.

Leaf-nodes sized below **100 MB** will be subject for deletion, as movie files are generally greater than this size. Any leftover directories will become leaf directories for the next run.

**Optimization**

Movies mode uses a **timestamp-based optimization** to skip directories that haven't changed since the last run:
- On first run, all directories are scanned
- A `.lastrun` file is created with the timestamp
- Subsequent runs only scan directories modified after that timestamp

**Subtitle Management:**

- **Keeps**: English (eng, en, english), French (fre, fra, fr, français), Canadian French (fr-ca, québec)
- **Removes**: 40+ other languages (spa, ger, ara, hin, ita, por, etc.)
- **Keeps**: If language is uncertain, file is kept

**Uncertain Subtitles:**

Subtitles without recognizable language codes are flagged as "uncertain" and reported in preview mode. Use `--scan-all` to see these again on subsequent runs.


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

### TV

All episode files for season/year are contained within the same folder. TV show files are expected to be in leaf nodes.

Deletes all files sized below **100 MB** that do not have a corresponding large file, and are not named "folder" or "poster". Empty season folders (no video files) are also deleted.

**Show Folders Without Seasons:**
TV show root folders without season subdirectories are detected and reported for manual review. This can happen when:
- All season folders were deleted (because they were empty)
- New season folders will be added later

These folders are flagged with `[NO SEASONS]` in preview mode for awareness.

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

The tool creates the following files after every run:

- **`cleanLog.log`** - Log of all deleted items
- **`.lastrun`** (Movies only) - Tracks the last successful run date (UTC) for optimizing subtitle cleaning

## Sample Output

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

Scan all mode with uncertain subtitles:
```
Validating path... done
Scanning directories... done
Finding leaf nodes... done
  Scan all mode: Checking all directories (optimization disabled)
Finding small directories... done
Classifying subtitles... done

=== UNCERTAIN SUBTITLES (Review Manually) ===
  [UNCERTAIN] Z:\Movies\Some Movie\subtitle.srt

PREVIEW MODE - The following items would be deleted with --execute
...
```

TV show folders without seasons:
```
Validating path... done
Scanning directories... done
Finding leaf nodes... done
Separating show folders... done

=== TV SHOW FOLDERS WITHOUT SEASONS (Review Manually) ===
  [NO SEASONS] Z:\TV\Documentary Series

Finding leaf nodes... done
...
```

## Notes

> - **.actors** and **extrafanart** folders are never evaluated for deletion. Their fate is determined by their parent folder
> - **Iterative cleaning**: The tool processes leaf directories only. Run multiple times to clean newly exposed leaf nodes after deletions
> - Preview mode is the default. Use `--execute` to actually delete
> - Files are not deleted unless tool can definitively identify
> - Movies mode will only scan recently created/modified directories. Use `--scan-all` to bypass and scan all directories to see subtitles the tool cannot identify
> - The `--scan-all` flag only affects movies mode. TV and music modes don't use optimization because of their nested file structure.

## Building

### Prerequisites
- Windows 10+
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
dotnet run --project src/DirectoryCleaner.fsproj movies -p "Z:\Movies"
```

## Project History

This tool has evolved through several iterations:
* 2005 - **Original**: AutoIT script
* 2015 - **F# 3 rewrite**: First F# implementation
* 2025 - **F# 10 rewrite**: Enhanced version with modern F# practices and comprehensive test coverage

## License

This project is provided as-is for personal use.
