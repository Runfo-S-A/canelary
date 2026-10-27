# Proposal

## Why

The Generate Sample command history can lose the command being saved when editable dropdown updates change its bound text. Users also need an easy way to find generated PNG images once generation finishes.

## What Changes

- Preserve the executed command while updating the existing JSON MRU cache; ignore blank commands and load only valid, unique entries, capped at 10.
- Offer to open the output folder after a run writes PNG images and its optional command finishes.
- Still offer the folder when the optional command fails, reporting that failure separately.
- Skip the folder prompt for physical printing and runs that produce no images.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `generate-sample-dialog`: reliable nonblank command history and an optional folder-opening prompt after image generation.

## Impact

Changes are confined to sample generation in `Cohere/ViewModels/GenerateSampleViewModel.cs`, its editable ComboBox binding in `Cohere/Views/Dialogs/GenerateSample.xaml` if necessary, and focused regression checks. Keep `after_command.cache`, its JSON array format, the 10-entry limit, and the current `output` directory. No new dependencies, configuration, or legacy plain-text cache migration.
