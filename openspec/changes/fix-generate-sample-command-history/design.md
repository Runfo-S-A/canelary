# Design

## Context

See `proposal.md` for motivation. `GenerateSampleViewModel` loads a JSON array into an `ObservableCollection<string>` bound to an editable ComboBox. Saving removes the current command and then reads the bound `AfterCommand` again when inserting it. Collection notifications can change that text synchronously; this is the suspected cause, pending WPF reproduction. Blank strings are checked, but whitespace is accepted. Legacy plain-text caches are deliberately ignored.

Generation uses `Parallel.ForEachAsync`, writes to `.\output`, then synchronously waits for an optional CMD command. `GenerateImage` catches save errors and returns no outcome; checking directory existence cannot prove this run wrote images. There is no reusable confirmation dialog found in the inspected code. The existing `IDialogService` has no registered general confirmation view.

## Goals / Non-Goals

**Goals:** Isolate a run from mutable UI input and make completion reflect actual image writes. Keep the existing cache and output paths, MVVM arrangement, and dependencies.

**Non-Goals:** Cache relocation or migration, a new dialog framework, configurable output paths, a progress/cancellation redesign, and unrelated module refactoring.

## Decisions

1. **Capture run inputs once.** Capture printer mode, command text, and absolute output directory before asynchronous generation. Pass the command explicitly to execution and persistence instead of rereading the bound property. Keep exact nonblank command text and ordinal equality; do not trim executable text. Restore the input after history updates. Replacing the whole collection or adding a control wrapper is unnecessary.
2. **Sanitize history at its existing boundary.** Load only nonblank, unique entries in stored order, taking 10. Build the MRU result from the captured successful command, persist that result, then update the observable collection. Empty or whitespace-only input never writes the cache. Retain current missing/malformed/legacy JSON behavior; report cache I/O failures separately from command execution failures so a successful command is not described as failed.
3. **Use exit status as success.** A start exception or nonzero exit code is a command failure and does not promote history. Report it separately on the UI thread, then continue to the folder offer if images were written. Use asynchronous process waiting to avoid blocking the UI while waiting for the command. Do not introduce command parsing or shell escaping changes: the input remains an intentional CMD command.
4. **Track successful saves in this run.** Make `GenerateImage` return the number of successful saves, including any completed before a later save error. Aggregate counts safely across parallel iterations. Return that outcome to `ClosingDialog`; do not infer success from old files or directory existence. Preserve existing image-error handling; do not turn an aborted generation into a success prompt.
5. **Use a standard WPF Yes/No popup.** After completed PNG generation and optional command handling, ask `¿Desea abrir la carpeta de imágenes generadas?` on the UI thread when the saved count is positive. On Yes, open the captured absolute output path using `ProcessStartInfo` with `UseShellExecute = true`. Catch and report folder-opening errors, including a folder removed by the command. Finish by closing the sample dialog. A new Prism confirmation component adds unnecessary files for this one interaction.

## Risks / Trade-offs

- [Suspected binding reentrancy] -> Reproduce reuse of a selected history entry with the real ComboBox; snapshotting protects persistence regardless of the observed notification order.
- [Parallel save counts and partial failures] -> Count only successful saves and aggregate with thread-safe operations; an existing directory alone must not enable the prompt.
- [Post-command moves or deletes output] -> Handle opening failures explicitly; do not recreate the folder or rerun generation.
- [No existing test project] -> Add one focused assert-based history regression check using existing tooling where feasible, plus a manual WPF check for binding and folder interaction; avoid creating a broad testing framework.

## Migration Plan

No data migration or settings changes. Existing JSON history remains readable. Deployment and rollback use the normal application build; previously erased commands cannot be reconstructed.
