# Contributing

Issues and pull requests are welcome.

## Build requirements

- Windows 10/11 x64
- .NET 10 SDK

```powershell
dotnet restore
dotnet build
dotnet run
```

Please do not commit files from `Data/`, `Sessions/`, `Reports/`, or `Logs/`; these can contain machine-specific battery history.
