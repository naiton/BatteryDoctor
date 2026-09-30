# Function reference

This reference is generated from the XML summaries in the source tree. It gives contributors a quick map of **what each function is responsible for** without having to infer behavior from implementation details first.

Current documented method/constructor count: **194**.

> Line numbers are approximate navigation aids for this source revision and will move as the code changes. The source `/// <summary>` beside each function remains the authoritative explanation.

## `App.xaml.cs`

**Responsibility:** Application bootstrap and last-resort exception handling. Initializes portable storage before the main window is created.

### `OnStartup` — line ~17

`protected override void OnStartup(StartupEventArgs e)`

Initializes portable folders and global exception hooks, then creates the main window. The --background switch starts the window hidden when background monitoring is requested.

### `OnDispatcherUnhandledException` — line ~48

`private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)`

Handles otherwise-unhandled WPF UI exceptions, writes a crash log, informs the user, and marks the exception handled so the diagnostic message can be seen.

### `OnDomainUnhandledException` — line ~62

`private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)`

Records process/AppDomain exceptions that bypass the WPF dispatcher.

### `OnUnobservedTaskException` — line ~71

`private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)`

Records unobserved Task exceptions and marks them observed to avoid escalation during finalization.

### `WriteCrashLog` — line ~80

`private static string WriteCrashLog(string area, Exception ex)`

Writes diagnostic exception details to the portable Logs folder and returns the created path (or a safe fallback message if logging itself fails).

## `Controls/TrendChart.cs`

**Responsibility:** Lightweight WPF chart renderer used for battery history/live trends without a third-party chart dependency.

### `OnPointsChanged` — line ~45

`private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)`

Rewires collection-change notifications when the chart data source changes, then invalidates the visual for repaint.

### `OnPointsCollectionChanged` — line ~58

`private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)`

Requests a repaint whenever an observable source collection is modified.

### `OnRender` — line ~82

`protected override void OnRender(DrawingContext dc)`

Normalizes valid trend points into chart coordinates, draws grid/axes labels, and renders the time-series polyline.

### `DrawText` — line ~169

`private void DrawText(DrawingContext dc, string text, WpfBrush brush, double size, WpfPoint point)`

Draws one formatted text label at a WPF point using the current display DPI.

### `MakeText` — line ~175

`private FormattedText MakeText(string text, WpfBrush brush, double size)`

Creates DPI-aware FormattedText using the current UI culture and left-to-right flow.

## `MainWindow.xaml.cs`

**Responsibility:** WPF window shell. Owns window chrome, the polling timer, system-tray integration, and forwards UI lifecycle events to MainViewModel.

### `MainWindow` — line ~32

`public MainWindow(bool startHidden = false)`

Builds the WPF shell, binds MainViewModel, configures the monitor timer, and creates the WinForms NotifyIcon/context menu used by system-tray mode.

### `OnLoaded` — line ~80

`private async void OnLoaded(object sender, RoutedEventArgs e)`

Performs one-time asynchronous view-model initialization, starts periodic monitoring, and optionally hides to tray for background startup.

### `OnMonitorTick` — line ~95

`private async void OnMonitorTick(object? sender, EventArgs e)`

Runs one periodic monitor cycle. Hidden windows skip polling when background monitoring is disabled.

### `OnViewModelPropertyChanged` — line ~108

`private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)`

Keeps tray text/localization and polling cadence synchronized with view-model state changes.

### `UpdateMonitorInterval` — line ~131

`private void UpdateMonitorInterval()`

Uses a faster 2-second poll while a test is running and a lower-overhead 5-second interval during ordinary monitoring.

### `OnClosed` — line ~141

`private void OnClosed(object? sender, EventArgs e)`

Stops timers, detaches handlers, and disposes WinForms tray resources to prevent process/resource leaks.

### `OnMinimizeClick` — line ~156

`private void OnMinimizeClick(object sender, RoutedEventArgs e)`

Handles the minimize click event.

### `OnMaximizeRestoreClick` — line ~164

`private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)`

Handles the maximize restore click event.

### `OnCloseClick` — line ~172

`private void OnCloseClick(object sender, RoutedEventArgs e)`

Handles the close click event.

### `OnTitleBarMouseLeftButtonDown` — line ~180

`private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)`

Implements custom-title-bar drag and double-click maximize/restore behavior.

### `ToggleMaximizeRestore` — line ~199

`private void ToggleMaximizeRestore()`

Switches between maximized and normal window states.

### `OnWindowStateChanged` — line ~209

`private void OnWindowStateChanged(object? sender, EventArgs e)`

Updates the custom maximize icon and moves minimized windows to the tray when that preference is enabled.

### `HideToTray` — line ~221

`private void HideToTray()`

Hides the WPF window from the taskbar while keeping the process and background monitor alive.

### `RestoreFromTray` — line ~240

`private void RestoreFromTray()`

Restores, activates, and focuses the WPF window after a tray interaction.

### `UpdateTrayLocalization` — line ~255

`private void UpdateTrayLocalization()`

