# Battery Doctor source documentation update

## Goal

Make the codebase easier for outside contributors to understand before changing battery diagnostics, portable storage, crash recovery, reports, or UI behavior.

## Added to source

- File-responsibility comments on the maintained C# files.
- XML `/// <summary>` documentation on detected methods and constructors.
- Additional inline comments around non-obvious behavior such as:
  - sudden percentage-drop windows,
  - rolling-median voltage-sag detection,
  - reboot-vs-app-restart recovery logic,
  - reliability-score penalties,
  - write-through/flush crash safety,
  - copy-only portable migration,
  - multi-provider Windows battery telemetry,
  - report privacy boundaries.
- Extra comments on firmware `HealthPercent` vs reliability diagnostics.
- Comments explaining compact local HTML-formatting helper functions.

## Contributor documentation

- `docs/ARCHITECTURE.md` — end-to-end data flow and component responsibilities.
- `docs/FUNCTION_REFERENCE.md` — function-by-function responsibility reference.
- `docs/COMMENTING_GUIDE.md` — rules/examples for keeping comments useful and current.
- `CONTRIBUTING.md` now includes documentation expectations.
- `README.md` links directly to the developer documentation.

## Documentation guard

`tools/Check-CodeDocumentation.ps1` checks maintained C# methods/constructors for an adjacent XML documentation summary. GitHub Actions runs this check before restore/build so future pull requests do not silently add undocumented functions.

The project also enables compiler XML-documentation output (`GenerateDocumentationFile`) while suppressing CS1591 for UI binding properties that do not require a full public API comment.

## Static validation performed

- 168 detected methods/constructors checked.
- 0 detected methods/constructors missing XML summaries.
- JSON language/config files parsed successfully.
- `BatteryDoctor.csproj`, `App.xaml`, `MainWindow.xaml`, and `app.manifest` parsed as XML successfully.
- Gross C# brace-balance check passed.

A real WPF compile still needs to run on Windows because the current build environment does not have the Windows/.NET WPF SDK.
