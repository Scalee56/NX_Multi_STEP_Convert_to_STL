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

## Prerequisites

- Siemens NX with the **Journal** feature available (standard in most licenses; needs the NX Open .NET environment, installed by default with NX).
- Write permissions on the output folder (and on the Desktop, used as a fallback for the log).
- The two folders referenced by the script:
  - `C:\Users\AndreaScalenghe\Desktop\STEP_Convert` (input)
  - `C:\Users\AndreaScalenghe\Desktop\STL_Convert` (output)

  The output folder is created automatically by the script if it doesn't exist yet. The input folder must exist beforehand and contain the STEP files, otherwise the script stops immediately with an error in the log.

## Setup

1. **Save the file** `BatchConvertSTEPtoSTL.cs` somewhere accessible (e.g. Desktop or a dedicated `Journals` folder). NX doesn't need it in any specific location.
2. **Check the paths at the top of the file** (`inputFolder`, `outputFolder`). If your username or folder structure is different from `AndreaScalenghe\Desktop\...`, edit these two lines directly in the `.cs` file with a text editor (Notepad is fine) before running it in NX.
3. **Check the tolerances** (`chordalTol`, `adjacencyTol`, `angularTol`) against the ones you'd normally set in a manual STL export for the same process (MJF, SLA, FDM). Adjust them here if needed; there's no in-NX dialog for this, since the journal runs unattended.
4. **Decide on `onlySolidBodies`**: leave it `true` for standard AM export (solids only); set it to `false` only if you also need to export loose surface bodies.
5. **Populate the input folder** with the STEP files to convert (`.stp` or `.step`, both are picked up).

## How to use it

1. Open NX (no part needs to be open beforehand).
2. Go to **Tools > Journal > Play...**
3. In the file picker, select `BatchConvertSTEPtoSTL.cs` and confirm.
4. NX opens the Listing Window automatically and starts processing the STEP files one by one; you'll see `[n/total]` progress lines and an OK/ERROR result for each file.
5. Let it run until you see the final summary line (`Completato: X riusciti, Y falliti su Z totali.`). Do not close NX while it's running.
6. When it's done, check the output folder:
   - The converted `.stl` files, one per source STEP file, same base name.
   - `log_conversione.txt`: full run log, always written.
   - `errori_conversione.log`: only present if at least one file failed, with one line per failure (timestamp, file path, error message).

**Tip:** before running the conversion on a large batch, copy 2-3 STEP files into the input folder and run the journal on those first, to confirm tolerances, folder paths and `onlySolidBodies` give the expected result. Once confirmed, add the rest of the batch and re-run; already-produced STL files with the same name will simply be overwritten.

**If the journal fails to compile in NX:** double-check that the `.cs` file wasn't altered when copying/pasting (encoding or line-ending issues can break the journal compiler), and that you're running it via **Play...** and not trying to build it as a Visual Studio project.

## Output at the end of the run

The Listing Window (and the log) show a final summary with the number of files succeeded, failed, and total. If a file fails, it doesn't block the rest of the batch: the error gets logged and the script moves on to the next file.

## Notes

- If a part contains no solid bodies (with `onlySolidBodies = true`), it's logged as an error and skipped.
- Parts are always closed, even if the export fails, to avoid leftover open parts interfering with subsequent files.
- If writing the log to file also fails (e.g. the output folder isn't writable), the script doesn't stop: it falls back to writing the log to the Desktop.
