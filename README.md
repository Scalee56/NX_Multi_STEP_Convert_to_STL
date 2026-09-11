# Batch Convert STEP to STL — NX Open Journal

C# journal for NX Open that automatically converts every STEP file (`.stp` / `.step`) in an input folder into STL files, with an interactive folder/scan/decision flow, a pre-flight conflict scan, full assembly traversal, per-occurrence export of repeated components, and separate handling of open (non-solid) bodies.

## What it does

1. Playing the journal (**Tools > Journal > Play...**) immediately asks — via a couple of dialog boxes, before any NX part is touched — whether to use the default input and output folders or pick different ones (via the native Windows folder browser).
2. It then scans, purely on disk (no NX part is opened for this), what the batch would produce against what already exists in the output folder — both the old flat file layout and the new grouped-subfolder layout (see below) — and shows a summary dialog: how many STEP files are new / already OK / in conflict, plus a short list of the conflicting ones (the full list is always written to `ultima_scansione.txt` in the output folder, even if the dialog only shows the first several).
3. After confirming you want to proceed, you choose **one** action for the whole batch, via a dialog with two options mapped to its buttons:
   - **Sovrascrivi** (Sì) — proceed normally; any output the scan flagged as already existing gets overwritten instead of skipped.
   - **Copia in nuova cartella** (No) — the *entire* run's output (not just the conflicting files) goes into a freshly auto-generated, timestamped subfolder (e.g. `STL_Convert\Export_2026-09-11_143000\`), leaving the configured output folder untouched. This is a fully independent run: it starts with its own empty `component_index.txt`, not the original's history.
   - Answering "No" to the earlier "proceed?" question (or Cancel on either folder prompt) stops the journal immediately: nothing is written.
4. The actual conversion then loops over the input folder, and for every STEP file:
   - Opens the file as the active part in NX, switches to Modeling, cleans up faceted faces/edges.
   - Collects the bodies found directly in the part, split into solid bodies and open/non-solid bodies (sheet surfaces).
   - If direct bodies are found, exports them (see "Output layout" below).
   - If no direct bodies are found and the part is an assembly, walks the component tree recursively and applies the same export logic to every component with its own bodies, exporting **each occurrence separately** — or, if that assembly was already processed in a previous run and its component `.prt` files still exist in the input folder, reopens those `.prt` files directly instead of reopening the STEP file.
   - Closes the part without saving and moves on to the next file.

Progress is printed to the NX Listing Window and, at the same time, saved to a log file on disk incrementally (line by line, not just at the end), so the result is still available even if the Listing Window isn't visible, or the journal is interrupted mid-batch.

## Output layout

