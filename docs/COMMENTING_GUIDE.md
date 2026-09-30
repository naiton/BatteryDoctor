# Code commenting guide

Battery Doctor favors comments that explain **intent, assumptions, and failure behavior**, not comments that merely repeat a C# statement.

## XML documentation

Every method/constructor in the maintained source should have a short `/// <summary>` that answers:

- Why does this function exist?
- What important side effect or constraint does it have?
- For diagnostic functions, what does the result mean (and what does it *not* prove)?

Example:

```csharp
/// <summary>
/// Integrates discharge power over time with the trapezoidal rule while ignoring
/// invalid or excessively large sample gaps.
/// </summary>
private static double? EstimateEnergyWh(...)
```

## Inline comments

Use inline comments for things that are not obvious from the statement itself:

```csharp
// Environment.TickCount64 is monotonic since the current Windows boot. Comparing
// boot time with the last flushed sample helps distinguish a real reboot from
// simply closing/reopening Battery Doctor in the same boot.
```

Good subjects for inline comments:

- why a threshold/window exists,
- why a median/average is used,
- why data is clamped or rejected,
- privacy decisions,
- crash-safety/durability decisions,
- fallback/provider behavior,
- compatibility/migration behavior.

Avoid low-value comments such as:

```csharp
// Increment i
 i++;
```

## When changing behavior

If you change an algorithm, update all three when applicable:

1. source XML/inline comments,
2. `docs/ARCHITECTURE.md`,
3. `docs/FUNCTION_REFERENCE.md` or its generated/maintained entry.

Comments that describe old behavior are more harmful than missing comments.


## Sensor provenance

When adding a hardware/sensor field, document **where the value comes from** and whether it is direct or derived.

Good:

```csharp
// BatteryTemperature comes from the battery-class IOCTL. Do not replace it with
// CPU/GPU/generic ACPI temperature when the battery driver does not expose a sensor.
```

For derived metrics such as current (`P/V`) or dynamic resistance (`ΔV/ΔI`), comments and UI labels must say **estimated** and describe the assumptions/quality filters.