Refreshes tray-menu labels from the currently selected localization dictionary.

### `UpdateTrayText` — line ~266

`private void UpdateTrayText()`

Builds the short NotifyIcon tooltip from current charge and power state, respecting Windows tooltip length limits.

### `OnBackgroundAlertRaised` — line ~275

`private void OnBackgroundAlertRaised(object? sender, BackgroundAlertEventArgs e)`

Maps a diagnostic alert severity to a Windows tray balloon icon and displays the alert on the UI dispatcher.

### `ShowTrayBalloon` — line ~289

`private void ShowTrayBalloon(string title, string detail, Forms.ToolTipIcon icon)`

Displays one tray balloon notification with the supplied title, detail text, and severity icon.

## `Models/BackgroundAlertEventArgs.cs`

**Responsibility:** Event payload for a user-visible background/tray battery warning.

### `BackgroundAlertEventArgs` — line ~13

`public BackgroundAlertEventArgs(string severity, string title, string detail, DateTimeOffset capturedAt)`

Initializes background alert event args.

## `Services/AppLinksService.cs`

**Responsibility:** Loads optional project/support URLs shipped beside the executable.

### `Load` — line ~17

`public AppLinks Load()`

Reads AppLinks.json from the application directory. Invalid or missing configuration falls back to empty links rather than preventing startup.

## `Services/AppSettingsService.cs`

**Responsibility:** Loads and atomically saves user preferences in the portable Data folder.

### `AppSettingsService` — line ~19

`public AppSettingsService()`

Initializes the portable settings location under Data.

### `Load` — line ~30

`public AppSettings Load()`

Deserializes settings.json; corrupt/missing settings safely fall back to defaults.

### `Save` — line ~46

`public void Save(AppSettings settings)`

Serializes preferences to a temporary file and atomically replaces settings.json to reduce corruption risk.

## `Services/BatteryAnomalyDetector.cs`

**Responsibility:** Detects short-term charge, voltage, and power anomalies from recent battery telemetry.

### `Analyze` — line ~15

`public IReadOnlyList<AnomalyFinding> Analyze(IReadOnlyList<BatterySnapshot> samples)`

Scans recent snapshots for sudden percentage drops, rapid collapse patterns, large short-term voltage swings, and unusual power spikes.

## `Services/BatteryHealthAnalyzer.cs`

**Responsibility:** Converts firmware-reported capacity/health data into user-facing health findings.

### `Analyze` — line ~15

`public HealthAssessment Analyze(BatterySnapshot s, double? healthOverride = null)`

Classifies firmware-reported health into condition bands and builds explanatory findings without treating the score as proof of electrical stability.

## `Services/BatteryReportExporter.cs`

**Responsibility:** Exports a completed discharge test to privacy-conscious JSON and human-readable HTML.

### `ExportAsync` — line ~19

`public async Task<ExportResult> ExportAsync(`

Exports the supplied diagnostic/test data to JSON and a standalone human-readable HTML report in the portable Reports folder.

### `BuildHtml` — line ~75

`private static string BuildHtml(`

Builds the standalone HTML report from already-derived metrics and localized labels; this method performs presentation only and does not change diagnostic results.

### `Metric` — line ~211

`private static void Metric(StringBuilder sb, string value, string label)`

Appends one value/label metric card to the generated HTML.

### `FormatDuration` — line ~222

`private static string FormatDuration(TimeSpan span)`

Formats the test duration for the exported report.

## `Services/BatteryTemperatureProvider.cs`

**Responsibility:** Reads battery-pack temperature from the Windows battery class IOCTL and an optional battery-specific WMI fallback.

### `TryRead` — line ~36

`public static BatteryTemperatureReading? TryRead()`

Tries the native battery-class API first, then the battery-specific WMI class. Returns null when the OEM battery/driver does not expose temperature.

### `TryReadNative` — line ~58

`private static BatteryTemperatureReading? TryReadNative()`

Queries a cached battery path first, then enumerates battery interfaces only when the cache is empty/stale. The current tag is still reacquired on every read.

### `TryReadDevice` — line ~110

`private static BatteryTemperatureReading? TryReadDevice(string path)`

Opens one battery interface, reacquires its current tag, and returns a validated temperature reading.

### `TryGetDevicePath` — line ~134

`private static string? TryGetDevicePath(IntPtr deviceInfo, ref SpDeviceInterfaceData interfaceData)`

Reads the variable-length SP_DEVICE_INTERFACE_DETAIL_DATA buffer and extracts the Unicode device path.

### `TryQueryBatteryTag` — line ~165

`private static bool TryQueryBatteryTag(SafeFileHandle handle, out uint tag)`

Retrieves the current battery tag. Tags protect subsequent queries from returning data for a battery that changed mid-query.

### `TryQueryTemperature` — line ~182

`private static bool TryQueryTemperature(SafeFileHandle handle, uint tag, out uint rawTemperature)`

Requests BatteryTemperature for the current tag. Windows returns tenths of a degree Kelvin.

### `TryReadBatteryWmi` — line ~205

