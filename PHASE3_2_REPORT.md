# Battery Doctor Phase 3.2 — Sensor Diagnostics

Version: **0.9.0**

## Goal

Add battery-pack temperature and derived electrical diagnostics without pretending that generic CPU/GPU/ACPI temperatures are battery data.

## Added

- Native Windows battery temperature query using the battery class device interface.
  - Enumerates battery interfaces with SetupAPI.
  - Acquires the current battery tag.
  - Queries `BatteryTemperature` through `IOCTL_BATTERY_QUERY_INFORMATION`.
  - Converts tenths Kelvin to Celsius.
- Battery-specific `ROOT\WMI` `BatteryTemperature` fallback when supported by the OEM driver.
- No fallback to CPU, GPU, motherboard, or generic ACPI thermal zones.
- Estimated signed pack current using `I ≈ P / V`.
- Dynamic resistance estimate from valid short discharge load steps using `|ΔV / ΔI|` and a median filter.
- Five-minute battery-temperature change and rate.
- Descriptive Pearson correlation between pack power and battery temperature when enough variation exists.
- Live battery-temperature and estimated-current charts.
- Battery Test summary now includes:
  - average/min/max temperature,
  - temperature change,
  - average estimated current,
  - dynamic resistance estimate,
  - power↔temperature correlation.
- HTML/JSON test and recovered-collapse reports include temperature/current where available.
- SQLite history gains optional `temperature_c` and `estimated_current_a` columns through additive migration.
- Existing JSONL collapse sessions remain readable because `TemperatureC` is optional.

## Interpretation limits

Battery temperature is shown only when Windows/OEM firmware exposes a battery sensor. `Not reported` is a valid result.

Estimated current and dynamic resistance are derived pack-level values. They are useful for trends and abnormal-load comparison, but they are **not** direct current-sensor or laboratory cell-impedance measurements.

Power↔temperature correlation is descriptive. Battery thermal response can lag electrical load, so it is not used as a standalone fault verdict.

## Contributor documentation

All new methods include XML summaries and non-obvious native/diagnostic logic includes inline comments. `docs/FUNCTION_REFERENCE.md` was regenerated for this source revision.
