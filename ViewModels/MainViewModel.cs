using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BatteryDoctor.Models;
using BatteryDoctor.Services;

// File responsibility: Primary presentation/application coordinator. Reads battery telemetry, derives diagnostics, controls tests, persists history, exports reports, and exposes localized UI state.

namespace BatteryDoctor.ViewModels;

/// <summary>
/// Bindable application state and workflow coordinator used by MainWindow.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan HistoryInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(5);

    private readonly IBatteryProvider _batteryProvider = new WindowsBatteryProvider();
    private readonly BatteryHealthAnalyzer _analyzer = new();
    private readonly BatteryAnomalyDetector _anomalyDetector = new();
    private readonly BatteryTestAnalyzer _testAnalyzer = new();
    private readonly VoltageSagAnalyzer _voltageSagAnalyzer = new();
    private readonly ElectricalDiagnosticsAnalyzer _electricalAnalyzer = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly BatteryReportExporter _reportExporter = new();
    private readonly RecoveredCollapseReportExporter _collapseReportExporter = new();
    private readonly BatteryTestJournal _testJournal = new();
    private readonly DiagnosticSnapshotExporter _diagnosticExporter = new();
    private readonly AppSettings _settings;
    private readonly AppLinks _appLinks;
    private readonly List<BatterySnapshot> _liveSamples = new();
    private readonly List<BatterySnapshot> _testRawSamples = new();
    private readonly List<BatteryTestSample> _testSamples = new();
    private IReadOnlyList<HistorySamplePoint> _historyTrend = Array.Empty<HistorySamplePoint>();

    private HistoryRepository? _history;
    private DateTimeOffset? _lastHistoryPersistAt;
    private bool _initialized;
    private BatterySnapshot? _snapshot;
    private HealthAssessment? _assessment;
    private ElectricalDiagnosticsAssessment? _electricalAssessment;
    private BatteryTestSummary? _testSummary;
    private bool _isBusy;
    private string? _error;
    private LanguageOption? _selectedLanguage;
    private bool _isTestRunning;
    private string _testStatusKey = "TestReady";
    private string? _testExportStatus;
    private string? _recoveredExportStatus;
    private TestProfileOption? _selectedTestProfile;
    private RecoveredTestInterruption? _recoveredInterruption;
    private bool _recoveryChecked;
    private DateTimeOffset _lastSagNotificationAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGaugeNotificationAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastCriticalNotificationAt = DateTimeOffset.MinValue;
    private bool _criticalRecoveryNotificationSent;
    private string? _lastBackgroundAlertTitle;
    private string? _lastBackgroundAlertDetail;
    private string? _diagnosticExportStatus;
    private DateTimeOffset? _lastBackgroundAlertAt;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<BackgroundAlertEventArgs>? BackgroundAlertRaised;

    public LocalizationService Loc { get; } = new();
    public ObservableCollection<DisplayFinding> Findings { get; } = new();
    public ObservableCollection<HistoryPoint> History { get; } = new();
    public ObservableCollection<TrendPoint> LivePowerTrend { get; } = new();
    public ObservableCollection<TrendPoint> LiveTemperatureTrend { get; } = new();
    public ObservableCollection<TrendPoint> LiveCurrentTrend { get; } = new();
    public ObservableCollection<TrendPoint> HealthTrend { get; } = new();
    public ObservableCollection<TrendPoint> FullCapacityTrend { get; } = new();
    public ObservableCollection<TrendPoint> TestPowerTrend { get; } = new();
    public ObservableCollection<DisplayFinding> LiveAnomalies { get; } = new();
    public ObservableCollection<DisplayFinding> TestAnomalies { get; } = new();
    public ObservableCollection<TestProfileOption> TestProfiles { get; } = new();

    public IReadOnlyList<LanguageOption> Languages => Loc.Languages;
    public ICommand RefreshCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand ExportTestCommand { get; }
    public ICommand ExportRecoveredCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenSupportCommand { get; }
    public ICommand TestNotificationCommand { get; }
    public ICommand ExportDiagnosticCommand { get; }

    /// <summary>
    /// Constructs services, commands and test profiles, loads persisted preferences, and prepares bindable state for the main window.
    /// </summary>
    public MainViewModel()
    {
        _settings = _settingsService.Load();
        _appLinks = new AppLinksService().Load();
        if (_settings.StartWithWindows)
            StartupManager.TrySetEnabled(true, out _);

        _selectedLanguage = Languages.FirstOrDefault(x => x.Code == Loc.CurrentLanguage) ?? Languages[0];
        RebuildTestProfiles("standard");
        RefreshCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        TestCommand = new RelayCommand(ToggleTest, () => _snapshot is not null);
        ExportTestCommand = new AsyncCommand(ExportTestReportAsync, CanExportTestReport);
        ExportRecoveredCommand = new AsyncCommand(ExportRecoveredReportAsync, CanExportRecoveredReport);
        OpenDataFolderCommand = new RelayCommand(() => OpenFolder(DataFolder));
        OpenReportsFolderCommand = new RelayCommand(() => OpenFolder(ReportsFolder));
        OpenProjectCommand = new RelayCommand(() => OpenUrl(_appLinks.ProjectUrl), () => ProjectLinkConfigured);
        OpenSupportCommand = new RelayCommand(() => OpenUrl(_appLinks.SupportUrl), () => SupportLinkConfigured);
        TestNotificationCommand = new RelayCommand(() => RaiseBackgroundAlert("warn", Loc["TestNotificationTitle"], Loc["TestNotificationDetail"], DateTimeOffset.Now));
        ExportDiagnosticCommand = new AsyncCommand(ExportDiagnosticSnapshotAsync);
        RebuildLiveAnomalies();
        RebuildTestAnomalies();
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value) return;
            _settings.MinimizeToTray = value;
            PersistSettings();
            OnPropertyChanged();
        }
    }

    public bool BackgroundMonitoring
    {
        get => _settings.BackgroundMonitoring;
        set
        {
            if (_settings.BackgroundMonitoring == value) return;
            _settings.BackgroundMonitoring = value;
            PersistSettings();
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundMonitoringStatus));
        }
    }

    public bool TrayNotifications
    {
        get => _settings.TrayNotifications;
        set
        {
            if (_settings.TrayNotifications == value) return;
            _settings.TrayNotifications = value;
            PersistSettings();
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows == value) return;
            if (!StartupManager.TrySetEnabled(value, out var error))
            {
                Error = $"{Loc["StartupSettingFailed"]}: {error}";
                OnPropertyChanged();
                return;
            }
            _settings.StartWithWindows = value;
            PersistSettings();
            OnPropertyChanged();
        }
    }

    public string BackgroundMonitoringStatus => BackgroundMonitoring ? Loc["BackgroundMonitoringOn"] : Loc["BackgroundMonitoringOff"];
    public bool HasBackgroundAlert => !string.IsNullOrWhiteSpace(_lastBackgroundAlertTitle);
    public string LastBackgroundAlertTitle => _lastBackgroundAlertTitle ?? Loc["NoBackgroundAlertTitle"];
    public string LastBackgroundAlertDetail => _lastBackgroundAlertDetail ?? Loc["NoBackgroundAlertDetail"];
    public string LastBackgroundAlertTime => _lastBackgroundAlertAt?.LocalDateTime.ToString("g") ?? "—";
    public string AppVersion => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.9.0";
    public string ChemistryDisplay => _snapshot?.Chemistry ?? Loc["NotSupported"];
    public string DataFolder => PortablePaths.RootDirectory;
    public string ReportsFolder => PortablePaths.ReportsDirectory;
    public string SettingsPath => _settingsService.SettingsPath;
    public bool ProjectLinkConfigured => !string.IsNullOrWhiteSpace(_appLinks.ProjectUrl);
    public bool SupportLinkConfigured => !string.IsNullOrWhiteSpace(_appLinks.SupportUrl);
    public string ProjectLinkStatus => ProjectLinkConfigured ? Loc["LinkReady"] : Loc["LinkNotConfigured"];
    public string SupportLinkStatus => SupportLinkConfigured ? Loc["LinkReady"] : Loc["SupportLinkNotConfigured"];
    public string DiagnosticExportStatus => _diagnosticExportStatus ?? Loc["DiagnosticExportNotYet"];

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || Equals(_selectedLanguage, value)) return;
            _selectedLanguage = value;
            Loc.CurrentLanguage = value.Code;
            var profileCode = _selectedTestProfile?.Code ?? "standard";
            RebuildTestProfiles(profileCode);
            RebuildFindings();
            RebuildLiveAnomalies();
            RebuildTestAnomalies();
            RaiseRecoveryProperties();
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundMonitoringStatus));
            OnPropertyChanged(nameof(LastBackgroundAlertTitle));
            OnPropertyChanged(nameof(LastBackgroundAlertDetail));
            OnPropertyChanged(nameof(ProjectLinkStatus));
            OnPropertyChanged(nameof(SupportLinkStatus));
            RaiseFormattedProperties();
        }
    }

    public TestProfileOption? SelectedTestProfile
    {
        get => _selectedTestProfile;
        set
        {
            if (value is null || Equals(_selectedTestProfile, value)) return;
            _selectedTestProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TestProfileHint));
            OnPropertyChanged(nameof(TestTarget));
        }
    }

    public bool IsTestProfileEnabled => !IsTestRunning;
    public string TestProfileHint => _selectedTestProfile is null ? "—" : Loc[$"TestProfile_{_selectedTestProfile.Code}_Hint"];
    public string TestTarget => _selectedTestProfile is null
        ? "—"
        : _selectedTestProfile.CollapseWatch
            ? Loc["UntilStoppedOrPowerLoss"]
            : $"{_selectedTestProfile.TargetMinutes} min";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            ((AsyncCommand)RefreshCommand).RaiseCanExecuteChanged();
        }
    }

    public string? Error
    {
        get => _error;
        private set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string HealthScore => _assessment?.Score is { } score ? $"{score:0.0}%" : "—";
    public string HealthEstimateHint => CountHealthSamples() >= 3 ? Loc["HealthSmoothed"] : Loc["HealthCurrentReading"];
    public string CapacitySourceNote => _recoveredInterruption?.FirmwareRecalibrationDetected == true
        ? Loc["HealthFirmwareReestimated"]
        : HealthEstimateHint;
    public string Condition => _assessment is null ? "—" : Loc[_assessment.ConditionKey];
    public string Summary => _assessment is null ? Loc["Loading"] : Loc[_assessment.SummaryKey];
    public string DesignCapacity => FormatWh(_snapshot?.DesignCapacityMWh);
    public string FullChargeCapacity => FormatWh(_snapshot?.FullChargeCapacityMWh);
    public string Wear => _assessment?.Score is { } health ? $"{Math.Clamp(100 - health, 0, 100):0.0}%" : "—";
    public string CycleCount => _snapshot?.CycleCount?.ToString("N0") ?? Loc["NotSupported"];
    public string Voltage => _snapshot?.VoltageMV is { } mv ? $"{mv / 1000.0:0.00} V" : "—";
    public string BatteryTemperature => _snapshot?.TemperatureC is { } c ? $"{c:0.0} °C" : Loc["NotSupported"];
    public string BatteryTemperatureSource => _snapshot?.TemperatureC is null
        ? Loc["TemperatureUnavailableHint"]
        : $"{Loc["SensorSource"]}: {_snapshot.TemperatureSource ?? Loc["Unknown"]}";
    public string EstimatedCurrent
    {
        get
        {
            if (_electricalAssessment?.EstimatedCurrentA is not { } current) return "—";
            return current >= 0 ? $"↑ {current:0.00} A" : $"↓ {Math.Abs(current):0.00} A";
        }
    }
    public string DynamicResistance => _electricalAssessment?.DynamicResistanceOhm is { } ohm
        ? $"~{ohm:0.000} Ω"
        : "—";
    public string DynamicResistanceConfidence => _electricalAssessment is null
        ? "—"
        : _electricalAssessment.DynamicResistanceSampleCount > 0
            ? $"{_electricalAssessment.DynamicResistanceSampleCount} {Loc["LoadSteps"]}"
            : Loc["NeedLoadVariation"];
    public string TemperatureChange5Min => _electricalAssessment?.TemperatureDelta5MinC is { } delta
        ? $"{delta:+0.0;-0.0;0.0} °C"
        : "—";
    public string TemperatureRate => _electricalAssessment?.TemperatureRateCPerMinute is { } rate
        ? $"{rate:+0.00;-0.00;0.00} °C/min"
        : "—";
    public string PowerTemperatureCorrelation => _electricalAssessment?.PowerTemperatureCorrelation is { } correlation
        ? $"{correlation:+0.00;-0.00;0.00}"
        : "—";
    public string TemperatureSampleCount => _electricalAssessment?.ValidTemperatureSampleCount.ToString("N0") ?? "0";
    public string CurrentCharge => _snapshot?.EstimatedChargePercent is { } p ? $"{Math.Clamp(p, 0, 100)}%" : "—";
    public string Runtime => FormatMinutes(CalculateBestRuntimeMinutes());
    public string RuntimeSource => GetRuntimeSource();
    public string BatteryName => _snapshot?.Name ?? Loc["UnknownBattery"];
    public string LastUpdated => _snapshot?.CapturedAt.LocalDateTime.ToString("g") ?? "—";
    public string LastUpdatedDisplay => $"{Loc["LastUpdated"]}: {LastUpdated}";
    public string DatabasePath => _history?.DatabasePath ?? "—";
    public string LiveSampleCount => _liveSamples.Count.ToString("N0");
    public string LiveValidPowerSampleCount => _liveSamples.Count(x => GetSignedPowerW(x) is not null).ToString("N0");
    public string HealthChange30Days => FormatHealthChange(30);
    public string HealthChange90Days => FormatHealthChange(90);
    public string HistorySpan => _historyTrend.Count >= 2
        ? $"{Math.Max(0, (_historyTrend[^1].CapturedAt - _historyTrend[0].CapturedAt).TotalDays):0} d"
        : Loc["NotEnoughHistory"];

    public string PowerState
    {
        get
        {
            if (_snapshot?.Charging == true) return Loc["Charging"];
            if (_snapshot?.Discharging == true) return Loc["Discharging"];
            if (_snapshot?.PowerOnline == true) return Loc["PluggedIn"];
            return Loc["Unknown"];
        }
    }

    public string PowerRate
    {
        get
        {
            if (_snapshot?.Discharging == true && _snapshot.DischargeRateMW is { } d && d != 0)
                return $"↓ {Math.Abs(d) / 1000.0:0.0} W";
            if (_snapshot?.Charging == true && _snapshot.ChargeRateMW is { } c && c != 0)
                return $"↑ {Math.Abs(c) / 1000.0:0.0} W";
            if (_snapshot?.DischargeRateMW is { } anyD && anyD != 0)
                return $"↓ {Math.Abs(anyD) / 1000.0:0.0} W";
            if (_snapshot?.ChargeRateMW is { } anyC && anyC != 0)
                return $"↑ {Math.Abs(anyC) / 1000.0:0.0} W";
            return "—";
        }
    }

    public string PowerDataStatus
    {
        get
        {
            if (_snapshot is null) return Loc["Loading"];
            if (_snapshot.PowerOnline == true && _snapshot.Discharging != true && _snapshot.Charging != true)
                return Loc["PowerDataPluggedIn"];
            if (_snapshot.Discharging == true && _snapshot.DischargeRateMW is null)
                return Loc["PowerRateUnavailable"];
            if (_snapshot.Charging == true && _snapshot.ChargeRateMW is null)
                return Loc["ChargeRateUnavailable"];
            return Loc["PowerDataAvailable"];
        }
    }

    public string Average1Minute => FormatPowerAverage(TimeSpan.FromMinutes(1));
    public string Average5Minutes => FormatPowerAverage(TimeSpan.FromMinutes(5));
    public string Runtime1Minute => FormatMinutes(CalculateRuntimeMinutes(TimeSpan.FromMinutes(1)));
    public string Runtime5Minutes => FormatMinutes(CalculateRuntimeMinutes(TimeSpan.FromMinutes(5)));

    public bool IsTestRunning
    {
        get => _isTestRunning;
        private set
        {
            _isTestRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TestButtonText));
            OnPropertyChanged(nameof(IsTestProfileEnabled));
            ((AsyncCommand)ExportTestCommand).RaiseCanExecuteChanged();
        }
    }

    public string TestButtonText => IsTestRunning ? Loc["StopTest"] : Loc["StartTest"];
    public string TestStatus => Loc[_testStatusKey];
    public string TestDuration => _testSummary is not null
        ? FormatDuration(_testSummary.Duration)
        : _testSamples.Count >= 2 ? FormatDuration(_testSamples[^1].CapturedAt - _testSamples[0].CapturedAt) : "00:00";
    public string TestChargeDrop => _testSummary?.ChargeDropPercent is { } drop ? $"{drop}%" : "—";
    public string TestAveragePower => _testSummary?.AveragePowerW is { } power ? $"{power:0.0} W" : "—";
    public string TestVoltageRange => _testSummary?.MinVoltageV is { } min && _testSummary.MaxVoltageV is { } max
        ? $"{min:0.00}–{max:0.00} V"
        : "—";
    public string TestSampleCount => _testSamples.Count.ToString("N0");
    public string TestValidPowerSamples => _testSummary?.ValidPowerSampleCount.ToString("N0") ?? "0";
    public string TestChargeStartEnd => _testSummary is null
        ? "—"
        : $"{FormatPercent(_testSummary.StartChargePercent)} → {FormatPercent(_testSummary.EndChargePercent)}";
    public string TestEnergyUsed => _testSummary?.EstimatedEnergyUsedWh is { } energy ? $"{energy:0.00} Wh" : "—";
    public string TestAverageVoltage => _testSummary?.AverageVoltageV is { } voltage ? $"{voltage:0.00} V" : "—";
    public string TestCapacityDelta => _testSummary?.CapacityDeltaWh is { } delta ? $"{delta:0.00} Wh" : "—";
    public string TestCapacityDeltaNote => _testSummary?.CapacityDeltaWh is { } ? $"{Loc["CapacityDelta"]}: {TestCapacityDelta}" : Loc["CapacityDeltaUnavailable"];
    public string TestVerdict => _testSummary is null ? Loc["TestVerdictPending"] : Loc[$"TestVerdict_{_testSummary.VerdictCode}_Title"];
    public string TestVerdictDetail => _testSummary is null ? Loc["TestVerdictPendingDetail"] : BuildVerdictDetail(_testSummary);
    public string TestVerdictMode => IsTestRunning ? Loc["TestVerdictProvisional"] : Loc["TestVerdictFinal"];
    public string TestExportStatus => _testExportStatus ?? Loc["ExportNotYet"];
    public string TestConfidence => _testSummary is null ? "—" : Loc[$"TestConfidence_{_testSummary.ConfidenceCode}"];
    public string TestEnergyAgreement => _testSummary?.EnergyAgreementPercent is { } agreement ? $"{agreement:0.0}%" : "—";
    public string TestDataConsistency => _testSummary is null ? "—" : Loc[$"DataConsistency_{_testSummary.DataConsistencyCode}"];
    public string TestEstimatedUsableCapacity => _testSummary?.ExtrapolatedUsableCapacityWh is { } wh ? $"{wh:0.0} Wh" : "—";
    public string TestEstimatedUsableHealth => _testSummary?.ExtrapolatedUsableHealthPercent is { } health ? $"{health:0.0}%" : "—";
    public string TestVoltageSagScore => _testSummary is null
        ? "—"
        : $"{_testSummary.VoltageSagSeverityScore}/100 · {Loc["SagSeverity_" + _testSummary.VoltageSagSeverityCode]}";
    public string TestVoltageSagEvents => _testSummary?.VoltageSagEventCount.ToString("N0") ?? "—";
    public string TestMaxVoltageSag => _testSummary?.MaxVoltageSagV is { } sag ? $"{sag:0.00} V" : "—";
    public string TestTemperatureRange => _testSummary?.MinTemperatureC is { } minT && _testSummary.MaxTemperatureC is { } maxT
        ? $"{minT:0.0}–{maxT:0.0} °C"
        : "—";
    public string TestAverageTemperature => _testSummary?.AverageTemperatureC is { } temp ? $"{temp:0.0} °C" : "—";
    public string TestTemperatureRise => _testSummary?.TemperatureRiseC is { } rise ? $"{rise:+0.0;-0.0;0.0} °C" : "—";
    public string TestAverageCurrent => _testSummary?.AverageEstimatedCurrentA is { } current ? $"~{current:0.00} A" : "—";
    public string TestDynamicResistance => _testSummary?.DynamicResistanceOhm is { } resistance ? $"~{resistance:0.000} Ω" : "—";
    public string TestPowerTemperatureCorrelation => _testSummary?.PowerTemperatureCorrelation is { } correlation
        ? $"{correlation:+0.00;-0.00;0.00}"
        : "—";

    public bool HasRecoveredInterruption => _recoveredInterruption?.SystemRestartDetected == true;
    public string RecoveredInterruptionTitle => _recoveredInterruption?.StrongGaugeJumpDetected == true
        ? Loc["RecoveredCollapseHighTitle"]
        : Loc["RecoveredCollapsePossibleTitle"];
    public string RecoveredInterruptionDetail
    {
        get
        {
            if (_recoveredInterruption is null) return "";
            var key = _recoveredInterruption.StrongGaugeJumpDetected
                ? "RecoveredCollapseHighDetail"
                : "RecoveredCollapsePossibleDetail";
            return Loc[key]
                .Replace("{last}", FormatPercent(_recoveredInterruption.LastReportedChargePercent))
                .Replace("{current}", FormatPercent(_recoveredInterruption.CurrentChargePercent))
                .Replace("{energy}", _recoveredInterruption.EnergyRecordedWh?.ToString("0.00") ?? "—")
                .Replace("{observed}", _recoveredInterruption.ObservedEnergyVsDesignPercent?.ToString("0.0") ?? "—")
                .Replace("{remaining}", _recoveredInterruption.LastRemainingWh?.ToString("0.00") ?? "—");
        }
    }
    public string RecoveredLastCharge => FormatPercent(_recoveredInterruption?.LastReportedChargePercent);
    public string RecoveredCurrentCharge => FormatPercent(_recoveredInterruption?.CurrentChargePercent);
    public string RecoveredEnergy => _recoveredInterruption?.EnergyRecordedWh is { } energy ? $"{energy:0.00} Wh" : "—";
    public string RecoveredObservedEnergyVsDesign => _recoveredInterruption?.ObservedEnergyVsDesignPercent is { } value ? $"{value:0.0}%" : "—";
    public string RecoveredObservedVsReportedAvailable => _recoveredInterruption?.ObservedEnergyVsReportedAvailablePercent is { } value ? $"{value:0.0}%" : "—";
    public string RecoveredLastRemaining => _recoveredInterruption?.LastRemainingWh is { } value ? $"{value:0.00} Wh" : "—";
    public string RecoveredLastVoltage => _recoveredInterruption?.LastVoltageV is { } value ? $"{value:0.00} V" : "—";
    public string RecoveredVoltageSagScore => _recoveredInterruption is null
        ? "—"
        : $"{_recoveredInterruption.VoltageSagSeverityScore}/100 · {Loc["SagSeverity_" + _recoveredInterruption.VoltageSagSeverityCode]}";
    public string RecoveredMaxVoltageSag => _recoveredInterruption?.MaxVoltageSagV is { } value ? $"{value:0.00} V" : "—";
    public string RecoveredSagEvents => _recoveredInterruption?.VoltageSagEventCount.ToString("N0") ?? "—";
    public string RecoveredGaugeReliability => _recoveredInterruption is null
        ? "—"
        : $"{_recoveredInterruption.GaugeReliabilityScore}/100 · {Loc["Reliability_" + _recoveredInterruption.GaugeReliabilityCode]}";
    public string RecoveredBatteryReliability => _recoveredInterruption is null
        ? "—"
        : $"{_recoveredInterruption.BatteryReliabilityScore}/100 · {Loc["Reliability_" + _recoveredInterruption.BatteryReliabilityCode]}";
    public string RecoveredFirmwareChange
    {
        get
        {
            if (_recoveredInterruption is null) return "—";
            if (_recoveredInterruption.ReportedFullChargeCapacityWh is not { } before ||
                _recoveredInterruption.FullChargeCapacityAfterRestartWh is not { } after)
                return "—";
            var delta = _recoveredInterruption.FullChargeCapacityChangePercent;
            var suffix = delta is { } d ? $" ({d:+0.0;-0.0;0.0}%)" : "";
            return $"{before:0.0} → {after:0.0} Wh{suffix}";
        }
    }
    public string RecoveredFirmwareEvent => _recoveredInterruption?.FirmwareRecalibrationDetected == true
        ? Loc["FirmwareRecalibrationDetected"]
        : Loc["FirmwareRecalibrationNotDetected"];
    public string RecoveredConfidence => _recoveredInterruption is null ? "—" : Loc[$"RecoveryConfidence_{_recoveredInterruption.ConfidenceCode}"];
    public string RecoveredJournalPath => _recoveredInterruption?.SamplePath ?? "—";
    public string RecoveredExportStatus => _recoveredExportStatus ?? Loc["CollapseExportNotYet"];

    public string DiagnosticCapacity => _assessment?.Score is { } score
        ? $"{score:0.0}% · {Condition}"
        : "—";
    public string DiagnosticGauge => RecoveredGaugeReliability;
    public string DiagnosticVoltage
    {
        get
        {
            if (_recoveredInterruption is null) return "—";
            var score = Math.Clamp(100 - _recoveredInterruption.VoltageSagSeverityScore, 0, 100);
            return $"{score}/100 · {Loc["Reliability_" + ReliabilityCode(score)]}";
        }
    }
    public string DiagnosticShutdown
    {
        get
        {
            if (_recoveredInterruption is null) return "—";
            var score = ShutdownReliabilityScore();
            return $"{score}/100 · {Loc["Reliability_" + ReliabilityCode(score)]}";
        }
    }
    public string DiagnosticOverall => RecoveredBatteryReliability;
    public string PercentageTrustStatus => _recoveredInterruption?.StrongGaugeJumpDetected == true
        ? Loc["PercentageUnreliable"]
        : _recoveredInterruption?.PossibleSuddenCollapse == true
            ? Loc["PercentageQuestionable"]
            : Loc["PercentageNoIssueDetected"];
    public string RecommendationTitle => GetRecommendation().Title;
    public string RecommendationDetail => GetRecommendation().Detail;

    /// <summary>
    /// Performs the first telemetry refresh, reloads history, attempts interrupted-session recovery, and builds the initial UI/diagnostic state.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await RefreshCoreAsync();
    }

    /// <summary>
    /// Executes one scheduled background-monitor cycle; the timer is owned by MainWindow.
    /// </summary>
    public async Task MonitorTickAsync()
    {
        if (!_initialized || IsBusy) return;
        await RefreshCoreAsync();
    }

    /// <summary>
    /// Command-friendly wrapper around the core refresh pipeline.
    /// </summary>
    private Task RefreshAsync() => RefreshCoreAsync();

    /// <summary>
    /// Reads a fresh BatterySnapshot, updates live/test/history state, recovers diagnostics, persists periodic history, and evaluates background alerts.
    /// </summary>
    private async Task RefreshCoreAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        string? nonFatalWarning = null;

        try
        {
            if (_history is null)
            {
                try
                {
                    _history = new HistoryRepository();
                    OnPropertyChanged(nameof(DatabasePath));
                }
                catch (Exception ex)
                {
                    nonFatalWarning = $"History database is unavailable: {ex.Message}";
                }
            }

            var snapshot = await _batteryProvider.ReadAsync();
            _snapshot = snapshot;

            if (!_recoveryChecked)
            {
                _recoveryChecked = true;
                try
                {
                    _recoveredInterruption = _testJournal.RecoverInterruptedSession(snapshot)
                        ?? _testJournal.LoadLatestRecoveredSession(snapshot);
                    RaiseRecoveryProperties();
                }
                catch (Exception ex)
                {
                    nonFatalWarning = $"Previous battery-test recovery could not be read: {ex.Message}";
                }
            }

            AddLiveSample(snapshot);
            EvaluateBackgroundAlerts(snapshot);
            var smoothedHealth = GetSmoothedHealthPercent();
            _assessment = _analyzer.Analyze(snapshot, smoothedHealth);
            UpdateBatteryTest(snapshot);
            RebuildFindings();
            RebuildLiveAnomalies();

            if (_history is not null && ShouldPersistHistory(snapshot.CapturedAt))
            {
                try
                {
                    await _history.AddAsync(snapshot);
                    _lastHistoryPersistAt = snapshot.CapturedAt;
                    await ReloadHistoryAsync();
                }
                catch (Exception ex)
                {
                    nonFatalWarning = $"Battery data was read, but history could not be updated: {ex.Message}";
                }
            }

            RaiseFormattedProperties();
            Error = nonFatalWarning;
            ((RelayCommand)TestCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Error = $"Battery read failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Applies the history write cadence so the database is useful without receiving a row on every 2-5 second poll.
    /// </summary>
    private bool ShouldPersistHistory(DateTimeOffset now)
        => _lastHistoryPersistAt is null || now - _lastHistoryPersistAt.Value >= HistoryInterval;

    /// <summary>
    /// Reloads recent/trend history from SQLite and rebuilds the chart collections and degradation estimates.
    /// </summary>
    private async Task ReloadHistoryAsync()
    {
        if (_history is null) return;

        var recent = await _history.GetRecentAsync(20);
        History.Clear();
        foreach (var point in recent) History.Add(point);

        var trend = await _history.GetTrendAsync(90, 200);
        _historyTrend = trend;
        HealthTrend.Clear();
        FullCapacityTrend.Clear();
        foreach (var point in trend)
        {
            if (point.HealthPercent is { } h) HealthTrend.Add(new TrendPoint(point.CapturedAt, h));
            if (point.FullChargeWh is { } wh) FullCapacityTrend.Add(new TrendPoint(point.CapturedAt, wh));
        }
    }

    /// <summary>
    /// Adds one snapshot to the bounded in-memory live window used for moving averages, runtime estimation, and anomaly detection.
    /// </summary>
    private void AddLiveSample(BatterySnapshot snapshot)
    {
        _liveSamples.Add(snapshot);
        var cutoff = snapshot.CapturedAt - LiveWindow;
        _liveSamples.RemoveAll(x => x.CapturedAt < cutoff);
        _electricalAssessment = _electricalAnalyzer.Analyze(_liveSamples);

        var signedPower = GetSignedPowerW(snapshot);
        if (signedPower is { } watts)
        {
            LivePowerTrend.Add(new TrendPoint(snapshot.CapturedAt, watts));
            while (LivePowerTrend.Count > 120) LivePowerTrend.RemoveAt(0);
        }

        if (snapshot.TemperatureC is { } temperature)
        {
            LiveTemperatureTrend.Add(new TrendPoint(snapshot.CapturedAt, temperature));
            while (LiveTemperatureTrend.Count > 120) LiveTemperatureTrend.RemoveAt(0);
        }

        if (_electricalAssessment.EstimatedCurrentA is { } current)
        {
            LiveCurrentTrend.Add(new TrendPoint(snapshot.CapturedAt, current));
            while (LiveCurrentTrend.Count > 120) LiveCurrentTrend.RemoveAt(0);
        }
    }

    /// <summary>
    /// Starts the selected discharge/collapse test when idle or stops the current test when already running.
    /// </summary>
    private void ToggleTest()
    {
        if (IsTestRunning)
        {
            StopTest("user_stop", "TestComplete");
            return;
        }

        if (_snapshot?.Discharging != true)
        {
            _testStatusKey = "TestNeedUnplug";
            OnPropertyChanged(nameof(TestStatus));
            return;
        }

        _testSamples.Clear();
        _testRawSamples.Clear();
        TestPowerTrend.Clear();
        TestAnomalies.Clear();
        _testSummary = null;
        _testExportStatus = null;
        _testStatusKey = "TestRunning";

        try
        {
            _testJournal.StartSession(_snapshot, _selectedTestProfile?.Code ?? "standard");
        }
        catch (Exception ex)
        {
            Error = $"Battery test could not create its recovery journal: {ex.Message}";
            return;
        }

        IsTestRunning = true;
        AddTestSample(_snapshot);
        RebuildTestAnomalies();
        UpdateTestSummary();
        OnPropertyChanged(nameof(TestStatus));
        RaiseTestProperties();
    }

    /// <summary>
    /// Finalizes a running test, closes its crash-safe journal, computes the final summary, and updates user-visible status.
    /// </summary>
    private void StopTest(string reason, string statusKey)
    {
        IsTestRunning = false;
        _testStatusKey = statusKey;
        UpdateTestSummary();
        try { _testJournal.CompleteSession(reason); }
        catch (Exception ex) { Error = $"Battery test journal could not be finalized: {ex.Message}"; }
        OnPropertyChanged(nameof(TestStatus));
        RaiseTestProperties();
    }

    /// <summary>
    /// Updates test elapsed state on each telemetry poll and automatically stops timed profiles when their target duration is reached.
    /// </summary>
    private void UpdateBatteryTest(BatterySnapshot snapshot)
    {
        if (!IsTestRunning) return;

        if (snapshot.Discharging != true)
        {
            _testStatusKey = "TestPausedPluggedIn";
            OnPropertyChanged(nameof(TestStatus));
            return;
        }

        _testStatusKey = "TestRunning";
        AddTestSample(snapshot);
        RebuildTestAnomalies();
        UpdateTestSummary();
        OnPropertyChanged(nameof(TestStatus));
        RaiseTestProperties();

        if (_selectedTestProfile is { TargetMinutes: > 0 } profile &&
            _testSummary is { } summary &&
            summary.Duration >= TimeSpan.FromMinutes(profile.TargetMinutes))
        {
            StopTest("timed_complete", "TestAutoComplete");
        }
    }

    /// <summary>
    /// Converts a BatterySnapshot into the compact test sample format and appends it to both memory and the crash-safe journal.
    /// </summary>
    private void AddTestSample(BatterySnapshot snapshot)
    {
        if (_testRawSamples.Count > 0 &&
            snapshot.CapturedAt - _testRawSamples[^1].CapturedAt < TimeSpan.FromSeconds(2))
            return;

        _testRawSamples.Add(snapshot);
        double? power = snapshot.DischargeRateMW is { } d ? Math.Abs(d) / 1000.0 : null;
        double? voltage = snapshot.VoltageMV is { } mv ? mv / 1000.0 : null;
        var sample = new BatteryTestSample(
            snapshot.CapturedAt,
            snapshot.EstimatedChargePercent is { } pct ? Math.Clamp(pct, 0, 100) : null,
            voltage,
            power,
            snapshot.RemainingCapacityMWh,
            snapshot.TemperatureC);
        _testSamples.Add(sample);

        try { _testJournal.AppendSample(sample); }
        catch (Exception ex) { Error = $"Battery test sample could not be flushed to disk: {ex.Message}"; }

        if (power is { } watts)
            TestPowerTrend.Add(new TrendPoint(snapshot.CapturedAt, watts));
    }

    /// <summary>
    /// Re-runs BatteryTestAnalyzer against all test samples so provisional metrics remain live during a test.
    /// </summary>
    private void UpdateTestSummary()
    {
        var designWh = _snapshot?.DesignCapacityMWh is { } design ? design / 1000.0 : (double?)null;
        _testSummary = _testAnalyzer.Analyze(
            _testSamples,
            _testRawSamples,
            _assessment?.Score ?? _snapshot?.HealthPercent,
            designWh);
        ((AsyncCommand)ExportTestCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Returns whether a completed test summary and samples exist for export.
    /// </summary>
    private bool CanExportTestReport()
        => !IsTestRunning && _testSummary is not null && _testSamples.Count >= 2 && _snapshot is not null;

    /// <summary>
    /// Builds localized report text, exports HTML/JSON, and updates the UI with the saved location/error.
    /// </summary>
    private async Task ExportTestReportAsync()
    {
        if (!CanExportTestReport() || _testSummary is null || _snapshot is null) return;

        try
        {
            var text = BuildReportText(_testSummary);
            var observations = TestAnomalies.Select(x => new ReportObservation(x.Severity, x.Title, x.Detail)).ToList();
            var result = await _reportExporter.ExportAsync(
                _testSummary,
                _testSamples,
                _snapshot,
                observations,
                text,
                Loc.CurrentLanguage);

            _testExportStatus = $"{Loc["ExportSaved"]}: {result.HtmlPath}";
            OnPropertyChanged(nameof(TestExportStatus));

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{result.HtmlPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // Export succeeded; opening Explorer is optional.
            }
        }
        catch (Exception ex)
        {
            _testExportStatus = $"{Loc["ExportFailed"]}: {ex.Message}";
            OnPropertyChanged(nameof(TestExportStatus));
        }
    }

    /// <summary>
    /// Returns whether a recovered interrupted-session report is available for export.
    /// </summary>
    private bool CanExportRecoveredReport()
        => _recoveredInterruption is not null && _snapshot is not null;

    /// <summary>
    /// Exports recovered sudden-collapse evidence and all persisted samples to privacy-safe HTML/JSON.
    /// </summary>
    private async Task ExportRecoveredReportAsync()
    {
        if (!CanExportRecoveredReport() || _recoveredInterruption is null || _snapshot is null) return;

        try
        {
            var text = BuildRecoveredReportText();
            var result = await _collapseReportExporter.ExportAsync(
                _recoveredInterruption,
                _snapshot,
                text,
                Loc.CurrentLanguage,
                RecommendationTitle,
                RecommendationDetail);

            _recoveredExportStatus = $"{Loc["ExportSaved"]}: {result.HtmlPath}";
            OnPropertyChanged(nameof(RecoveredExportStatus));

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{result.HtmlPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // Export succeeded; opening Explorer is optional.
            }
        }
        catch (Exception ex)
        {
            _recoveredExportStatus = $"{Loc["ExportFailed"]}: {ex.Message}";
            OnPropertyChanged(nameof(RecoveredExportStatus));
        }
    }

    private IReadOnlyDictionary<string, string> BuildRecoveredReportText()
    {
        var keys = new[]
        {
            "CollapseReportTitle", "GeneratedAt", "RecoveredCollapseHighTitle", "RecoveredCollapsePossibleTitle", "RecoveredCollapseHighDetail",
            "RecoveredCollapsePossibleDetail", "RecoveredLastCharge", "RecoveredCurrentCharge", "RecoveredEnergy",
            "RecoveredLastRemaining", "RecoveredLastVoltage", "RecoveredObservedEnergyVsDesign",
            "RecoveredObservedVsReportedAvailable", "RecoveredBatteryReliability", "RecoveredGaugeReliability",
            "RecoveredVoltageSagScore", "RecoveredSagEvents", "RecoveredMaxVoltageSag", "RecoveredFirmwareChange",
            "DiagnosticSummary", "DiagnosticCapacity", "DiagnosticGauge", "DiagnosticVoltage", "DiagnosticShutdown",
            "DiagnosticOverall", "CollapseEvidenceDetails", "BatteryInformation", "BatteryNameLabel",
            "DesignCapacity", "FullChargeCapacity", "CurrentFirmwareHealth", "Wear", "CycleCount", "BatteryTemperature", "EstimatedCurrent",
            "Reliability_good", "Reliability_fair", "Reliability_poor", "Reliability_critical",
            "SagSeverity_unknown", "SagSeverity_none", "SagSeverity_low", "SagSeverity_moderate", "SagSeverity_high", "SagSeverity_critical",
            "ReportSamples", "ReportTime", "ReportCharge", "Voltage", "CurrentPower", "ReportEstimatedCurrent", "ReportTemperature", "ReportRemainingWh",
            "CollapseReportDisclaimer"
        };
        return keys.ToDictionary(x => x, x => Loc[x]);
    }

    private IReadOnlyDictionary<string, string> BuildReportText(BatteryTestSummary summary)
    {
        var keys = new[]
        {
            "ReportTitle", "GeneratedAt", "TestVerdictHeading", "BatteryInformation", "BatteryNameLabel",
            "HealthScore", "DesignCapacity", "FullChargeCapacity", "CycleCount", "BatteryTest",
            "TestDuration", "TestChargeStartEnd", "TestChargeDrop", "TestAveragePower", "TestEnergyUsed",
            "TestAverageVoltage", "TestVoltageRange", "TestValidPowerSamples", "TestObservations",
            "TestConfidence", "TestEnergyAgreement", "TestDataConsistency", "TestEstimatedUsableCapacity", "TestEstimatedUsableHealth",
            "TestVoltageSagScore", "TestVoltageSagEvents", "TestMaxVoltageSag",
            "BatteryTemperature", "EstimatedCurrent", "TestAverageTemperature", "TestTemperatureRange", "TestTemperatureRise",
            "TestAverageCurrent", "TestDynamicResistance", "TestPowerTemperatureCorrelation",
            "SagSeverity_unknown", "SagSeverity_none", "SagSeverity_low", "SagSeverity_moderate", "SagSeverity_high", "SagSeverity_critical",
            "TestConfidence_very_low", "TestConfidence_low", "TestConfidence_medium", "TestConfidence_high",
            "DataConsistency_good", "DataConsistency_fair", "DataConsistency_poor", "DataConsistency_unknown",
            "ReportSamples", "ReportTime", "ReportCharge", "Voltage", "CurrentPower", "ReportEstimatedCurrent", "ReportTemperature", "ReportRemainingWh",
            "ReportDisclaimer"
        };
        var dict = keys.ToDictionary(x => x, x => Loc[x]);
        dict["VerdictTitle"] = TestVerdict;
        dict["VerdictDetail"] = BuildVerdictDetail(summary);
        return dict;
    }

    private (string Title, string Detail) GetRecommendation()
    {
        if (_recoveredInterruption?.StrongGaugeJumpDetected == true ||
            _recoveredInterruption?.BatteryReliabilityScore <= 20)
        {
            return (Loc["RecommendationCriticalTitle"], Loc["RecommendationCriticalDetail"]);
        }

        if (_recoveredInterruption?.PossibleSuddenCollapse == true ||
            _recoveredInterruption?.GaugeReliabilityScore < 35 ||
            _recoveredInterruption?.VoltageSagSeverityScore >= 75)
        {
            return (Loc["RecommendationWarningTitle"], Loc["RecommendationWarningDetail"]);
        }

        if (_assessment?.Score is < 60)
            return (Loc["RecommendationReplaceTitle"], Loc["RecommendationReplaceDetail"]);

        return (Loc["RecommendationMonitorTitle"], Loc["RecommendationMonitorDetail"]);
    }

    /// <summary>
    /// Converts recovered collapse/gauge-jump evidence into a conservative 0-100 shutdown-reliability score.
    /// </summary>
    private int ShutdownReliabilityScore()
    {
        if (_recoveredInterruption?.StrongGaugeJumpDetected == true) return 0;
        if (_recoveredInterruption?.PossibleSuddenCollapse == true) return 20;
        return 100;
    }

    /// <summary>
    /// Maps a numeric reliability score to good/fair/poor/critical presentation bands.
    /// </summary>
    private static string ReliabilityCode(int score) => score switch
    {
        >= 80 => "good",
        >= 60 => "fair",
        >= 35 => "poor",
        _ => "critical"
    };

    /// <summary>
    /// Rebuilds capacity/health findings shown on the dashboard from the latest snapshot and smoothed health value.
    /// </summary>
    private void RebuildFindings()
    {
        Findings.Clear();
        if (_assessment is null) return;
        foreach (var f in _assessment.Findings)
        {
            var detail = Loc[f.DetailKey]
                .Replace("{health}", _assessment.Score?.ToString("0.0") ?? "—")
                .Replace("{cycles}", _snapshot?.CycleCount?.ToString("N0") ?? "—")
                .Replace("{voltage}", _snapshot?.VoltageMV is { } mv ? (mv / 1000.0).ToString("0.00") : "—");
            Findings.Add(new DisplayFinding(f.Severity, Loc[f.TitleKey], detail));
        }
    }

    /// <summary>
    /// Runs anomaly detection against the bounded live telemetry window and refreshes the live warning list.
    /// </summary>
    private void RebuildLiveAnomalies()
    {
        LiveAnomalies.Clear();
        var findings = _anomalyDetector.Analyze(_liveSamples);
        if (findings.Count == 0)
        {
            LiveAnomalies.Add(new DisplayFinding("good", Loc["NoAnomaliesTitle"], Loc["NoAnomaliesDetail"]));
            return;
        }
        foreach (var finding in findings)
            LiveAnomalies.Add(ToDisplayFinding(finding));
    }

    /// <summary>
    /// Converts the current test summary anomalies into localized UI findings.
    /// </summary>
    private void RebuildTestAnomalies()
    {
        TestAnomalies.Clear();
        var findings = _anomalyDetector.Analyze(_testRawSamples);
        if (findings.Count == 0)
        {
            TestAnomalies.Add(new DisplayFinding("good", Loc["NoTestAnomaliesTitle"], Loc["NoTestAnomaliesDetail"]));
            return;
        }
        foreach (var finding in findings)
            TestAnomalies.Add(ToDisplayFinding(finding));
    }

    /// <summary>
    /// Maps an internal anomaly code/value into localized title/detail strings suitable for the UI.
    /// </summary>
    private DisplayFinding ToDisplayFinding(AnomalyFinding finding)
    {
        var value = finding.Value?.ToString("0.0") ?? "—";
        return new DisplayFinding(
            finding.Severity,
            Loc[finding.TitleKey],
            Loc[finding.DetailKey].Replace("{value}", value));
    }

    /// <summary>
    /// Builds the localized test verdict explanation and inserts the current numeric evidence into placeholders.
    /// </summary>
    private string BuildVerdictDetail(BatteryTestSummary summary)
    {
        var detail = Loc[$"TestVerdict_{summary.VerdictCode}_Detail"];
        return detail
            .Replace("{health}", summary.BatteryHealthPercent?.ToString("0.0") ?? "—")
            .Replace("{minutes}", Math.Max(0, summary.Duration.TotalMinutes).ToString("0"));
    }

    /// <summary>
    /// Recreates localized test-profile options while preserving the previously selected profile code where possible.
    /// </summary>
    private void RebuildTestProfiles(string preferredCode)
    {
        TestProfiles.Clear();
        TestProfiles.Add(new TestProfileOption("quick", Loc["TestProfile_quick"], 5));
        TestProfiles.Add(new TestProfileOption("standard", Loc["TestProfile_standard"], 15));
        TestProfiles.Add(new TestProfileOption("deep", Loc["TestProfile_deep"], 30));
        TestProfiles.Add(new TestProfileOption("collapse", Loc["TestProfile_collapse"], 0, CollapseWatch: true));
        _selectedTestProfile = TestProfiles.FirstOrDefault(x => x.Code == preferredCode) ?? TestProfiles[1];
        OnPropertyChanged(nameof(TestProfiles));
        OnPropertyChanged(nameof(SelectedTestProfile));
        OnPropertyChanged(nameof(TestProfileHint));
        OnPropertyChanged(nameof(TestTarget));
    }

    /// <summary>
    /// Raises all dependent bindings that change when recovered-collapse evidence is loaded or recalculated.
    /// </summary>
    private void RaiseRecoveryProperties()
    {
        foreach (var name in new[]
        {
            nameof(HasRecoveredInterruption), nameof(RecoveredInterruptionTitle), nameof(RecoveredInterruptionDetail),
            nameof(RecoveredLastCharge), nameof(RecoveredCurrentCharge), nameof(RecoveredEnergy),
            nameof(RecoveredObservedEnergyVsDesign), nameof(RecoveredObservedVsReportedAvailable), nameof(RecoveredLastRemaining),
            nameof(RecoveredLastVoltage), nameof(RecoveredVoltageSagScore), nameof(RecoveredMaxVoltageSag), nameof(RecoveredSagEvents),
            nameof(RecoveredGaugeReliability), nameof(RecoveredBatteryReliability), nameof(RecoveredFirmwareChange),
            nameof(RecoveredFirmwareEvent), nameof(RecoveredConfidence), nameof(RecoveredJournalPath), nameof(RecoveredExportStatus),
            nameof(DiagnosticCapacity), nameof(DiagnosticGauge), nameof(DiagnosticVoltage), nameof(DiagnosticShutdown),
            nameof(DiagnosticOverall), nameof(PercentageTrustStatus), nameof(RecommendationTitle), nameof(RecommendationDetail),
            nameof(CapacitySourceNote)
        }) OnPropertyChanged(name);
        ((AsyncCommand)ExportRecoveredCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Evaluates current telemetry against critical/gauge/sag rules and emits a throttled background warning only when user attention is warranted.
    /// </summary>
    private void EvaluateBackgroundAlerts(BatterySnapshot snapshot)
    {
        if (!BackgroundMonitoring || snapshot.Discharging != true) return;
        var now = snapshot.CapturedAt;

        if (!_criticalRecoveryNotificationSent && _recoveredInterruption?.BatteryReliabilityScore <= 20)
        {
            _criticalRecoveryNotificationSent = true;
            RaiseBackgroundAlert(
                "critical",
                Loc["AlertReliabilityCriticalTitle"],
                Loc["AlertReliabilityCriticalDetail"].Replace("{score}", _recoveredInterruption.BatteryReliabilityScore.ToString()),
                now);
            _lastCriticalNotificationAt = now;
        }

        if (snapshot.Critical == true && now - _lastCriticalNotificationAt >= TimeSpan.FromMinutes(10))
        {
            RaiseBackgroundAlert("critical", Loc["AlertBatteryCriticalTitle"], Loc["AlertBatteryCriticalDetail"], now);
            _lastCriticalNotificationAt = now;
        }

        var recentTestSamples = _liveSamples
            .Where(x => x.Discharging == true && now - x.CapturedAt <= TimeSpan.FromMinutes(2))
            .Select(x => new BatteryTestSample(
                x.CapturedAt,
                x.EstimatedChargePercent,
                x.VoltageMV is { } mv ? mv / 1000.0 : null,
                x.DischargeRateMW is { } mw ? Math.Abs(mw) / 1000.0 : null,
                x.RemainingCapacityMWh))
            .ToList();
        var sag = _voltageSagAnalyzer.Analyze(recentTestSamples);
        if (sag.SeverityScore >= 75 && sag.MaxSagV is >= 1.5 && now - _lastSagNotificationAt >= TimeSpan.FromMinutes(10))
        {
            RaiseBackgroundAlert(
                "critical",
                Loc["AlertVoltageSagTitle"],
                Loc["AlertVoltageSagDetail"]
                    .Replace("{sag}", sag.MaxSagV.Value.ToString("0.00"))
                    .Replace("{charge}", FormatPercent(snapshot.EstimatedChargePercent)),
                now);
            _lastSagNotificationAt = now;
        }

        var findings = _anomalyDetector.Analyze(_liveSamples);
        var gaugeFinding = findings.FirstOrDefault(x => x.TitleKey is "AnomalySuddenDropTitle" or "AnomalyRapidCollapseTitle");
        if (gaugeFinding is not null && now - _lastGaugeNotificationAt >= TimeSpan.FromMinutes(10))
        {
            RaiseBackgroundAlert(
                "critical",
                Loc["AlertGaugeJumpTitle"],
                Loc["AlertGaugeJumpDetail"].Replace("{value}", gaugeFinding.Value?.ToString("0.0") ?? "—"),
                now);
            _lastGaugeNotificationAt = now;
        }
    }

    /// <summary>
    /// Applies alert cooldown/deduplication and raises the event consumed by the system-tray shell.
    /// </summary>
    private void RaiseBackgroundAlert(string severity, string title, string detail, DateTimeOffset capturedAt)
    {
        _lastBackgroundAlertTitle = title;
        _lastBackgroundAlertDetail = detail;
        _lastBackgroundAlertAt = capturedAt;
        OnPropertyChanged(nameof(HasBackgroundAlert));
        OnPropertyChanged(nameof(LastBackgroundAlertTitle));
        OnPropertyChanged(nameof(LastBackgroundAlertDetail));
        OnPropertyChanged(nameof(LastBackgroundAlertTime));

        if (TrayNotifications)
            BackgroundAlertRaised?.Invoke(this, new BackgroundAlertEventArgs(severity, title, detail, capturedAt));
    }

    /// <summary>
    /// Creates the privacy-safe support snapshot and reports the output path/status to the UI.
    /// </summary>
    private async Task ExportDiagnosticSnapshotAsync()
    {
        try
        {
            var path = await _diagnosticExporter.ExportAsync(_snapshot, _recoveredInterruption, AppVersion);
            _diagnosticExportStatus = $"{Loc["ExportSaved"]}: {path}";
            OnPropertyChanged(nameof(DiagnosticExportStatus));
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch { }
        }
        catch (Exception ex)
        {
            _diagnosticExportStatus = $"{Loc["ExportFailed"]}: {ex.Message}";
            OnPropertyChanged(nameof(DiagnosticExportStatus));
        }
    }

    /// <summary>
    /// Writes current tray/background preferences to the portable settings file.
    /// </summary>
    private void PersistSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            Error = $"{Loc["SettingsSaveFailed"]}: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens a local folder in Windows Explorer without passing it through a shell command string.
    /// </summary>
    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // Opening Explorer is optional and should not affect battery monitoring.
        }
    }

    /// <summary>
    /// Opens an explicitly configured http/https project or donation URL in the default browser.
    /// </summary>
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Link opening is optional.
        }
    }

    /// <summary>
    /// Calculates and formats the change in historical health over a requested number of days when sufficient history exists.
    /// </summary>
    private string FormatHealthChange(int days)
    {
        var current = _assessment?.Score ?? _snapshot?.HealthPercent;
        if (current is null || _historyTrend.Count < 2) return Loc["NotEnoughHistory"];

        var target = DateTimeOffset.Now.AddDays(-days);
        var candidate = _historyTrend
            .Where(x => x.HealthPercent is not null)
            .OrderBy(x => Math.Abs((x.CapturedAt - target).TotalDays))
            .FirstOrDefault();
        if (candidate is null || candidate.HealthPercent is not { } oldHealth ||
            Math.Abs((candidate.CapturedAt - target).TotalDays) > 3)
            return Loc["NotEnoughHistory"];

        var delta = current.Value - oldHealth;
        return $"{delta:+0.0;-0.0;0.0} pp";
    }

    /// <summary>
    /// Formats the moving-average discharge power for a requested live window.
    /// </summary>
    private string FormatPowerAverage(TimeSpan window)
    {
        var value = AverageDischargeW(window);
        return value is { } w ? $"↓ {w:0.0} W" : "—";
    }

    /// <summary>
    /// Calculates mean positive discharge watts from valid samples in the requested time window.
    /// </summary>
    private double? AverageDischargeW(TimeSpan window)
    {
        var values = GetDischargePowerSamples(window);
        return values.Count == 0 ? null : values.Average();
    }

    /// <summary>
    /// Returns valid discharge-power samples within a recent time window, excluding charging/invalid values.
    /// </summary>
    private List<double> GetDischargePowerSamples(TimeSpan window)
    {
        if (_snapshot is null) return new List<double>();
        var cutoff = _snapshot.CapturedAt - window;
        return _liveSamples
            .Where(x => x.CapturedAt >= cutoff && x.Discharging == true && x.DischargeRateMW is not null)
            .Select(x => Math.Abs(x.DischargeRateMW!.Value) / 1000.0)
            .Where(x => x > 0)
            .ToList();
    }

    /// <summary>
    /// Estimates runtime from a moving-average discharge window and current remaining capacity.
    /// </summary>
    private int? CalculateRuntimeMinutes(TimeSpan window)
        => CalculateRuntimeFromPower(AverageDischargeW(window));

    /// <summary>
    /// Estimates runtime from the latest instantaneous discharge-rate reading.
    /// </summary>
    private int? CalculateInstantRuntimeMinutes()
    {
        if (_snapshot?.Discharging != true || _snapshot.DischargeRateMW is not { } rate || rate == 0)
            return null;
        return CalculateRuntimeFromPower(Math.Abs(rate) / 1000.0);
    }

    /// <summary>
    /// Converts remaining watt-hours and discharge watts into bounded runtime minutes.
    /// </summary>
    private int? CalculateRuntimeFromPower(double? powerW)
    {
        if (powerW is not > 0 || _snapshot is null) return null;

        double? remainingMWh = _snapshot.RemainingCapacityMWh;
        if (remainingMWh is null && _snapshot.FullChargeCapacityMWh is { } full && _snapshot.EstimatedChargePercent is { } pct)
            remainingMWh = full * Math.Clamp(pct, 0, 100) / 100.0;
        if (remainingMWh is not > 0) return null;

        var minutes = remainingMWh.Value / (powerW.Value * 1000.0) * 60.0;
        return minutes is > 0 and < 14400 ? (int)Math.Round(minutes) : null;
    }

    /// <summary>
    /// Chooses the most stable available runtime estimate, preferring longer moving averages over instantaneous data.
    /// </summary>
    private int? CalculateBestRuntimeMinutes()
    {
        if (_snapshot?.Discharging != true) return null;

        var fiveMinuteSamples = GetDischargePowerSamples(TimeSpan.FromMinutes(5));
        if (fiveMinuteSamples.Count >= 36)
            return CalculateRuntimeFromPower(fiveMinuteSamples.Average());

        var oneMinuteSamples = GetDischargePowerSamples(TimeSpan.FromMinutes(1));
        if (oneMinuteSamples.Count >= 6)
            return CalculateRuntimeFromPower(oneMinuteSamples.Average());

        return CalculateInstantRuntimeMinutes() ?? _snapshot.EstimatedRuntimeMinutes;
    }

    /// <summary>
    /// Returns the localization key/text identifying whether runtime came from 5-minute, 1-minute, instantaneous, or firmware data.
    /// </summary>
    private string GetRuntimeSource()
    {
        if (_snapshot?.Discharging != true) return Loc["RuntimeOnlyOnBattery"];
        if (GetDischargePowerSamples(TimeSpan.FromMinutes(5)).Count >= 36) return Loc["RuntimeSource5Minute"];
        if (GetDischargePowerSamples(TimeSpan.FromMinutes(1)).Count >= 6) return Loc["RuntimeSource1Minute"];
        if (CalculateInstantRuntimeMinutes() is not null) return Loc["RuntimeSourceInstant"];
        if (_snapshot?.EstimatedRuntimeMinutes is not null) return Loc["RuntimeSourceFirmware"];
        return Loc["RuntimeUnavailable"];
    }

    /// <summary>
    /// Uses the median of recent valid firmware-health samples to reduce UI flicker from small FullChargeCapacity oscillations.
    /// </summary>
    private double? GetSmoothedHealthPercent()
    {
        var values = _liveSamples
            .Where(x => x.HealthPercent is not null)
            .TakeLast(24)
            .Select(x => x.HealthPercent!.Value)
            .OrderBy(x => x)
            .ToList();

        if (values.Count == 0) return _snapshot?.HealthPercent;
        if (values.Count < 3) return values[^1];
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }

    /// <summary>
    /// Counts valid recent health samples contributing to smoothing/confidence display.
    /// </summary>
    private int CountHealthSamples()
        => _liveSamples.Count(x => x.HealthPercent is not null);

    /// <summary>
    /// Normalizes charge power as positive and discharge power as negative watts for charts and history.
    /// </summary>
    private static double? GetSignedPowerW(BatterySnapshot snapshot)
    {
        if (snapshot.Discharging == true && snapshot.DischargeRateMW is { } d)
            return -Math.Abs(d) / 1000.0;
        if (snapshot.Charging == true && snapshot.ChargeRateMW is { } c)
            return Math.Abs(c) / 1000.0;
        return null;
    }

    /// <summary>
    /// Formats milliwatt-hours as human-readable watt-hours.
    /// </summary>
    private static string FormatWh(uint? mwh) => mwh is { } v ? $"{v / 1000.0:0.0} Wh" : "—";
    /// <summary>
    /// Clamps raw firmware charge to 0-100 for presentation while raw telemetry can still be preserved elsewhere for diagnostics.
    /// </summary>
    private static string FormatPercent(int? value) => value is { } v ? $"{Math.Clamp(v, 0, 100)}%" : "—";

    /// <summary>
    /// Formats nullable runtime minutes as a concise hour/minute string.
    /// </summary>
    private static string FormatMinutes(int? minutes)
        => minutes is { } m && m > 0 ? $"{m / 60}h {m % 60:00}m" : "—";

    /// <summary>
    /// Formats a TimeSpan for test-duration display.
    /// </summary>
    private static string FormatDuration(TimeSpan span)
        => span.TotalHours >= 1 ? $"{(int)span.TotalHours:0}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes:00}:{span.Seconds:00}";

    /// <summary>
    /// Raises all bindings derived from the current test/test-summary in one place after test state changes.
    /// </summary>
    private void RaiseTestProperties()
    {
        foreach (var name in new[]
        {
            nameof(TestButtonText), nameof(TestStatus), nameof(TestDuration), nameof(TestChargeDrop),
            nameof(TestAveragePower), nameof(TestVoltageRange), nameof(TestSampleCount), nameof(TestValidPowerSamples),
            nameof(TestChargeStartEnd), nameof(TestEnergyUsed), nameof(TestAverageVoltage), nameof(TestCapacityDelta), nameof(TestCapacityDeltaNote),
            nameof(TestVerdict), nameof(TestVerdictDetail), nameof(TestVerdictMode), nameof(TestExportStatus),
            nameof(TestConfidence), nameof(TestEnergyAgreement), nameof(TestDataConsistency), nameof(TestEstimatedUsableCapacity),
            nameof(TestEstimatedUsableHealth), nameof(TestVoltageSagScore), nameof(TestVoltageSagEvents), nameof(TestMaxVoltageSag),
            nameof(TestTemperatureRange), nameof(TestAverageTemperature), nameof(TestTemperatureRise), nameof(TestAverageCurrent),
            nameof(TestDynamicResistance), nameof(TestPowerTemperatureCorrelation),
            nameof(IsTestProfileEnabled), nameof(TestProfileHint), nameof(TestTarget)
        }) OnPropertyChanged(name);
        OnPropertyChanged(nameof(TestPowerTrend));
        OnPropertyChanged(nameof(TestAnomalies));
        ((AsyncCommand)ExportTestCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Raises all formatted/dashboard bindings after a new telemetry snapshot or localization change.
    /// </summary>
    private void RaiseFormattedProperties()
    {
        foreach (var name in new[]
        {
            nameof(HealthScore), nameof(HealthEstimateHint), nameof(CapacitySourceNote), nameof(Condition), nameof(Summary), nameof(DesignCapacity),
            nameof(FullChargeCapacity), nameof(Wear), nameof(CycleCount), nameof(Voltage), nameof(BatteryTemperature),
            nameof(BatteryTemperatureSource), nameof(EstimatedCurrent), nameof(DynamicResistance), nameof(DynamicResistanceConfidence),
            nameof(TemperatureChange5Min), nameof(TemperatureRate), nameof(PowerTemperatureCorrelation), nameof(TemperatureSampleCount),
            nameof(CurrentCharge),
            nameof(DiagnosticCapacity), nameof(DiagnosticGauge), nameof(DiagnosticVoltage), nameof(DiagnosticShutdown),
            nameof(DiagnosticOverall), nameof(PercentageTrustStatus), nameof(RecommendationTitle), nameof(RecommendationDetail),
            nameof(Runtime), nameof(RuntimeSource), nameof(BatteryName), nameof(ChemistryDisplay), nameof(LastUpdated), nameof(PowerState),
            nameof(PowerRate), nameof(PowerDataStatus), nameof(DatabasePath), nameof(LastUpdatedDisplay),
            nameof(Average1Minute), nameof(Average5Minutes), nameof(Runtime1Minute), nameof(Runtime5Minutes),
            nameof(LiveSampleCount), nameof(LiveValidPowerSampleCount), nameof(HealthChange30Days),
            nameof(HealthChange90Days), nameof(HistorySpan), nameof(TestButtonText), nameof(TestStatus)
        }) OnPropertyChanged(name);
        OnPropertyChanged(nameof(Findings));
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(LivePowerTrend));
        OnPropertyChanged(nameof(LiveTemperatureTrend));
        OnPropertyChanged(nameof(LiveCurrentTrend));
        OnPropertyChanged(nameof(HealthTrend));
        OnPropertyChanged(nameof(FullCapacityTrend));
        OnPropertyChanged(nameof(LiveAnomalies));
        RaiseTestProperties();
    }

    /// <summary>
    /// Standard INotifyPropertyChanged helper used by WPF data binding.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// ICommand adapter for asynchronous view-model actions.
/// </summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _executing;

    /// <summary>
    /// Wraps an asynchronous action as ICommand and prevents overlapping executions.
    /// </summary>
    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    /// <summary>
    /// Returns whether a command is currently allowed to execute.
    /// </summary>
    public bool CanExecute(object? parameter) => !_executing && (_canExecute?.Invoke() ?? true);

    /// <summary>
    /// Runs the configured command action and updates command enabled state around execution.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _executing = true;
        RaiseCanExecuteChanged();
        try { await _execute(); }
        finally { _executing = false; RaiseCanExecuteChanged(); }
    }

    /// <summary>
    /// Notifies WPF that a command enabled/disabled state should be re-evaluated.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// ICommand adapter for synchronous view-model actions.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Wraps a synchronous Action as ICommand with an optional CanExecute predicate.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    /// <summary>
    /// Returns whether a command is currently allowed to execute.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    /// <summary>
    /// Runs the configured command action and updates command enabled state around execution.
    /// </summary>
    public void Execute(object? parameter) => _execute();
    /// <summary>
    /// Notifies WPF that a command enabled/disabled state should be re-evaluated.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Localized finding displayed by the dashboard/test UI.
/// </summary>
public sealed record DisplayFinding(string Severity, string Title, string Detail);
