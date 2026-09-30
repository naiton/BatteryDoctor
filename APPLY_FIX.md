# Battery Doctor Phase 3.2 - Fix 1

Fixes C# compiler error CS0173 in `Services/ElectricalDiagnosticsAnalyzer.cs`.

## Cause

`var` could not infer a common type for this conditional expression:

```csharp
var dynamicResistance = resistanceCandidates.Count == 0 ? null : Median(resistanceCandidates);
```

The branches are `null` and `double`, so the variable now explicitly uses nullable double:

```csharp
double? dynamicResistance = resistanceCandidates.Count == 0 ? null : Median(resistanceCandidates);
```

## Apply

Copy:

`Services/ElectricalDiagnosticsAnalyzer.cs`

over the existing file in your Battery Doctor project, then run:

```powershell
dotnet clean
dotnet build
```
