# Batch Convert STEP to STL — NX Open Journal

C# journal for NX Open that automatically converts every STEP file (`.stp` / `.step`) in an input folder into an STL file, one per source file.

## What it does

The script loops over an input folder and, for every STEP file found:

1. Opens the file as the active part in NX.
2. Switches to the Modeling application and cleans up faceted faces/edges.
3. Collects the bodies in the part (solid bodies only, by default).
4. Exports the collected bodies to an STL file with the same name as the source STEP file, using the configured tolerances.
5. Closes the part without saving and moves on to the next file.

Progress is printed to the NX Listing Window and, at the same time, saved to a log file on disk, so the result is still available even if the Listing Window isn't visible or gets closed.

## What changed from v1

- Fixed the enum reference used to close parts: `NXOpen.BasePart.CloseModified` is nested inside `BasePart`, not a standalone `BasePartCloseModified`. This was the most likely cause of the compile-time crash.
- Removed LINQ usage (`Where`, `Cast`, `ToArray`) to minimize the risk of incompatibility with the .NET version used by NX's journal compiler.
- Added a general try/catch around the whole conversion loop, so the log is always written to file, even if something unexpected fails.

## Configuration

Settings are at the top of the `NXJournal` class:

| Parameter | Default value | Description |
|---|---|---|
| `inputFolder` | `C:\Users\AndreaScalenghe\Desktop\STEP_Convert` | Folder containing the STEP files to convert |
| `outputFolder` | `C:\Users\AndreaScalenghe\Desktop\STL_Convert` | Destination folder for STL files and logs |
| `chordalTol` | `0.0025` | STL chordal tolerance |
| `adjacencyTol` | `0.08` | STL adjacency tolerance |
| `angularTol` | `5.0` | STL angular tolerance |
| `onlySolidBodies` | `true` | If `true`, exports only solid bodies; if `false`, exports all bodies (including surfaces) |

To change the folders or tolerances, edit these lines directly before running the journal.

## How to use it

1. Copy the STEP files to convert into `STEP_Convert` (or update `inputFolder`).
2. Open NX and go to **Tools > Journal > Play...**
3. Select the `BatchConvertSTEPtoSTL.cs` file.
4. Watch the progress in the Listing Window.
5. When finished, you'll find the STL files in the output folder, along with:
   - `log_conversione.txt`: full log of the entire run.
   - `errori_conversione.log`: present only if there were errors, with details for each failed file.

**Tip:** before running the conversion on a large batch, test the script on 2-3 files first to make sure the tolerances and settings give the expected result.

## Output at the end of the run

The Listing Window (and the log) show a final summary with the number of files succeeded, failed, and total. If a file fails, it doesn't block the rest of the batch: the error gets logged and the script moves on to the next file.

## Notes

- If a part contains no solid bodies (with `onlySolidBodies = true`), it's logged as an error and skipped.
- Parts are always closed, even if the export fails, to avoid leftover open parts interfering with subsequent files.
- If writing the log to file also fails (e.g. the output folder isn't writable), the script doesn't stop: it falls back to writing the log to the Desktop.
