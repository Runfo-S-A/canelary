# Repository Guidelines

## Project Structure & Module Organization

Canelary is a Windows WPF application for editing ZPL labels, built with .NET 10 and Prism. `Canelary.sln` contains all projects.

- `Main/`: executable shell, startup, settings, and shared visual assets in `Resources/`.
- `Core/`: shared controls, events, database contexts, models, and service contracts (`IBackend`, `IPreview`, `IVersion`).
- `Editor/`, `Comparator/`, `Cohere/`, `Publish/`: editing, comparison, validation, and publishing modules.
- `Backend.Twins/`, `Preview.Labelary/`, `Version.Database/`, `Version.Git/`: replaceable service implementations.
- Module code follows `Views/`, `ViewModels/`, `Models/`, and `Services/` conventions. `openspec/` stores specifications and change artifacts; `REQUIREMENTS.md` describes behavior.

## Build, Test, and Development Commands

Use Windows with the .NET 10 SDK and WPF development support. Run from the repository root:

```powershell
dotnet restore Canelary.sln
dotnet build Canelary.sln -c Debug
dotnet run --project Main/Main.csproj --no-build
```

Restore retrieves NuGet packages. Build the entire solution: module build targets copy assemblies into the shell's output `Modules/` directory. The run command starts the shell using that build. Use `-c Release` for a release build.

## Coding Style & Naming Conventions

Follow `.editorconfig`: spaces, four-space C# indentation, two-space XML indentation, file-scoped namespaces, and opening braces on new lines. Match namespaces to folders. Use PascalCase for types, methods, and properties; `I`-prefixed interfaces; camelCase locals and parameters; and `_camelCase` private instance fields. Respect nullable annotations and existing MVVM/Prism patterns. Use editor formatting based on `.editorconfig`; avoid unrelated reformatting.

## Testing Guidelines

No automated test project, test framework, coverage threshold, or test naming convention is currently defined. Build the solution and manually exercise affected workflows. For editor changes, check opening, saving, switching tabs, and preview generation. Include reproduction steps and validation results in the PR; verify service-dependent behavior with appropriate local configuration.

## Commit & Pull Request Guidelines

History uses short English and Spanish subjects, occasionally with prefixes such as `chore:`; no uniform format is evident. Write concise, descriptive subjects and keep commits focused. PRs should explain the behavior changed, link relevant issues or OpenSpec changes, describe validation, and include screenshots for UI changes.

## Security & Configuration

Review `Main/Settings.yaml` and provider-specific YAML settings before running. Keep credentials and production connection strings out of commits. Use disposable labels and test databases when validating publishing or versioning operations.