`private static BatteryTemperatureReading? TryReadBatteryWmi()`

Attempts the battery driver's WMI temperature block only; it intentionally does not use generic ACPI thermal zones.

### `ConvertTenthsKelvinToCelsius` — line ~231

`private static double ConvertTenthsKelvinToCelsius(uint tenthsKelvin)`

Converts the battery API unit (tenths Kelvin) to Celsius.

### `IsPlausibleBatteryTemperature` — line ~237

`private static bool IsPlausibleBatteryTemperature(double celsius)`

Rejects sentinel/corrupt values while allowing a deliberately broad real-world battery range.

### `SetupDiGetClassDevs` — line ~259

`private static extern IntPtr SetupDiGetClassDevs(`

See the source XML summary for this method.

### `SetupDiEnumDeviceInterfaces` — line ~268

`private static extern bool SetupDiEnumDeviceInterfaces(`

See the source XML summary for this method.

### `SetupDiGetDeviceInterfaceDetail` — line ~278

`private static extern bool SetupDiGetDeviceInterfaceDetail(`

See the source XML summary for this method.

### `SetupDiDestroyDeviceInfoList` — line ~289

`private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);`

See the source XML summary for this method.

### `CreateFile` — line ~293

`private static extern SafeFileHandle CreateFile(`

See the source XML summary for this method.

### `DeviceIoControl` — line ~305

`private static extern bool DeviceIoControl(`

See the source XML summary for this method.

### `DeviceIoControl` — line ~318

`private static extern bool DeviceIoControl(`

See the source XML summary for this method.

## `Services/BatteryTestAnalyzer.cs`

**Responsibility:** Calculates discharge-test metrics, energy integration, confidence, consistency, sag severity, and verdict codes.

### `Analyze` — line ~18

`public BatteryTestSummary? Analyze(`

Turns recorded test samples into a summary: integrated energy, average power/voltage, charge drop, confidence, gauge agreement, extrapolated capacity, sag metrics, anomalies, and verdict.

### `GetConfidenceCode` — line ~130

`private static string GetConfidenceCode(TimeSpan duration, int? chargeDrop, int validPowerSamples)`

Assigns a confidence level from test duration, observed charge drop, and count of valid power samples.

### `CalculateAgreementPercent` — line ~144

`private static double? CalculateAgreementPercent(double? integratedEnergyWh, double? capacityDeltaWh)`

Compares watt-time integrated energy against the firmware remaining-capacity delta; high agreement means the two independent estimates are consistent.

### `EstimateEnergyWh` — line ~156

`private static double? EstimateEnergyWh(IReadOnlyList<BatteryTestSample> samples)`

Integrates discharge power over time with the trapezoidal rule while ignoring invalid or excessively large sample gaps.

### `EstimateDynamicResistanceCandidates` — line ~182

`private static List<double> EstimateDynamicResistanceCandidates(IReadOnlyList<BatteryTestSample> samples)`

Estimates pack dynamic resistance from short discharge-load steps using |ΔV / ΔI|. Only opposite-direction voltage/current changes are accepted to reduce noise-driven false values.

### `CalculatePowerTemperatureCorrelation` — line ~215

`private static double? CalculatePowerTemperatureCorrelation(IReadOnlyList<BatteryTestSample> samples)`

Calculates Pearson correlation between discharge power and measured battery temperature. The coefficient is descriptive only because thermal response can lag electrical load.

### `Median` — line ~247

`private static double Median(IReadOnlyList<double> values)`

Returns a median estimate to suppress one-off telemetry spikes.

## `Services/BatteryTestJournal.cs`

**Responsibility:** Crash-safe test journal. Persists every collapse-watch sample and reconstructs interrupted sessions after reboot.

### `BatteryTestJournal` — line ~25

`public BatteryTestJournal()`

Binds the journal to the portable Sessions folder and defines active-test.json as the recoverable session marker.

### `StartSession` — line ~37

`public void StartSession(BatterySnapshot snapshot, string profileCode)`

Creates a new active session state and stores a relative JSONL sample filename so the whole portable folder can be moved safely.

### `AppendSample` — line ~61

`public void AppendSample(BatteryTestSample sample)`

Appends one JSONL sample using write-through and an explicit disk flush so useful evidence survives a sudden battery cutoff.

### `CompleteSession` — line ~84

`public void CompleteSession(string reason)`

Marks a normal test completion, archives its metadata, removes the active marker, and clears in-memory journal state.

### `RecoverInterruptedSession` — line ~100

`public RecoveredTestInterruption? RecoverInterruptedSession(BatterySnapshot currentSnapshot)`

On startup, determines whether an active journal ended close to a real Windows reboot; if so, reconstructs collapse evidence from the last persisted samples.

### `LoadLatestRecoveredSession` — line ~147

`public RecoveredTestInterruption? LoadLatestRecoveredSession(BatterySnapshot currentSnapshot)`

Re-opens the most recent saved recovery result so Phase 2.7 can enrich a collapse that was already captured by Phase 2.6. Missing Phase 2.7 fields are recalculated from the crash-safe JSONL sample file.

