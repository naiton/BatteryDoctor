# Battery Doctor — Phase 2.6 / v0.4.0

Phase 2.6 focuses on the failure mode where Windows still reports a high battery percentage but the laptop suddenly powers off and later returns near 0%.

## Main additions

### 1. Sudden Collapse Watch

Battery Test now has four modes:

- Quick — 5 minutes
- Standard — 15 minutes
- Deep — 30 minutes
- Collapse Watch — runs until stopped or power is lost

During a running test, Battery Doctor samples about every 2 seconds.

### 2. Durable crash / power-loss journal

Every test sample is appended to:

`%LOCALAPPDATA%\BatteryDoctor\sessions`

The JSONL sample is flushed to disk immediately. If the laptop loses power unexpectedly, the next launch can inspect the unfinished test.

Recovery deliberately avoids claiming a battery collapse when:

- the app was only closed and Windows did not restart; or
- Windows restarted long after the last test sample.

A recovered event is surfaced when Windows restarted within about five minutes of the final saved discharge sample. A stronger warning is shown when the last saved percentage was still high and the post-restart percentage is much lower.

### 3. Measured usable health after a strong collapse

If a Collapse Watch session starts at roughly 85% or above and a strong post-restart gauge jump is observed, Battery Doctor estimates:

`measured usable health = measured energy before cutoff / design capacity × 100`

This is intentionally separate from firmware-reported battery health.

### 4. Broader collapse anomaly detection

The detector now checks both:

- sudden short-window percentage drops; and
- drops of 25+ percentage points within roughly 30 minutes.

The second rule is designed to catch cases such as a battery that shows around 90% but reaches cutoff in less than 15 minutes.

### 5. Test confidence and data consistency

Battery Test now reports:

- Test confidence: Very low / Low / Medium / High
- Energy agreement between integrated discharge power and firmware-reported capacity decrease
- Data consistency: Good / Fair / Poor / Not enough data
- Estimated usable capacity and usable-health percentage after a sufficiently large charge drop (10%+)

Short tests deliberately remain low-confidence.

### 6. UI correctness fixes

- Current charge is clamped to 0–100% for display, while raw hardware values can still remain in diagnostic data.
- Wear is clamped to 0–100% and displayed with one decimal place.
- The recovered-collapse alert appears on the Dashboard and Battery Test tab.

## Safety / interpretation

Battery Doctor only sees information exposed by Windows and the laptop firmware. It cannot directly measure individual cell-group voltages on most laptops.

A sudden-collapse event can be consistent with:

- battery gauge / BMS estimation error;
- severe cell imbalance;
- high internal resistance causing voltage sag and cutoff; or
- another battery-pack protection event.

The app reports observed behavior and confidence rather than claiming a specific failed cell.

## Files written locally

Long-term history:

`%LOCALAPPDATA%\BatteryDoctor\battery-doctor.db`

Collapse-test recovery data:

`%LOCALAPPDATA%\BatteryDoctor\sessions`

Crash logs:

`%LOCALAPPDATA%\BatteryDoctor\logs`

Exported reports:

`Documents\BatteryDoctor\Reports`

No server, account, telemetry, or internet connection is required for battery analysis.
