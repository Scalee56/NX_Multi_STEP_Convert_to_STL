# Batch Convert STEP to STL — NX Open Journal

C# journal for NX Open that automatically converts every STEP file (`.stp` / `.step`) in an input folder into STL files, with a pre-flight conflict scan, full assembly traversal, per-occurrence export of repeated components, and separate handling of open (non-solid) bodies. Configuration, conflict resolution and the final summary are shown in **real windows**, launched as a separate `powershell.exe` process so they aren't affected by the `System.Drawing`/`System.Windows.Forms` compatibility issue that breaks an in-process `Form` on some NX installations (see "Notes").

## What it does

1. Playing the journal (**Tools > Journal > Play...**) opens a configuration window immediately, before any NX part is touched, with the input/output folders pre-filled and "Sfoglia..." buttons, plus an optional "Mostra opzioni avanzate" section to change the STL tolerances or the open-mesh export behaviour for just this run. This window (and every other one below) is shown by a `powershell.exe` process the journal launches and waits for — see "Notes" for why. On environments where PowerShell isn't usable for any reason, the journal automatically falls back to native folder-browser/MessageBox dialogs instead — the rest of the flow behaves the same either way (same scan, same decisions, same cancel-via-marker-file and open-body toggle), just without a visible progress window (replaced by the Listing Window and log file updating live).
2. Once you press "Avvia scansione", the journal checks, purely on disk (no NX part is opened for this), what the batch would produce against what already exists in the output folder — both the old flat file layout and the new grouped-subfolder layout (see below).
3. **It only asks a follow-up question if the scan actually found something to decide.** If nothing conflicts, the journal proceeds straight to the conversion — no extra window. If the scan does find conflicting output from a previous run, a second window shows the results (counts, plus the conflicting files — the full list is always written to `ultima_scansione.txt`) and asks **one** choice for the whole batch:
   - **Sovrascrivi** — the conflicting output gets overwritten; everything else proceeds normally.
   - **Copia in nuova cartella** — the *entire* run's output (not just the conflicting files) goes into a freshly auto-generated, timestamped subfolder (e.g. `STL_Convert\Export_2026-09-11_143000\`), leaving the configured output folder untouched. This is a fully independent run: it starts with its own empty `component_index.txt`, not the original's history.
   - **Interrompi** — stop, nothing is written.