### `AnalyzeRecoveredSession` — line ~204

`private RecoveredTestInterruption AnalyzeRecoveredSession(`

Derives gauge jump, observed energy, firmware recalibration, voltage sag, gauge reliability, overall battery reliability, and confidence from a recovered interruption.

### `ScoreToReliabilityCode` — line ~310

`private static string ScoreToReliabilityCode(int score) => score switch`

Maps a numeric reliability score to good/fair/poor/critical.

### `JsonString` — line ~321

`private static string? JsonString(JsonElement root, string name)`

Safely reads a nullable string property from recovery JSON.

### `JsonInt` — line ~327

`private static int? JsonInt(JsonElement root, string name)`

Safely reads a nullable integer property from recovery JSON.

### `JsonDouble` — line ~333

`private static double? JsonDouble(JsonElement root, string name)`

Safely reads a nullable double property from recovery JSON.

### `JsonDate` — line ~339

`private static DateTimeOffset? JsonDate(JsonElement root, string name)`

Safely reads a nullable DateTimeOffset property from recovery JSON.

### `ArchiveRecoveredState` — line ~346

`private void ArchiveRecoveredState(TestJournalState state, string status)`

Archives the interrupted session metadata with a status describing how recovery classified it.

### `ReadSamples` — line ~359

`private static List<BatteryTestSample> ReadSamples(string path)`

Reads the crash-safe JSONL sample stream, tolerating a truncated final line that may result from abrupt power loss.

### `EstimateEnergyWh` — line ~384

`private static double? EstimateEnergyWh(IReadOnlyList<BatteryTestSample> samples)`

Integrates recovered power samples with the trapezoidal rule to estimate energy actually delivered before interruption.

### `ClampPercent` — line ~409

`private static int? ClampPercent(int? value)`

Clamps firmware charge values to the user-visible 0-100 percent range.

### `WriteState` — line ~415

`private static void WriteState(string path, TestJournalState state)`

Persists journal state via a temporary file followed by replace/move so metadata updates are as atomic as practical.

### `ReadState` — line ~436

`private static TestJournalState? ReadState(string path)`

Reads and deserializes journal metadata; malformed state is treated as unavailable instead of crashing startup.

### `JsonOptions` — line ~452

`private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };`

Returns the consistent indented serializer settings used for journal/recovery metadata.

### `Sanitize` — line ~457

`private static string Sanitize(string value)`

Replaces characters that are unsafe in an archive filename.

### `TryDelete` — line ~467

`private static void TryDelete(string path)`

Best-effort deletion used for journal cleanup; cleanup failure must never crash battery monitoring.

## `Services/DiagnosticSnapshotExporter.cs`

**Responsibility:** Creates a privacy-safe diagnostic snapshot for support/debugging without user names, serial numbers, or local paths.

### `ExportAsync` — line ~19

`public async Task<string> ExportAsync(`

Writes a support snapshot that intentionally omits Windows username, battery serial, sample path, and unrelated application/browsing data.

## `Services/ElectricalDiagnosticsAnalyzer.cs`

**Responsibility:** Derives current, dynamic-resistance and thermal/load metrics from recent pack-level telemetry.

### `Analyze` — line ~18

`public ElectricalDiagnosticsAssessment Analyze(IReadOnlyList<BatterySnapshot> samples)`

Analyzes the recent live window and returns current, dynamic resistance, temperature trend, and simple power/temperature correlation metrics when the required source data exists.

### `EstimateSignedCurrentA` — line ~67

`public static double? EstimateSignedCurrentA(BatterySnapshot snapshot)`

Estimates signed pack current from pack power and pack voltage using I = P / V. Positive values mean charging and negative values mean discharging.

### `EstimateDynamicResistanceCandidates` — line ~85

`private static List<double> EstimateDynamicResistanceCandidates(IReadOnlyList<BatterySnapshot> samples)`

Uses short load-current steps to estimate pack dynamic resistance from |ΔV / ΔI|. Pairs are accepted only when current and voltage move in opposite directions, which is the expected response of a battery under a changed discharge load.

### `EstimateDischargeCurrentA` — line ~118

`private static double? EstimateDischargeCurrentA(BatterySnapshot snapshot)`

Calculates positive discharge-current magnitude for dynamic-resistance estimation.

### `CalculatePowerTemperatureCorrelation` — line ~131

`private static double? CalculatePowerTemperatureCorrelation(IReadOnlyList<BatterySnapshot> samples)`

Computes a Pearson correlation between absolute pack power and measured battery temperature. It is shown only when enough variation exists; battery thermal response can lag load, so the coefficient is descriptive rather than a fault verdict.

### `SignedPowerW` — line ~168

`private static double? SignedPowerW(BatterySnapshot snapshot)`

Returns signed pack power in watts using the same convention as the live chart.

### `Median` — line ~180

`private static double Median(IReadOnlyList<double> values)`

Returns the median so a single noisy current/voltage step cannot dominate resistance display.

