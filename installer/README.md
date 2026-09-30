# Battery Doctor installer

Phase 3 uses Inno Setup 6 to create a per-user installer, so installation does not require administrator rights.

## Build

```powershell
winget install JRSoftware.InnoSetup
.\PUBLISH-RELEASE.ps1 -BuildInstaller
```

Outputs are written to `artifacts\release`.

The application itself manages the optional **Start with Windows** setting under the current user's Run key.
