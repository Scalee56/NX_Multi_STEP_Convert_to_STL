# Batch Convert STEP to STL — NX Open Journal

C# journal for NX Open that automatically converts every STEP file (`.stp` / `.step`) in an input folder into STL files, including full assembly traversal, per-occurrence export of repeated components, and separate handling of open (non-solid) bodies.

## What it does

The script loops over an input folder and, for every STEP file found:

1. Opens the file as the active part in NX.
2. Switches to the Modeling application and cleans up faceted faces/edges.
3. Collects the bodies found directly in the part, split into solid bodies and open/non-solid bodies (sheet surfaces).
4. If direct bodies are found:
   - Every solid body is exported to its own STL file in the output folder.
   - Every open body (if `exportNotClosedMeshes` is `true`) is exported to its own STL file in a dedicated `000_Not_Closed_Mesh` subfolder, with a `_NOT_CLOSED_MESH` suffix.
5. If no direct bodies are found and the part is an assembly, the script walks the component tree recursively and applies the same solid/open export logic to every leaf component, exporting **each occurrence separately** (see below) — or, if that assembly was already processed in a previous run and its component `.prt` files still exist in the input folder, it reopens those `.prt` files directly instead of reopening the STEP file.
6. Closes the part without saving and moves on to the next file.

Progress is printed to the NX Listing Window and, at the same time, saved to a log file on disk, so the result is still available even if the Listing Window isn't visible or gets closed.

## Key behaviors

- **One STL file per body.** Solid bodies are never merged into a single mesh: each becomes its own `.stl` file (`<name>.stl` if there's only one body, `<name>_corpoNN.stl` if there are several).
- **Open/non-solid bodies are kept, not discarded.** Sheet bodies (open surfaces) are exported into `000_Not_Closed_Mesh` with a `_NOT_CLOSED_MESH` suffix instead of being silently dropped, so nothing goes missing without being noticed.
- **Repeated components are exported once per occurrence.** If the same component appears multiple times in an assembly (e.g. 4 identical screws), it's exported 4 times — once per occurrence — with an `_occNN` suffix (`Vite_occ01.stl`, `Vite_occ02.stl`, ...). A component that appears only once keeps a plain name (`Vite.stl`), with no suffix.
- **Persistent component index.** Every STEP → component occurrence mapping is recorded in `component_index.txt`. On a later run, if a STEP file was already processed as an assembly and its components' `.prt` files still exist in the input folder, the script reopens those `.prt` files directly instead of reopening the STEP (which would otherwise fail because NX finds the existing `.prt` files).
- **Anti-overwrite protection.** If a target STL file already exists on disk, that body is skipped (not silently overwritten) and logged explicitly; skipped files are counted and reported in the final summary.
- **Multi-lump body separation (currently a no-op stub).** The script attempts, best-effort, to separate bodies made of multiple disconnected "lumps" before exporting, so that visually distinct solids fused into a single NX `Body` don't end up merged into one STL. This is controlled by `trySeparateMultiLumpBodies` (default `false`) and is currently an inactive stub — the correct NXOpen API depends on your NX version. See the comment above `TrySeparateMultiLumpBodies` in the source for how to record a journal and supply the right API call.
- Detailed, per-component logging, with grand totals at the end of the run (total solid bodies exported, total open bodies exported, total STL files written, across the whole batch).

## Configuration

Settings are at the top of the `NXJournal` class:

| Parameter | Default value | Description |
|---|---|---|
| `inputFolder` | `C:\Users\AndreaScalenghe\Desktop\STEP_Convert` | Folder containing the STEP files to convert (and, for previously-processed assemblies, their components' `.prt` files) |
| `outputFolder` | `C:\Users\AndreaScalenghe\Desktop\STL_Convert` | Destination folder for STL files, logs and the component index |
| `chordalTol` | `0.0025` | STL chordal tolerance |
| `adjacencyTol` | `0.08` | STL adjacency tolerance |
| `angularTol` | `5.0` | STL angular tolerance |
| `exportNotClosedMeshes` | `true` | If `true`, open/non-solid bodies are exported to a dedicated subfolder; if `false`, they're ignored |
| `notClosedSubfolderName` | `000_Not_Closed_Mesh` | Subfolder (inside `outputFolder`) where open bodies are exported |
| `notClosedSuffix` | `_NOT_CLOSED_MESH` | Suffix appended to open-body file names |
| `trySeparateMultiLumpBodies` | `false` | Currently a no-op stub; reserved for future multi-lump body separation |

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
4. **Decide on `exportNotClosedMeshes`**: leave it `true` to also get open/non-solid bodies exported (flagged in their own subfolder); set it to `false` if you only ever want solid-body STLs.
5. **Populate the input folder** with the STEP files to convert (`.stp` or `.step`, both are picked up).

## How to use it

1. Open NX (no part needs to be open beforehand).
2. Go to **Tools > Journal > Play...** (Strumenti > Automazione > Journal > Riproduci...).
3. In the file picker, select `BatchConvertSTEPtoSTL.cs` and confirm.
4. NX opens the Listing Window automatically and starts processing the STEP files one by one; you'll see `[n/total]` progress lines and an OK/ERROR result for each file (and, for assemblies, one result line per exported component occurrence).
5. Let it run until you see the final summary block, with per-STEP-file and per-component totals plus grand totals for solid bodies, open bodies, and STL files written. Do not close NX while it's running.
6. When it's done, check the output folder:
   - The converted `.stl` files.
   - `000_Not_Closed_Mesh\`: STL files for open/non-solid bodies, if any were found and `exportNotClosedMeshes` is `true`.
   - `log_conversione.txt`: full run log, always written.
   - `errori_conversione.log`: only present if at least one file/component failed, with one line per failure (timestamp, file/component, error message).
   - `component_index.txt`: persistent map of STEP file → component occurrences, used on later runs to reopen known assemblies' `.prt` files directly instead of the STEP.

**Tip:** before running the conversion on a large batch, copy 2-3 STEP files into the input folder and run the journal on those first — ideally including at least one assembly with a component repeated several times, and/or a part with multi-lump bodies — to confirm tolerances, folder paths and the export options give the expected result. Once confirmed, add the rest of the batch and re-run; STL files that would overwrite an existing one are skipped and logged instead of silently replaced.

**If the journal fails to compile in NX:** double-check that the `.cs` file wasn't altered when copying/pasting (encoding or line-ending issues can break the journal compiler), and that you're running it via **Play...** and not trying to build it as a Visual Studio project.

## Output at the end of the run

The Listing Window (and the log) show a final summary with:
- The number of STEP files succeeded/failed.
- The number of component occurrences exported/failed (from assemblies).
- Grand totals: total solid bodies exported, total open bodies exported, total STL files written across the whole run.
- The number of STL files skipped because they already existed on disk (if any).

If a file or component fails, it doesn't block the rest of the batch: the error gets logged and the script moves on.

## Notes

- Parts are always closed, even if the export fails, to avoid leftover open parts interfering with subsequent files.
- If writing the log to file also fails (e.g. the output folder isn't writable), the script doesn't stop: it falls back to writing the log to the Desktop.
- The component index (`component_index.txt`) is append-only and never deduplicated: each line represents one occurrence of a component in an assembly, so repeated components keep their correct quantity across runs.
- Multi-lump body separation is not yet functional (`trySeparateMultiLumpBodies = false`, no-op stub) — see the comment above `TrySeparateMultiLumpBodies` in the source for how to help complete it by recording a journal of a manual "Separate Bodies" operation in your NX version.
