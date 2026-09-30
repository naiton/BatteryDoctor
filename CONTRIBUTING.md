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

## Documentation expectations

Before changing behavior, read [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

When adding or changing a function:

1. keep or add a useful `/// <summary>` that explains purpose and important side effects,
2. add inline comments for thresholds, fallback behavior, durability/privacy decisions, or non-obvious algorithms,
3. update architecture/reference docs when responsibilities or algorithms change, and
4. avoid comments that merely repeat the C# statement.

See [`docs/COMMENTING_GUIDE.md`](docs/COMMENTING_GUIDE.md) for examples.