## `Services/HistoryRepository.cs`

**Responsibility:** SQLite persistence layer for long-term battery health and trend samples.

### `HistoryRepository` — line ~19

`public HistoryRepository()`

Opens the portable SQLite location and ensures the history schema is ready before use.

### `Initialize` — line ~31

`private void Initialize()`

Creates the history table/index and performs additive schema upgrades for newer telemetry fields.

### `AddColumnIfMissing` — line ~66

`private static void AddColumnIfMissing(SqliteConnection connection, string column, string definition)`

Checks PRAGMA table_info and adds a column only when an older database schema does not contain it.

### `AddAsync` — line ~92

`public async Task AddAsync(BatterySnapshot s, CancellationToken cancellationToken = default)`

Persists one normalized battery snapshot into SQLite using parameters and nullable-to-DB conversion.

### `GetRecentAsync` — line ~129

`public async Task<IReadOnlyList<HistoryPoint>> GetRecentAsync(int limit = 30, CancellationToken cancellationToken = default)`

Returns the most recent health/charge history points for compact history displays.

### `GetTrendAsync` — line ~163

`public async Task<IReadOnlyList<HistorySamplePoint>> GetTrendAsync(int days = 90, int limit = 1000, CancellationToken cancellationToken = default)`

Returns one representative sample per day for the requested trend window, including capacity, voltage, and signed power.

### `Db` — line ~224

`private static object Db(object? value) => value ?? DBNull.Value;`

Converts nullable CLR values into DBNull.Value for parameterized SQLite inserts.

## `Services/LocalizationService.cs`

**Responsibility:** Loads language JSON files and provides dictionary-style localized string lookup.

### `LocalizationService` — line ~43

`public LocalizationService()`

Chooses Thai or English from the current Windows UI culture and loads that language at startup.

### `Load` — line ~54

`public void Load(string language)`

Loads one language JSON dictionary, falling back to English if the requested file is missing, then notifies all bindings.

### `OnPropertyChanged` — line ~71

`private void OnPropertyChanged([CallerMemberName] string? name = null)`

Raises INotifyPropertyChanged so WPF refreshes localization-dependent bindings.

## `Services/PortablePaths.cs`

**Responsibility:** Central definition of portable Data/Sessions/Reports/Logs paths plus legacy-data migration helpers.

### `Initialize` — line ~30

`public static void Initialize()`

Creates all portable runtime folders, verifies they are writable, and performs a one-time best-effort legacy-data copy.

### `ResolveSessionPath` — line ~44

`public static string ResolveSessionPath(string? storedPath)`

Resolves a stored session path from either an existing legacy absolute path or a portable relative/sample filename.

### `ToPortableRelativePath` — line ~60

`public static string ToPortableRelativePath(string? path)`

Converts a path under the portable application root to a share-safe relative path; external paths are reduced to filename only.

### `VerifyWritable` — line ~80

`private static void VerifyWritable()`

Performs a real write/delete probe and gives a clear error when the portable app is placed in a protected/read-only directory.

### `TryMigrateLegacyUserData` — line ~100

`private static void TryMigrateLegacyUserData()`

Copies legacy AppData/Documents Battery Doctor files into the portable structure once, never deleting the originals.

### `CopyDirectoryIfPresent` — line ~138

`private static void CopyDirectoryIfPresent(string source, string destination)`

Recursively copies legacy files that do not already exist in the portable destination.

### `CopyFileIfMissing` — line ~153

`private static void CopyFileIfMissing(string source, string destination)`

Copies one legacy file only when the source exists and the destination has not already been created.

## `Services/PowerCfgBatteryReportReader.cs`

**Responsibility:** Fallback reader that invokes powercfg /batteryreport when WMI omits design/full capacity or cycle count.

### `TryRead` — line ~23

`public static BatteryReportFallback? TryRead()`

Runs powercfg /batteryreport to a temporary XML file and extracts capacity/cycle metadata used when WMI providers omit those values.

### `Child` — line ~74

`private static XElement? Child(XElement parent, string localName)`

Finds a child XML element by local name regardless of namespace.

### `Text` — line ~81

`private static string? Text(XElement parent, string localName)`

Reads and trims a battery-report XML element as text.

### `Number` — line ~87

`private static uint? Number(XElement parent, string localName)`

Extracts a positive integer from a battery-report XML field that may contain formatting text.

## `Services/RecoveredCollapseReportExporter.cs`

**Responsibility:** Exports recovered sudden-collapse evidence, reliability scores, and raw samples to JSON/HTML.

### `ExportAsync` — line ~21

`public async Task<ExportResult> ExportAsync(`

Exports recovered collapse evidence and its raw crash-safe samples to privacy-conscious JSON and standalone HTML.

### `BuildHtml` — line ~128

`private static string BuildHtml(`

Builds the standalone HTML report from already-derived metrics and localized labels; this method performs presentation only and does not change diagnostic results.

### `BuildRecoveryDetail` — line ~254

