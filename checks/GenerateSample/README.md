# Generate Sample regression checks

Run on Windows with the .NET 10 SDK, from the repository root:

```powershell
dotnet build Canelary.sln -c Debug
dotnet run --project checks/GenerateSample/GenerateSample.csproj
```

The dependency-free check executable references the application's Debug assemblies. It uses a unique temporary directory, without changing application settings or the real command cache. It instantiates a WPF editable ComboBox on an STA dispatcher, checks history persistence, executes harmless CMD commands, and acknowledges only its own error dialogs. It also checks successful image saves, empty images, partial failures, and parallel save counts. No backend or printer is contacted.

## Remaining desktop checks

With a locally configured application and disposable sample labels:

1. Select **To PNG**, choose products, and leave CMD blank. Generate, answer No to the folder question, and confirm the sample dialog closes.
2. Repeat and answer Yes. Confirm Explorer opens the actual output folder and the sample dialog closes.
3. Repeat with `exit /b 7`. Confirm the command error appears before the folder question; accepting must still open the folder.
4. On a disposable output directory only, arrange for it to be removed after generation. Accept the folder question and confirm a folder error appears, with no crash or regeneration.
5. Generate with no selected products, then with a preview result containing no valid image bytes; neither should ask about the folder, even with old images present.
6. Confirm physical printing never executes CMD or asks about the image folder.

## Results (2026-10-15)

- Solution Debug build: passed, zero warnings or errors.
- Regression executable: passed, including actual ComboBox selection, command execution, reload, blank runs, and forced synchronous text resets during history updates.
- Full application folder prompt/Explorer and physical-printer workflows: not exercised. These remain unchecked in the OpenSpec task list.
