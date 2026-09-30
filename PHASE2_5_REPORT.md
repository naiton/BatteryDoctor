# Battery Doctor — Phase 2.5 Combined Update

Version: 0.3.0

This update intentionally combines the missing window controls with the next diagnostic work, so there is only one patch to apply.

## Included

- Custom title-bar controls: minimize, maximize/restore, close.
- Drag the title area to move the window and resize from the window edges.
- Battery-health display now uses a recent median to reduce small firmware-estimate jumps.
- Health is displayed with one decimal place instead of rounding 34.x to 34/35 on successive reads.
- Dashboard runtime now has an immediate estimate, then automatically upgrades to a 1-minute and longer moving-average estimate as valid samples accumulate.
- Live Monitor separates polling samples from valid power samples.
- Live Monitor explains when the AC adapter is connected or the firmware does not expose charge/discharge watts.
- Battery Test now tracks start/end charge, estimated Wh consumed, average voltage, reported capacity decrease, valid power samples and a provisional/final assessment.
- Test assessment distinguishes capacity wear from short-term discharge stability.
- Finished tests can be exported as both HTML and JSON under `Documents\BatteryDoctor\Reports`.
- Exported reports contain battery metadata, summary metrics, observations and the recorded sample table.
- Thai and English strings are included for all new UI/report text.

## Assessment rules

The test assessment is intentionally conservative. A short session does not claim that a battery is healthy. A capacity-worn battery can still receive a result such as "capacity heavily worn, short-term discharge looks stable" when the observed discharge does not show a major sudden-drop symptom.

The app still relies on values exposed by Windows and laptop firmware. It does not perform cell-level laboratory measurements.

## Validation performed in this environment

- Main XAML and App XAML parse as valid XML.
- Thai and English localization files parse as valid JSON.
- Referenced view-model bindings were checked against the view model.
- Source tree was checked for duplicate class/record definitions.

This environment is Linux and does not contain the Windows WPF/.NET SDK, so the final WPF compile must still be run on the target Windows machine.