4. The conversion then runs directly inside NX (it needs the NXOpen API, so it can't happen in the separate PowerShell process); for every STEP file:
   - Opens the file as the active part in NX, switches to Modeling, cleans up faceted faces/edges.
   - Collects the bodies found directly in the part, split into solid bodies and open/non-solid bodies (sheet surfaces).
   - If direct bodies are found, exports them (see "Output layout" below).
   - If no direct bodies are found and the part is an assembly, walks the component tree recursively and applies the same export logic to every component with its own bodies, exporting **each occurrence separately** — or, if that assembly was already processed in a previous run and its component `.prt` files still exist in the input folder, reopens those `.prt` files directly instead of reopening the STEP file.
   - Closes the part without saving and moves on to the next file.
5. When it's done (or after you cancel it — see below), a final summary window appears with how many STEP files succeeded/failed and how many STL files were written, plus a button to open the output folder directly and a "Chiudi" button.

Progress during the conversion is printed to the NX Listing Window and saved to a log file on disk incrementally (line by line, not just at the end, with a `[n/total]` counter on every file), so the result is still available even if the Listing Window isn't visible, or the journal is interrupted mid-batch. To stop mid-batch, create a file named **`CANCEL.txt`** in the output folder — it's picked up between one STEP file and the next (never mid-export), the batch stops cleanly, and the marker file is deleted automatically (the journal logs the exact path to use at the start of every run).

## Output layout

- **One STL file per body.** Solid bodies are never merged into a single mesh: each becomes its own `.stl` file (`<name>.stl` if there's only one solid body from that source, `<name>_corpoNN.stl` if there are several).
- **Open/non-solid bodies are kept, not discarded.** Sheet bodies (open surfaces) are exported with a `_NOT_CLOSED_MESH` suffix into a `000_Not_Closed_Mesh` subfolder, instead of being silently dropped.
- **Automatic grouping.** When a single source (a STEP file with direct bodies, or one component of an assembly) produces **more than one file in total** (solid bodies + open bodies combined), all of its output is grouped into a dedicated subfolder named after that source (e.g. `PartXYZ\`), with any open bodies further nested inside a `000_Not_Closed_Mesh` subfolder *within* that dedicated folder (`PartXYZ\000_Not_Closed_Mesh\...`). When a source produces exactly one file in total, the layout stays flat exactly as before: the lone solid file sits directly in the output folder, or a lone open-body file sits in the top-level `000_Not_Closed_Mesh\` (not nested under anything).
- **Repeated components are exported once per occurrence.** If the same component appears multiple times in an assembly (e.g. 4 identical screws), it's exported 4 times — once per occurrence — with an `_occNN` suffix (`Vite_occ01.stl`, `Vite_occ02.stl`, ...). A component that appears only once keeps a plain name (`Vite.stl`), with no suffix.
- **Cross-assembly naming collisions are disambiguated automatically.** If two *different* STEP assemblies happen to contain a component with the same name, the script tracks which STEP first claimed that name; a later collision from a different STEP gets its file name prefixed with that STEP's name (e.g. `AssemblyB_Screw.stl`) and a note is logged explaining the rename. Names stay simple in the common case (no collision).
- **Sub-assemblies with their own bodies are exported too.** A component that has both child components *and* its own bodies (e.g. a sub-assembly with extra machining/geometry applied directly to it) is exported for its own bodies as well, instead of being treated as a pure container.

## Pre-flight scan and overwrite policy

- The scan never opens an NX part: for STEP files already known from a previous run (tracked in `component_index.txt`), it recomputes each component's expected output path using the exact same naming logic as the real export, and checks whether it already exists on disk (checking both the legacy flat layout and the current grouped layout, so output from older versions of this journal is still correctly detected). A STEP file with no trace in the index and no matching file on disk is reported as `Nuovo` — by definition it can't conflict with anything.
- The overwrite/copy decision is made **once for the whole batch**, not per file, to avoid a wall of repeated prompts on large batches — and isn't asked at all when the scan finds no conflicts.
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
| `enableProgressPreview` | `true` | Periodically renders the current NX view in the progress window. Set to `false` only for unattended/server runs where maximum throughput matters more than the preview. |
| `reuseIndexedComponentPrtFiles` | `false` | If `true`, a previously indexed assembly is rebuilt by reopening every component `.prt`. Disabled by default because opening the original STEP once is normally much faster, especially with repeated components. |
| `notClosedSubfolderName` | `000_Not_Closed_Mesh` | Subfolder name where open bodies are exported (top-level or nested, depending on grouping) |
| `notClosedSuffix` | `_NOT_CLOSED_MESH` | Suffix appended to open-body file names |
| `trySeparateMultiLumpBodies` | `false` | Currently a no-op stub; reserved for future multi-lump body separation |

To change the tolerances or the default folders, edit these lines directly before running the journal (the folders can also just be changed each run through the dialogs, without touching the file).

## Prerequisites

- Siemens NX with the **Journal** feature available (standard in most licenses; needs the NX Open .NET environment, installed by default with NX).
- **Windows PowerShell** available and runnable (`powershell.exe`, the classic one built into every Windows install — not needed as an admin, and not affected by `ExecutionPolicy` restrictions beyond what the journal already works around, see "Notes"). If it can't run for any reason, the journal automatically falls back to `System.Windows.Forms.MessageBox`/`FolderBrowserDialog` shown directly inside NX (native Windows dialogs, no separate process needed).
- Write permissions on the output folder and on the user's temp folder (`%TEMP%`, used to exchange data with the PowerShell process — no administrator rights needed for either of these).
- An input folder containing the STEP files — it must exist before scanning, otherwise the journal reports the error and stops. The output folder is created automatically if it doesn't exist yet.

## How to use it

1. Open NX (no part needs to be open beforehand). Go to **Tools > Journal > Play...** (Strumenti > Automazione > Journal > Riproduci...) and select `BatchConvertSTEPtoSTL.cs`.
2. Check or change the input/output folders (via "Sfoglia..." or by typing directly), then press "Avvia scansione" (in the fallback mode, each folder is picked via one native browser dialog — the default is already pre-selected, so just confirming it is one click).
3. **If the scan finds no conflicts, the conversion starts immediately — no further questions.** If it finds output that already exists from a previous run, a second window shows a summary (counts, plus the conflicting files — the full list is always in `ultima_scansione.txt`) and a single choice: **Interrompi**, **Sovrascrivi**, or **Copia in nuova cartella**.
4. The conversion then runs: progress is printed to the NX Listing Window, with `[n/total]` progress lines and an OK/ERROR result for each file (and, for assemblies, one result line per exported component occurrence).
5. Let it run until you see the final summary window, with per-STEP-file and per-component totals plus grand totals for solid bodies, open bodies, and STL files written (or, in fallback mode, the same summary printed to the Listing Window/log instead). Do not close NX while it's running.
6. When it's done, check the output folder (the original one, or the new timestamped one if you chose "Copia in nuova cartella"):
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
- The progress preview is enabled by default. It can be disabled with `enableProgressPreview` for unattended runs.
- Previously indexed component `.prt` files are not reopened by default: the journal imports the original STEP once, avoiding one NX open/close cycle per indexed occurrence.
- The final window shows both the concise totals and the complete `log_conversione.txt`, already positioned at the last lines. Once the log is visible the NX journal is allowed to finish immediately; the independent summary window remains open until you close it.
- The incremental log is buffered and flushed at the end of each STEP instead of after every line. When incremental logging succeeds, the finalization step closes the stream without rewriting the complete log.
- If writing the log to file also fails (e.g. the output folder isn't writable), the script doesn't stop: it falls back to writing the log to the Desktop.
- The component index (`component_index.txt`) is append-only and never deduplicated: each line represents one occurrence of a component in an assembly, so repeated components keep their correct quantity across runs. In "Copia in nuova cartella" mode, the new folder starts with its own empty index — it does not inherit the original folder's history.
- Multi-lump body separation is not yet functional (`trySeparateMultiLumpBodies = false`, no-op stub) — see the comment above `TrySeparateMultiLumpBodies` in the source for how to help complete it by recording a journal of a manual "Separate Bodies" operation in your NX version.
- **Why a separate PowerShell process instead of an in-process window.** On some NX installations, *any* `System.Windows.Forms.Form` created directly inside the NX process crashes the moment it's shown (`Form.ShowDialog` → `Form.CreateHandle` → `Form.UpdateWindowIcon` → `MissingMethodException` on `System.Drawing.Icon`'s constructor), due to a version mismatch between the `System.Windows.Forms` and `System.Drawing.Common` assemblies loaded by that process. This was confirmed to be structural on at least one installation — it reproduces identically even right after a full NX restart and with a preload-based mitigation in place, so there was no code-level fix for it *inside* the NX process. `powershell.exe` (the classic, .NET-Framework-based Windows PowerShell, not PowerShell 7/`pwsh.exe`) runs as its own separate process with its own consistent set of Framework assemblies, so the same mismatch can't occur there. The journal writes a small `.ps1` script to a per-run temp folder (under `%TEMP%`, no administrator rights needed), launches it with `-ExecutionPolicy Bypass` (scoped to that single process only — this does not change any system or user policy, and doesn't need elevation), and waits for it to close before continuing, exchanging data through simple `key=value` text files instead of any in-process API. `-WindowStyle Hidden`/`CreateNoWindow` keep PowerShell's own console from flashing on screen; the WinForms window the script creates is unaffected by that and shows normally.
- **Automatic fallback.** If launching `powershell.exe` fails for any reason (not installed, blocked by a security policy, or anything else), the journal logs it and transparently switches to a flow built only from `MessageBox` and `FolderBrowserDialog` shown directly inside NX (native Windows dialogs, not a managed `Form`, so unaffected by the crash described above). This fallback has the same "ask only if there's a real decision to make" behavior as the PowerShell windows, plus the same practical features:
  - **Progress**: the `[i/totale]` counter is printed to the Listing Window (and the log file) for every STEP file.
  - **Cancel**: create a file named `CANCEL.txt` in the output folder while the batch is running — it's picked up between one STEP file and the next (never mid-export), the batch stops cleanly, and the marker file is deleted automatically. This works identically whether the PowerShell GUI or the fallback started the run, since the conversion itself never knows which one was used.
  - **Advanced options**: right before the conversion starts, a single Yes/No dialog lets you turn the export of open (non-solid) bodies on or off for that run. The three STL tolerances stay at their configured defaults in the fallback (not worth the risk of adding more untested UI components for a niche setting) — edit the `chordalTol`/`adjacencyTol`/`angularTol` fields near the top of the source if you need to change them.
