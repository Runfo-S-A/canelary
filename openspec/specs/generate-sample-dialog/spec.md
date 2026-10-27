## Purpose

Behavior of the "Generar Muestra" dialog (`Cohere/Views/Dialogs/GenerateSample.xaml`) — printer selection, conditional CMD command input with MRU history, and the print action.

## Requirements

### Requirement: Single print action
The "Generar Muestra" dialog SHALL present a single primary action, labeled "Imprimir", docked to the left of the button row. The dialog SHALL NOT provide a separate "Cancelar" button.

#### Scenario: User confirms printing
- **WHEN** the user clicks "Imprimir"
- **THEN** the system prints/generates the selected products using the current printer, CMD command (if applicable), and then closes the dialog

### Requirement: Conditional CMD command input
The CMD command input SHALL be visible only when the selected printer is `"To PNG"`. For any other printer selection, the CMD command input SHALL be hidden and its value SHALL NOT be applied during printing.

#### Scenario: "To PNG" printer selected
- **WHEN** the user selects "To PNG" as the printer
- **THEN** the CMD command input becomes visible

#### Scenario: Physical printer selected
- **WHEN** the user selects a printer other than "To PNG"
- **THEN** the CMD command input is hidden and no post-print command is executed

### Requirement: CMD command history
The CMD command input SHALL be an editable dropdown backed by a most-recently-used (MRU) history of up to 10 unique, nonblank commands, persisted across sessions. Selecting an entry SHALL populate the input; users MAY also type a command. Saving history SHALL preserve the executed command despite dropdown updates. Blank input SHALL neither execute nor alter persisted history.

#### Scenario: Command history populated
- **WHEN** the dialog opens and a command history exists
- **THEN** the dropdown lists up to 10 previously used commands, most recently used first
- **AND** null, empty, whitespace-only, and duplicate entries are excluded

#### Scenario: New command runs and is saved
- **WHEN** the user enters a command not present in history and it runs successfully after printing
- **THEN** the command is added to the top of the history; if the history exceeds 10 entries, the oldest entry is dropped

#### Scenario: Reusing an existing command
- **WHEN** the user selects or re-enters a command that already exists in history
- **THEN** the history is reordered so that command becomes the most recent entry, without creating a duplicate

#### Scenario: Dropdown changes during history update
- **WHEN** saving a used command changes the dropdown selection or text
- **THEN** the executed command is saved intact and remains in the input
- **AND** other retained commands are preserved across reopening the dialog

#### Scenario: Empty command input
- **WHEN** the user generates PNG images with empty or whitespace-only command input
- **THEN** no command executes and the persisted history remains unchanged

#### Scenario: Failed command
- **WHEN** a command cannot start or exits with a nonzero exit code
- **THEN** its failure is reported to the user and it is not added or promoted in history

### Requirement: Open generated images folder
After a run saves at least one PNG image and its optional command finishes, the system SHALL ask whether to open the output folder. Accepting SHALL open that folder; declining SHALL finish without opening it. The prompt SHALL also appear when the optional command fails, with the command failure reported separately.

#### Scenario: User accepts the folder prompt
- **WHEN** PNG generation finishes and the user accepts the folder prompt
- **THEN** the folder containing the generated PNG images opens
- **AND** the Generate Sample dialog closes

#### Scenario: User declines the folder prompt
- **WHEN** PNG generation finishes and the user declines the folder prompt
- **THEN** no folder opens and the Generate Sample dialog closes

#### Scenario: Optional command fails after images are saved
- **WHEN** the run saves PNG images and its optional command fails
- **THEN** the command failure is reported separately
- **AND** the user is still offered the output folder

#### Scenario: No optional command
- **WHEN** the run saves PNG images with blank command input
- **THEN** the folder prompt appears after generation finishes

#### Scenario: Physical printing or no new images
- **WHEN** the run uses a physical printer or saves no PNG images
- **THEN** no folder prompt appears, even if images from an earlier run exist

#### Scenario: Folder cannot be opened
- **WHEN** the user accepts but the output folder is missing or cannot be opened
- **THEN** the system reports the folder-opening error without crashing or retrying generation