`private static string BuildRecoveryDetail(RecoveredTestInterruption recovery, Func<string, string> text)`

Expands the localized recovered-collapse narrative template with the recorded percentages and energy evidence.

### `FormatFirmwareChange` — line ~268

`private static string FormatFirmwareChange(RecoveredTestInterruption recovery)`

Formats the pre/post-restart FullChargeCapacity change that indicates a large firmware/BMS re-estimation.

### `ReliabilityCode` — line ~279

`private static string ReliabilityCode(int score) => score switch`

Maps a numeric reliability score to the localization code used by reports.

### `ReadSamples` — line ~290

`private static IReadOnlyList<BatteryTestSample> ReadSamples(string path)`

Reads the JSONL sample file and ignores an incomplete final record that can be left by abrupt power loss.

### `Metric` — line ~315

`private static void Metric(StringBuilder sb, string value, string label)`

Appends one value/label metric card to the generated HTML.

## `Services/StartupManager.cs`

**Responsibility:** Portable-build startup policy. Deliberately avoids creating persistent Windows startup state.

### `TrySetEnabled` — line ~15

`public static bool TrySetEnabled(bool enabled, out string? error)`

Portable mode intentionally rejects enabling Windows auto-start so the app leaves no persistent per-user registry state.

### `IsEnabled` — line ~30

`public static bool IsEnabled() => false;`

Always reports auto-start disabled for the portable build.

## `Services/VoltageSagAnalyzer.cs`

**Responsibility:** Detects transient pack-voltage sag relative to a rolling local baseline and converts it to a severity score.

### `Analyze` — line ~20

`public VoltageSagAssessment Analyze(IReadOnlyList<BatteryTestSample> samples)`

Compares each voltage sample with a short rolling median baseline, groups nearby sags into events, and converts sag depth/frequency/terminal sag into a 0-100 severity score.

### `Median` — line ~117

`private static double Median(IReadOnlyList<double> values)`

Returns the median of an already sorted local baseline window, reducing sensitivity to one noisy voltage sample.

## `Services/WindowsBatteryProvider.cs`

**Responsibility:** Windows telemetry provider. Merges ROOT/WMI, Win32_Battery and powercfg fallback data into BatterySnapshot.

### `ReadAsync` — line ~21

`public Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default)`

Reads Windows battery telemetry on a worker thread so WMI/powercfg work never blocks the WPF UI thread.

### `ReadInternal` — line ~29

`private static BatterySnapshot ReadInternal()`

Queries Windows battery providers, merges their fields, applies cached powercfg fallback data, normalizes chemistry, and builds one BatterySnapshot.

### `GetBatteryReportFallback` — line ~92

`private static BatteryReportFallback? GetBatteryReportFallback()`

Caches expensive powercfg battery-report fallback data for 30 minutes.

### `First` — line ~108

`private static ManagementObject? First(string scope, string query)`

Executes a WMI query and returns the first object while treating provider/permission failures as missing optional data.

### `ChemistryText` — line ~130

`private static string? ChemistryText(ManagementBaseObject? obj)`

Decodes WMI chemistry values, including packed four-character codes such as LION, into human-readable chemistry names.

### `Text` — line ~176

`private static string? Text(ManagementBaseObject? obj, string key)`

Reads a trimmed string property from a WMI object.

### `UInt` — line ~182

`private static uint? UInt(ManagementBaseObject? obj, string key)`

Converts a WMI property to nullable UInt32 without propagating conversion/provider errors.

### `UInt64AsUInt` — line ~191

`private static uint? UInt64AsUInt(ManagementBaseObject? obj, string key)`

Converts an unsigned 64-bit WMI value only when it fits safely into UInt32.

### `Int` — line ~205

`private static int? Int(ManagementBaseObject? obj, string key)`

Converts a WMI property to nullable Int32 without propagating conversion/provider errors.

### `Bool` — line ~214

`private static bool? Bool(ManagementBaseObject? obj, string key)`

Converts a WMI property to nullable Boolean without propagating conversion/provider errors.

### `NonZero` — line ~223

`private static uint? NonZero(uint? value) => value is > 0 ? value : null;`

Normalizes zero-valued firmware fields to null because zero commonly means unavailable.

### `NormalizeRuntime` — line ~228

`private static int? NormalizeRuntime(int? minutes)`

Rejects invalid/sentinel firmware runtime values and returns plausible minutes only.

## `ViewModels/MainViewModel.cs`

**Responsibility:** Primary presentation/application coordinator. Reads battery telemetry, derives diagnostics, controls tests, persists history, exports reports, and exposes localized UI state.

### `MainViewModel` — line ~97

`public MainViewModel()`

Constructs services, commands and test profiles, loads persisted preferences, and prepares bindable state for the main window.

### `InitializeAsync` — line ~507

`public async Task InitializeAsync()`

Performs the first telemetry refresh, reloads history, attempts interrupted-session recovery, and builds the initial UI/diagnostic state.

### `MonitorTickAsync` — line ~517

`public async Task MonitorTickAsync()`

