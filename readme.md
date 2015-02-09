# Kodi/XBMC Media Directory Cleaner

Tool to help keep Kodi/XBMC media directories clean.



## Background

Using [supplemental tools](http://kodi.wiki/view/Library_managers) like [Media Companion](http://kodi.wiki/view/Media_Companion), users can download media meta content (such as artwork and subtitles) and store it locally. Kodi/XBMC won't scrape the internet if the information it needs is present with the media.


Unfortunately, when media is deleted from within the Kodi/XBMC interface, the local meta files are left behind. Hence, the need for this tool.


This Media Directory Cleaner is a rewrite of a 10 yr old AutoIT script to practice F#.



## Usage

``DirectoryCleaner.exe [mode] -path "[path]" [--preview]``

Examples:

``DirectoryCleaner.exe tv -path "Z:\TV Shows" --preview``

``DirectoryCleaner.exe movies -path "Z:\Movies"``

> This can be scheduled in Task Scheduler to run once a day



### Mode

There are 2 available modes:
- tv
- movies

### Path

Folder path must be provided

### Preview flag

Adding the optional (``--preview``) will display the list of files that would have been deleted without this flag present.



## Folders

### Movies

The main movie folder may contain set folders with subdirectories.

Leaf-nodes sized below **100 MB** will be subject for deletion, as movie files are generally greater than this size. Any left over directories will become leaf directories for next run.

#### Expected folder structure:

```
Movies
   |---- Some Movie (2015)
   |       |---- <ignore>
   |
   |---- Movie Set
   |       |---- Another Movie 1 (2010)
   |       |        |---- <ignore>
   |       |
   |       |---- Another Movie 2 (2011)
```


### TV

All episode files for season/year are contained within same folder.
Delete all files sized below **100 MB** that do not have a corresponding large file, and are not named "folder"

TV show files are expected to be in leaf nodes.

#### Expected folder structure:

```
TV Shows
   |----TV Show 1
   |       |----Season #
   |            |--Files
   |
   |----TV Show 2 (year)
   |       |--Files
   |
   |----TV Show 3
   |       |----2008
   |            |--Files
```