- **One STL file per body.** Solid bodies are never merged into a single mesh: each becomes its own `.stl` file (`<name>.stl` if there's only one solid body from that source, `<name>_corpoNN.stl` if there are several).
- **Open/non-solid bodies are kept, not discarded.** Sheet bodies (open surfaces) are exported with a `_NOT_CLOSED_MESH` suffix into a `000_Not_Closed_Mesh` subfolder, instead of being silently dropped.
- **Automatic grouping.** When a single source (a STEP file with direct bodies, or one component of an assembly) produces **more than one file in total** (solid bodies + open bodies combined), all of its output is grouped into a dedicated subfolder named after that source (e.g. `PartXYZ\`), with any open bodies further nested inside a `000_Not_Closed_Mesh` subfolder *within* that dedicated folder (`PartXYZ\000_Not_Closed_Mesh\...`). When a source produces exactly one file in total, the layout stays flat exactly as before: the lone solid file sits directly in the output folder, or a lone open-body file sits in the top-level `000_Not_Closed_Mesh\` (not nested under anything).
- **Repeated components are exported once per occurrence.** If the same component appears multiple times in an assembly (e.g. 4 identical screws), it's exported 4 times — once per occurrence — with an `_occNN` suffix (`Vite_occ01.stl`, `Vite_occ02.stl`, ...). A component that appears only once keeps a plain name (`Vite.stl`), with no suffix.
- **Cross-assembly naming collisions are disambiguated automatically.** If two *different* STEP assemblies happen to contain a component with the same name, the script tracks which STEP first claimed that name; a later collision from a different STEP gets its file name prefixed with that STEP's name (e.g. `AssemblyB_Screw.stl`) and a note is logged explaining the rename. Names stay simple in the common case (no collision).
- **Sub-assemblies with their own bodies are exported too.** A component that has both child components *and* its own bodies (e.g. a sub-assembly with extra machining/geometry applied directly to it) is exported for its own bodies as well, instead of being treated as a pure container.

## Pre-flight scan and overwrite policy

- The scan never opens an NX part: for STEP files already known from a previous run (tracked in `component_index.txt`), it recomputes each component's expected output path using the exact same naming logic as the real export, and checks whether it already exists on disk (checking both the legacy flat layout and the current grouped layout, so output from older versions of this journal is still correctly detected). A STEP file with no trace in the index and no matching file on disk is reported as `Nuovo` — by definition it can't conflict with anything.
- The overwrite/copy decision is made **once for the whole batch**, not per file, to avoid a wall of repeated prompts on large batches.
- Regardless of the chosen mode, a collision **between two outputs produced by the same run** (two different sources that would land on the same file path) is always caught and skipped with a warning — the script never silently overwrites a file it just wrote in this same run.
- **Multi-lump body separation (currently a no-op stub).** The script attempts, best-effort, to separate bodies made of multiple disconnected "lumps" before exporting, so that visually distinct solids fused into a single NX `Body` don't end up merged into one STL. This is controlled by `trySeparateMultiLumpBodies` (default `false`) and is currently an inactive stub — the correct NXOpen API depends on your NX version. See the comment above `TrySeparateMultiLumpBodies` in the source for how to record a journal and supply the right API call.

## Configuration

Settings are at the top of the `NXJournal` class. The two folder paths are only *defaults* now — they pre-fill the folder-confirmation dialogs, but can be changed per run through the dialogs without editing the file:

| Parameter | Default value | Description |
|---|---|---|
| `inputFolder` | `C:\Users\AndreaScalenghe\Desktop\STEP_Convert` | Default folder containing the STEP files to convert (and, for previously-processed assemblies, their components' `.prt` files) — can be changed per run in the folder dialog |
| `configuredOutputFolder` | `C:\Users\AndreaScalenghe\Desktop\STL_Convert` | Default destination folder for STL files, logs and the component index — can be changed per run in the folder dialog |
| `chordalTol` | `0.0025` | STL chordal tolerance |
| `adjacencyTol` | `0.08` | STL adjacency tolerance |
| `angularTol` | `5.0` | STL angular tolerance |
| `exportNotClosedMeshes` | `true` | If `true`, open/non-solid bodies are exported to a dedicated subfolder; if `false`, they're ignored |
| `notClosedSubfolderName` | `000_Not_Closed_Mesh` | Subfolder name where open bodies are exported (top-level or nested, depending on grouping) |
| `notClosedSuffix` | `_NOT_CLOSED_MESH` | Suffix appended to open-body file names |
| `trySeparateMultiLumpBodies` | `false` | Currently a no-op stub; reserved for future multi-lump body separation |

To change the tolerances or the default folders, edit these lines directly before running the journal (the folders can also just be changed each run through the dialogs, without touching the file).

## Prerequisites

- Siemens NX with the **Journal** feature available (standard in most licenses; needs the NX Open .NET environment, installed by default with NX). The interactive dialogs use `System.Windows.Forms.MessageBox` and `System.Windows.Forms.FolderBrowserDialog` only — both wrap native Windows dialogs and don't require a custom window/Form to be created, which matters on NX installations where a custom `Form` fails with a `System.Drawing`/`System.Windows.Forms` version-mismatch crash (see "Notes" below).
- Write permissions on the output folder (and on the Desktop, used as a fallback for the log).
- An input folder containing the STEP files — it must exist before scanning, otherwise a dialog reports the error and the journal stops. The output folder is created automatically if it doesn't exist yet.

## How to use it

1. Open NX (no part needs to be open beforehand). Go to **Tools > Journal > Play...** (Strumenti > Automazione > Journal > Riproduci...) and select `BatchConvertSTEPtoSTL.cs`.
2. Two dialogs ask you to confirm (or change, via the native folder browser) the input and output folders. Answering "Annulla" on either one stops the journal immediately.
3. A dialog shows the scan results (counts of new/OK/conflicting STEP files, plus a short list of the conflicting ones — the full list is always in `ultima_scansione.txt`) and asks whether to proceed.
4. If you proceed, a dialog asks **Sovrascrivi** (Sì) or **Copia in nuova cartella** (No) for the whole batch.
5. The conversion then runs: progress is printed to the NX Listing Window, with `[n/total]` progress lines and an OK/ERROR result for each file (and, for assemblies, one result line per exported component occurrence).
6. Let it run until you see the final summary block, with per-STEP-file and per-component totals plus grand totals for solid bodies, open bodies, and STL files written. Do not close NX while it's running.
7. When it's done, check the output folder (the original one, or the new timestamped one if you chose "Copia in nuova cartella"):
   - The converted `.stl` files, flat or grouped into per-source subfolders depending on how many files each source produced (see "Output layout" above).
   - `000_Not_Closed_Mesh\` folders (top-level and/or nested inside grouped subfolders): STL files for open/non-solid bodies, if any were found and `exportNotClosedMeshes` is `true`.
   - `log_conversione.txt`: full run log, written incrementally as the batch progresses.
   - `errori_conversione.log`: only present if at least one file/component failed, with one line per failure (timestamp, file/component, error message).
   - `component_index.txt`: persistent map of STEP file → component occurrences, used on later runs both to reopen known assemblies' `.prt` files directly instead of the STEP, and to detect cross-assembly naming collisions.
   - `ultima_scansione.txt`: full detail of the last pre-flight scan (every STEP file's status), even for entries the summary dialog had to truncate.

**Tip:** before running the conversion on a large batch, copy 2-3 STEP files into the input folder and run the journal on those first — ideally including at least one assembly with a component repeated several times, a part with multiple solid bodies, and/or a part with multi-lump bodies — to confirm tolerances, folder paths, and the grouping/overwrite behavior give the expected result. Once confirmed, add the rest of the batch and re-run: the pre-flight scan will tell you what's already there before anything gets touched.

**If the journal fails to compile in NX:** double-check that the `.cs` file wasn't altered when copying/pasting (encoding or line-ending issues can break the journal compiler), and that you're running it via **Play...** and not trying to build it as a Visual Studio project.

## Output at the end of the run

The Listing Window (and the log) show a final summary with:
- The number of STEP files succeeded/failed.
- The number of component occurrences exported/failed (from assemblies).
- Grand totals: total solid bodies exported, total open bodies exported, total STL files written across the whole run.
- The number of STL files skipped because they already existed and the chosen mode didn't allow overwriting them (if any).

If a file or component fails, it doesn't block the rest of the batch: the error gets logged and the script moves on.

## Notes

- Parts are always closed, even if the export fails, to avoid leftover open parts interfering with subsequent files.
- If writing the log to file also fails (e.g. the output folder isn't writable), the script doesn't stop: it falls back to writing the log to the Desktop.
- The component index (`component_index.txt`) is append-only and never deduplicated: each line represents one occurrence of a component in an assembly, so repeated components keep their correct quantity across runs. In "Copia in nuova cartella" mode, the new folder starts with its own empty index — it does not inherit the original folder's history.
- Multi-lump body separation is not yet functional (`trySeparateMultiLumpBodies = false`, no-op stub) — see the comment above `TrySeparateMultiLumpBodies` in the source for how to help complete it by recording a journal of a manual "Separate Bodies" operation in your NX version.
- The interactive prompts deliberately use only `MessageBox` and `FolderBrowserDialog`, not a custom `System.Windows.Forms.Form`. An earlier version used a custom form with a scrollable results list, but on some NX installations *any* `Form` crashes the moment it's shown (`Form.ShowDialog` → `Form.CreateHandle` → `Form.UpdateWindowIcon` → `MissingMethodException` on `System.Drawing.Icon`'s constructor), due to a version mismatch between the `System.Windows.Forms` and `System.Drawing.Common` assemblies loaded by the NX process. `MessageBox`/`FolderBrowserDialog` wrap native Windows dialogs instead of creating a managed `Form`, so they aren't affected.
