# Organizer

[Español](README.es.md)

Organizer is a Windows console app for sorting photos and videos from one or more folders, including Google Photos Takeout exports. Choose your sources, where to put the library, and what to call it. The app previews every destination before you move a file.

## Quick start

1. Extract or clone this repository into a writable folder on Windows with .NET Framework 4.8. Double-click **Organizer.cmd**. The launcher builds the executable on first use.
2. Select **1 · English**, then **1 · Create a preview**. Type or drag in each source folder, pressing Enter after each one. Press Enter on a blank line to finish.
3. Choose the parent folder and a name for your library. For example, a parent of `D:\Pictures` and a name of `Family Library` produce `D:\Pictures\Family Library`. The sources and destination must be separate folders on the same volume. For a network drive, use the same mapped drive or share throughout.
4. Open **Plan.csv** in the folder shown after the scan. To apply the preview, select **2** and enter **MOVE**. To go back later, select **3** and enter **UNDO**. Keep the **Planes** folder beside the app so it can find its journal.

## How files are organized

```text
Family Library/
  2024/06 - June/2024-06-07/Photos/Vacation/
  2024/06 - June/2024-06-07/Screenshots/Facebook/
  2019/Unknown month/Videos/
  Undated/Photos/
```

Organizer prefers the capture time in a matching Takeout JSON, then embedded media metadata, then a date in the filename. If only a folder year is available, it uses **Unknown month**. Album subfolders keep their names. The language selected when creating a preview determines its month and category names.

Screenshots and Facebook media are identified from names and folders. Videos with 32-character hexadecimal names go in **Possible TikToks** when there is no stronger Facebook or screenshot clue. Camera originals, edits and repeated files are kept. Name collisions receive suffixes such as ` (2)`. Matching JSON, XMP, AAE and THM sidecars travel with their media. Check **Revisar.csv** for ambiguous files and read errors.

## Command line and development

```powershell
.\Build.cmd
.\Organizer.exe preview "D:\Pictures\Family Library" "D:\Imports\Camera" "D:\Imports\Takeout"
.\Organizer.exe apply ".\Planes\BATCH_FOLDER"
.\Organizer.exe undo ".\Planes\BATCH_FOLDER"
.\tests\Check.ps1
```

Replace `BATCH_FOLDER` with the directory printed by the preview. The CLI apply and undo commands run immediately. After an interruption, rerun the same command with the same batch. Exit codes are 0 for success, 1 for an error and 2 when files remain pending. Use `--lang en` or `--lang es` before a command to choose its language.

Run **Build.cmd** again after changing the source. Tests use generated media in a temporary directory. Git ignores the compiled executable and the **Planes** directory, which contains local paths, filenames and move journals.
