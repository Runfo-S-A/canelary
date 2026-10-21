# Tasks

## 1. Reliable command history

- [x] 1.1 Capture the command and printer mode before generation and pass the captured command through execution and history saving; verify reuse of a selected dropdown entry cannot insert blank text or erase retained entries.
- [x] 1.2 Filter null/blank/duplicate cached entries, preserve MRU order and the 10-entry cap, skip whitespace-only commands, and restore command input after collection updates; add and run a focused regression check covering new entries, reuse, blank input, dirty JSON arrays, and truncation.
- [x] 1.3 Await command completion asynchronously, recognize start errors and nonzero exit codes, and report command and cache-write failures separately without suppressing the later folder offer; verify with successful and failing harmless commands and an unwritable cache.
- [x] 1.4 Verify the actual WPF ComboBox flow by selecting, running, and reopening a cached command, then clearing the input and running again; record the observed binding behavior and confirm retained history survives both runs.

## 2. Image completion and folder opening

- [x] 2.1 Return successful image-save counts from image generation and aggregate safely across parallel workers using the captured absolute output path; verify zero images, partial save failure, and multiple products count actual saves rather than old files.
- [ ] 2.2 Add the Spanish Yes/No folder prompt after completed PNG generation and optional command handling, open the captured directory on Yes, and report launch errors without retrying generation; verify Yes, No, failed CMD, and missing-folder cases and confirm the sample dialog closes afterward.
- [ ] 2.3 Verify physical printing, no selected products, and no valid image bytes never show the folder prompt, even with an existing output folder; record the manual results alongside the completion checks.

## 3. Integration validation

- [ ] 3.1 Run `dotnet build Canelary.sln -c Debug` and the focused regression check; verify a new command and a reused command survive reopening the dialog and that PNG completion offers the folder with both blank and failing optional commands. Record any environment-dependent checks that could not run.

## Verification record

- Debug solution build passed with zero warnings/errors; focused WPF checks passed. Commands, coverage, and remaining desktop steps are recorded in `checks/GenerateSample/README.md`.
- Tasks 2.2 and 3.1 have their implementation/build portions in place but remain unchecked pending the complete popup/Explorer interaction in the running application. Task 2.3 also retains its required desktop verification.