Executes one scheduled background-monitor cycle; the timer is owned by MainWindow.

### `RefreshAsync` — line ~526

`private Task RefreshAsync() => RefreshCoreAsync();`

Command-friendly wrapper around the core refresh pipeline.

### `RefreshCoreAsync` — line ~531

`private async Task RefreshCoreAsync()`

Reads a fresh BatterySnapshot, updates live/test/history state, recovers diagnostics, persists periodic history, and evaluates background alerts.

### `ShouldPersistHistory` — line ~610

`private bool ShouldPersistHistory(DateTimeOffset now)`

Applies the history write cadence so the database is useful without receiving a row on every 2-5 second poll.

### `ReloadHistoryAsync` — line ~616

`private async Task ReloadHistoryAsync()`

Reloads recent/trend history from SQLite and rebuilds the chart collections and degradation estimates.

### `AddLiveSample` — line ~638

`private void AddLiveSample(BatterySnapshot snapshot)`

Adds one snapshot to the bounded in-memory live window used for moving averages, runtime estimation, and anomaly detection.

### `ToggleTest` — line ~668

`private void ToggleTest()`

Starts the selected discharge/collapse test when idle or stops the current test when already running.

### `StopTest` — line ~712

`private void StopTest(string reason, string statusKey)`

Finalizes a running test, closes its crash-safe journal, computes the final summary, and updates user-visible status.

### `UpdateBatteryTest` — line ~726

`private void UpdateBatteryTest(BatterySnapshot snapshot)`

Updates test elapsed state on each telemetry poll and automatically stops timed profiles when their target duration is reached.

### `AddTestSample` — line ~755

`private void AddTestSample(BatterySnapshot snapshot)`

Converts a BatterySnapshot into the compact test sample format and appends it to both memory and the crash-safe journal.

### `UpdateTestSummary` — line ~783

`private void UpdateTestSummary()`

Re-runs BatteryTestAnalyzer against all test samples so provisional metrics remain live during a test.

### `CanExportTestReport` — line ~797

`private bool CanExportTestReport()`

Returns whether a completed test summary and samples exist for export.

### `ExportTestReportAsync` — line ~803

`private async Task ExportTestReportAsync()`

Builds localized report text, exports HTML/JSON, and updates the UI with the saved location/error.

### `CanExportRecoveredReport` — line ~844

`private bool CanExportRecoveredReport()`

Returns whether a recovered interrupted-session report is available for export.

### `ExportRecoveredReportAsync` — line ~850

`private async Task ExportRecoveredReportAsync()`

Exports recovered sudden-collapse evidence and all persisted samples to privacy-safe HTML/JSON.

### `ShutdownReliabilityScore` — line ~955

`private int ShutdownReliabilityScore()`

Converts recovered collapse/gauge-jump evidence into a conservative 0-100 shutdown-reliability score.

### `ReliabilityCode` — line ~965

`private static string ReliabilityCode(int score) => score switch`

Maps a numeric reliability score to good/fair/poor/critical presentation bands.

### `RebuildFindings` — line ~976

`private void RebuildFindings()`

Rebuilds capacity/health findings shown on the dashboard from the latest snapshot and smoothed health value.

### `RebuildLiveAnomalies` — line ~993

`private void RebuildLiveAnomalies()`

Runs anomaly detection against the bounded live telemetry window and refreshes the live warning list.

### `RebuildTestAnomalies` — line ~1009

`private void RebuildTestAnomalies()`

Converts the current test summary anomalies into localized UI findings.

### `ToDisplayFinding` — line ~1025

`private DisplayFinding ToDisplayFinding(AnomalyFinding finding)`

Maps an internal anomaly code/value into localized title/detail strings suitable for the UI.

### `BuildVerdictDetail` — line ~1037

`private string BuildVerdictDetail(BatteryTestSummary summary)`

Builds the localized test verdict explanation and inserts the current numeric evidence into placeholders.

### `RebuildTestProfiles` — line ~1048

`private void RebuildTestProfiles(string preferredCode)`

Recreates localized test-profile options while preserving the previously selected profile code where possible.

### `RaiseRecoveryProperties` — line ~1065

`private void RaiseRecoveryProperties()`

Raises all dependent bindings that change when recovered-collapse evidence is loaded or recalculated.

### `EvaluateBackgroundAlerts` — line ~1085

`private void EvaluateBackgroundAlerts(BatterySnapshot snapshot)`

Evaluates current telemetry against critical/gauge/sag rules and emits a throttled background warning only when user attention is warranted.

### `RaiseBackgroundAlert` — line ~1145

`private void RaiseBackgroundAlert(string severity, string title, string detail, DateTimeOffset capturedAt)`

Applies alert cooldown/deduplication and raises the event consumed by the system-tray shell.

### `ExportDiagnosticSnapshotAsync` — line ~1162

`private async Task ExportDiagnosticSnapshotAsync()`

Creates the privacy-safe support snapshot and reports the output path/status to the UI.

### `PersistSettings` — line ~1185

`private void PersistSettings()`

Writes current tray/background preferences to the portable settings file.

### `OpenFolder` — line ~1200

`private static void OpenFolder(string path)`

Opens a local folder in Windows Explorer without passing it through a shell command string.

### `OpenUrl` — line ~1216

`private static void OpenUrl(string? url)`

Opens an explicitly configured http/https project or donation URL in the default browser.

### `FormatHealthChange` — line ~1232

`private string FormatHealthChange(int days)`

Calculates and formats the change in historical health over a requested number of days when sufficient history exists.

### `FormatPowerAverage` — line ~1253

`private string FormatPowerAverage(TimeSpan window)`

Formats the moving-average discharge power for a requested live window.

### `AverageDischargeW` — line ~1262

`private double? AverageDischargeW(TimeSpan window)`

Calculates mean positive discharge watts from valid samples in the requested time window.

### `GetDischargePowerSamples` — line ~1271

`private List<double> GetDischargePowerSamples(TimeSpan window)`

Returns valid discharge-power samples within a recent time window, excluding charging/invalid values.

### `CalculateRuntimeMinutes` — line ~1285

`private int? CalculateRuntimeMinutes(TimeSpan window)`

Estimates runtime from a moving-average discharge window and current remaining capacity.

### `CalculateInstantRuntimeMinutes` — line ~1291

`private int? CalculateInstantRuntimeMinutes()`

Estimates runtime from the latest instantaneous discharge-rate reading.

### `CalculateRuntimeFromPower` — line ~1301

`private int? CalculateRuntimeFromPower(double? powerW)`

Converts remaining watt-hours and discharge watts into bounded runtime minutes.

### `CalculateBestRuntimeMinutes` — line ~1317

`private int? CalculateBestRuntimeMinutes()`

Chooses the most stable available runtime estimate, preferring longer moving averages over instantaneous data.

### `GetRuntimeSource` — line ~1335

`private string GetRuntimeSource()`

Returns the localization key/text identifying whether runtime came from 5-minute, 1-minute, instantaneous, or firmware data.

### `GetSmoothedHealthPercent` — line ~1348

`private double? GetSmoothedHealthPercent()`

Uses the median of recent valid firmware-health samples to reduce UI flicker from small FullChargeCapacity oscillations.

### `CountHealthSamples` — line ~1368

`private int CountHealthSamples()`

Counts valid recent health samples contributing to smoothing/confidence display.

### `GetSignedPowerW` — line ~1374

`private static double? GetSignedPowerW(BatterySnapshot snapshot)`

Normalizes charge power as positive and discharge power as negative watts for charts and history.

### `FormatWh` — line ~1386

`private static string FormatWh(uint? mwh) => mwh is { } v ? $"{v / 1000.0:0.0} Wh" : "—";`

Formats milliwatt-hours as human-readable watt-hours.

### `FormatPercent` — line ~1390

`private static string FormatPercent(int? value) => value is { } v ? $"{Math.Clamp(v, 0, 100)}%" : "—";`

Clamps raw firmware charge to 0-100 for presentation while raw telemetry can still be preserved elsewhere for diagnostics.

### `FormatMinutes` — line ~1395

`private static string FormatMinutes(int? minutes)`

Formats nullable runtime minutes as a concise hour/minute string.

### `FormatDuration` — line ~1401

`private static string FormatDuration(TimeSpan span)`

Formats a TimeSpan for test-duration display.

### `RaiseTestProperties` — line ~1407

`private void RaiseTestProperties()`

Raises all bindings derived from the current test/test-summary in one place after test state changes.

### `RaiseFormattedProperties` — line ~1429

`private void RaiseFormattedProperties()`

Raises all formatted/dashboard bindings after a new telemetry snapshot or localization change.

### `OnPropertyChanged` — line ~1460

`private void OnPropertyChanged([CallerMemberName] string? name = null)`

Standard INotifyPropertyChanged helper used by WPF data binding.

### `AsyncCommand` — line ~1476

`public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)`

Wraps an asynchronous action as ICommand and prevents overlapping executions.

### `CanExecute` — line ~1486

`public bool CanExecute(object? parameter) => !_executing && (_canExecute?.Invoke() ?? true);`

Returns whether a command is currently allowed to execute.

### `Execute` — line ~1491

`public async void Execute(object? parameter)`

Runs the configured command action and updates command enabled state around execution.

### `RaiseCanExecuteChanged` — line ~1503

`public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);`

Notifies WPF that a command enabled/disabled state should be re-evaluated.

### `RelayCommand` — line ~1517

`public RelayCommand(Action execute, Func<bool>? canExecute = null)`

Wraps a synchronous Action as ICommand with an optional CanExecute predicate.

### `CanExecute` — line ~1527

`public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;`

Returns whether a command is currently allowed to execute.

### `Execute` — line ~1531

`public void Execute(object? parameter) => _execute();`

Runs the configured command action and updates command enabled state around execution.

### `RaiseCanExecuteChanged` — line ~1535

`public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);`

Notifies WPF that a command enabled/disabled state should be re-evaluated.
