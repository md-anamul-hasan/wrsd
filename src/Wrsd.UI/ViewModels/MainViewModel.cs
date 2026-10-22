using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Wrsd.Core.Diff;
using Wrsd.Core.Exporters;
using Wrsd.Core.Models;
using Wrsd.Core.Scanning;
using Wrsd.Core.Storage;
using Wrsd.UI.Services;

namespace Wrsd.UI.ViewModels;

public enum EnterpriseNavTab
{
    DiffInspector = 0,
    SnapshotVault = 1,
    ThreatHunter = 2,
    SiemReports = 3,
    SettingsRules = 4
}

public sealed class MainViewModel : ObservableObject
{
    private readonly RegistryScanner _scanner = new();
    private readonly DispatcherTimer _actionTimer;
    private Stopwatch? _actionStopwatch;
    private CancellationTokenSource? _scanCts;

    // Navigation & View Toggles
    private EnterpriseNavTab _currentNavTab = EnterpriseNavTab.DiffInspector;
    private bool _isSideBySideView;

    // Scan & Diff State
    private SnapshotScopePreset _selectedPreset = SnapshotScopePreset.QuickTriage;
    private RegistrySnapshot? _baselineSnapshot;
    private RegistrySnapshot? _targetSnapshot;
    private SnapshotDiffReport? _diffReport;

    private readonly List<KeyDiffViewModel> _allKeyViewModels = new();

    private bool _isScanning;
    private string _statusMessage = "Ready to take baseline snapshot";
    private string _scanProgressText = string.Empty;
    private string _elapsedActionTime = "00:00:00";
    private bool _isActionPhaseActive;

    // Filters
    private bool _hideNoise = true;
    private bool _persistenceOnly;
    private string _searchText = string.Empty;
    private DiffChangeType? _selectedChangeType;

    private KeyDiffViewModel? _selectedKey;
    private ThreatItemViewModel? _selectedThreatItem;

    private SnapshotVaultItemViewModel? _selectedVaultSnapshotA;
    private SnapshotVaultItemViewModel? _selectedVaultSnapshotB;

    public MainViewModel()
    {
        _actionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _actionTimer.Tick += (s, e) =>
        {
            if (_actionStopwatch != null && _actionStopwatch.IsRunning)
            {
                ElapsedActionTime = _actionStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
            }
        };

        // Navigation Commands
        SwitchNavTabCommand = new RelayCommand(p =>
        {
            if (p is EnterpriseNavTab tab)
            {
                CurrentNavTab = tab;
            }
            else if (p is string s && Enum.TryParse<EnterpriseNavTab>(s, out var parsedTab))
            {
                CurrentNavTab = parsedTab;
            }
        });

        ToggleSideBySideViewCommand = new RelayCommand(() => IsSideBySideView = !IsSideBySideView);

        // Core Workflow Commands
        TakeBaselineCommand = new RelayCommand(async () => await TakeBaselineAsync(), () => !IsScanning);
        LoadBaselineCommand = new RelayCommand(async () => await LoadBaselineAsync(), () => !IsScanning);
        SaveBaselineCommand = new RelayCommand(async () => await SaveBaselineAsync(), () => BaselineSnapshot != null && !IsScanning);

        TakeTargetAndDiffCommand = new RelayCommand(async () => await TakeTargetAndDiffAsync(), () => BaselineSnapshot != null && !IsScanning);
        LoadTargetAndDiffCommand = new RelayCommand(async () => await LoadTargetAndDiffAsync(), () => BaselineSnapshot != null && !IsScanning);
        SaveTargetCommand = new RelayCommand(async () => await SaveTargetAsync(), () => TargetSnapshot != null && !IsScanning);

        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        LaunchInstallerCommand = new RelayCommand(LaunchInstaller, () => !IsScanning);
        ResetCommand = new RelayCommand(ResetWorkflow, () => !IsScanning);

        // Exporter Commands
        ExportUndoRegCommand = new RelayCommand(async () => await ExportUndoRegAsync(), () => DiffReport != null);
        ExportRedoRegCommand = new RelayCommand(async () => await ExportRedoRegAsync(), () => DiffReport != null);
        ExportHtmlCommand = new RelayCommand(async () => await ExportHtmlAsync(), () => DiffReport != null);
        ExportJsonCommand = new RelayCommand(async () => await ExportJsonAsync(), () => DiffReport != null);
        ExportCsvCommand = new RelayCommand(async () => await ExportCsvAsync(), () => DiffReport != null);

        // Threat Hunter Commands
        ExportThreatRollbackRegCommand = new RelayCommand(async () => await ExportThreatRollbackRegAsync(), () => ThreatItems.Count > 0);

        // Vault Commands
        RefreshVaultCommand = new RelayCommand(RefreshVaultSnapshots);
        CompareSelectedVaultSnapshotsCommand = new RelayCommand(async () => await CompareVaultSnapshotsAsync(), () => SelectedVaultSnapshotA != null && SelectedVaultSnapshotB != null && !IsScanning);
        DeleteVaultSnapshotCommand = new RelayCommand(DeleteSelectedVaultSnapshot);
        ImportSnapshotToVaultCommand = new RelayCommand(async () => await ImportSnapshotToVaultAsync());

        // Utilities
        CopyKeyPathCommand = new RelayCommand(CopyKeyPath, () => SelectedKey != null);
        OpenInRegeditCommand = new RelayCommand(OpenInRegedit, () => SelectedKey != null);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => !string.IsNullOrEmpty(SearchText));

        CopyValueNameCommand = new RelayCommand(p =>
        {
            if (p is ValueDiffViewModel v)
            {
                Clipboard.SetText(v.ValueName);
                StatusMessage = $"Copied value name '{v.ValueName}'";
            }
        });

        CopyBeforeValueCommand = new RelayCommand(p =>
        {
            if (p is ValueDiffViewModel v)
            {
                Clipboard.SetText(v.BeforeDisplay);
                StatusMessage = "Copied Before value to clipboard";
            }
        });

        CopyAfterValueCommand = new RelayCommand(p =>
        {
            if (p is ValueDiffViewModel v)
            {
                Clipboard.SetText(v.AfterDisplay);
                StatusMessage = "Copied After value to clipboard";
            }
        });

        CopyFullRowCommand = new RelayCommand(p =>
        {
            if (p is ValueDiffViewModel v)
            {
                string text = $"{v.ValueName}\t{v.TypeDisplay}\t{v.BadgeText}\t{v.BeforeDisplay}\t{v.AfterDisplay}";
                Clipboard.SetText(text);
                StatusMessage = "Copied row details to clipboard";
            }
        });

        SetFilterAllCommand = new RelayCommand(() => SelectedChangeType = null);
        SetFilterAddedCommand = new RelayCommand(() => SelectedChangeType = DiffChangeType.Added);
        SetFilterModifiedCommand = new RelayCommand(() => SelectedChangeType = DiffChangeType.Modified);
        SetFilterDeletedCommand = new RelayCommand(() => SelectedChangeType = DiffChangeType.Deleted);

        OpenRepositoryCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
        IsCliRuntimeMissing = !CommandLineRuntime.IsInstalled();
        InstallCliRuntimeCommand = new RelayCommand(async () => await InstallCliRuntimeAsync(), () => IsCliRuntimeMissing && !IsInstallingCliRuntime);
        PresetOptions = new ObservableCollection<PresetOption>
        {
            new(SnapshotScopePreset.QuickTriage, "Quick Triage (recommended)", @"HKLM\SOFTWARE, HKCU\Software and the services key. Fast and right for most installers.", p => SelectedPreset = p),
            new(SnapshotScopePreset.SoftwareOnly, "Software only", @"HKLM\SOFTWARE and HKCU\Software: installed programs and their settings.", p => SelectedPreset = p),
            new(SnapshotScopePreset.SystemServices, "System services", "Driver and service registrations (CurrentControlSet\\Services).", p => SelectedPreset = p),
            new(SnapshotScopePreset.PersistenceOnly, "Persistence only", "Autoruns: Run and RunOnce, Winlogon, Image File Execution Options and services.", p => SelectedPreset = p),
            new(SnapshotScopePreset.FullSystem, "Full system", "Everything under HKLM, HKCU, HKCR, HKU and HKCC. Thorough but slow and large.", p => SelectedPreset = p),
        };
        foreach (var option in PresetOptions) option.Sync(_selectedPreset);


        // Theme Commands
        ToggleThemeCommand = new RelayCommand(() => ThemeManager.CycleNextTheme());
        SetThemeSystemCommand = new RelayCommand(() => ThemeManager.SetTheme(AppThemeMode.System));
        SetThemeDarkCommand = new RelayCommand(() => ThemeManager.SetTheme(AppThemeMode.Dark));
        SetThemeLightCommand = new RelayCommand(() => ThemeManager.SetTheme(AppThemeMode.Light));

        ThemeManager.ThemeChanged += (mode, isDark) =>
        {
            OnPropertyChanged(nameof(CurrentTheme));
            OnPropertyChanged(nameof(IsThemeSystem));
            OnPropertyChanged(nameof(IsThemeDark));
            OnPropertyChanged(nameof(IsThemeLight));
            OnPropertyChanged(nameof(ThemeLabelText));
            OnPropertyChanged(nameof(ThemeTooltipText));
            OnPropertyChanged(nameof(ThemeIconGeometry));
        };

        RefreshVaultSnapshots();
    }

    // Navigation Properties
    public EnterpriseNavTab CurrentNavTab
    {
        get => _currentNavTab;
        set
        {
            if (SetProperty(ref _currentNavTab, value))
            {
                OnPropertyChanged(nameof(IsDiffInspectorActive));
                OnPropertyChanged(nameof(IsSnapshotVaultActive));
                OnPropertyChanged(nameof(IsThreatHunterActive));
                OnPropertyChanged(nameof(IsSiemReportsActive));
                OnPropertyChanged(nameof(IsSettingsRulesActive));
            }
        }
    }

    public bool IsDiffInspectorActive => CurrentNavTab == EnterpriseNavTab.DiffInspector;
    public bool IsSnapshotVaultActive => CurrentNavTab == EnterpriseNavTab.SnapshotVault;
    public bool IsThreatHunterActive => CurrentNavTab == EnterpriseNavTab.ThreatHunter;
    public bool IsSiemReportsActive => CurrentNavTab == EnterpriseNavTab.SiemReports;
    public bool IsSettingsRulesActive => CurrentNavTab == EnterpriseNavTab.SettingsRules;

    public bool IsSideBySideView
    {
        get => _isSideBySideView;
        set => SetProperty(ref _isSideBySideView, value);
    }

    public ObservableCollection<SnapshotScopePreset> Presets { get; } = new()
    {
        SnapshotScopePreset.QuickTriage,
        SnapshotScopePreset.SoftwareOnly,
        SnapshotScopePreset.SystemServices,
        SnapshotScopePreset.PersistenceOnly,
        SnapshotScopePreset.FullSystem
    };

    public SnapshotScopePreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
                foreach (var option in PresetOptions) option.Sync(value);
        }
    }

    /// <summary>The scan presets with their explanation, shown on the Settings page (same selection as the top-bar combo box).</summary>
    public ObservableCollection<PresetOption> PresetOptions { get; }

    public RegistrySnapshot? BaselineSnapshot
    {
        get => _baselineSnapshot;
        private set
        {
            if (SetProperty(ref _baselineSnapshot, value))
            {
                OnPropertyChanged(nameof(HasBaseline));
                OnPropertyChanged(nameof(BaselineSummary));
                OnPropertyChanged(nameof(BaselineBadgeText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public RegistrySnapshot? TargetSnapshot
    {
        get => _targetSnapshot;
        private set
        {
            if (SetProperty(ref _targetSnapshot, value))
            {
                OnPropertyChanged(nameof(HasTarget));
                OnPropertyChanged(nameof(TargetSummary));
                OnPropertyChanged(nameof(TargetBadgeText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public SnapshotDiffReport? DiffReport
    {
        get => _diffReport;
        private set
        {
            if (SetProperty(ref _diffReport, value))
            {
                RebuildKeyViewModels();
                OnPropertyChanged(nameof(HasDiff));
                OnPropertyChanged(nameof(HasNoDiff));
                OnPropertyChanged(nameof(TotalChanges));
                OnPropertyChanged(nameof(AddedKeysCount));
                OnPropertyChanged(nameof(DeletedKeysCount));
                OnPropertyChanged(nameof(ModifiedKeysCount));
                OnPropertyChanged(nameof(TotalValuesChanged));
                OnPropertyChanged(nameof(PersistenceRisksCount));
                OnPropertyChanged(nameof(NoiseKeysCount));
                UpdateFilteredList();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<KeyDiffViewModel> FilteredKeys { get; } = new();
    public ObservableCollection<ThreatItemViewModel> ThreatItems { get; } = new();
    public ObservableCollection<SnapshotVaultItemViewModel> VaultSnapshots { get; } = new();

    public KeyDiffViewModel? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                OnPropertyChanged(nameof(HasSelectedKeyValues));
                OnPropertyChanged(nameof(HasNoSelectedKeyValues));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ThreatItemViewModel? SelectedThreatItem
    {
        get => _selectedThreatItem;
        set => SetProperty(ref _selectedThreatItem, value);
    }

    public SnapshotVaultItemViewModel? SelectedVaultSnapshotA
    {
        get => _selectedVaultSnapshotA;
        set
        {
            if (SetProperty(ref _selectedVaultSnapshotA, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public SnapshotVaultItemViewModel? SelectedVaultSnapshotB
    {
        get => _selectedVaultSnapshotB;
        set
        {
            if (SetProperty(ref _selectedVaultSnapshotB, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelectedKeyValues => SelectedKey != null && SelectedKey.Values.Count > 0;
    public bool HasNoSelectedKeyValues => SelectedKey != null && SelectedKey.Values.Count == 0;

    public bool HasBaseline => BaselineSnapshot != null;
    public bool HasTarget => TargetSnapshot != null;
    public bool HasDiff => DiffReport != null;
    public bool HasNoDiff => DiffReport == null;

    public string BaselineBadgeText => BaselineSnapshot == null
        ? string.Empty
        : $"{BaselineSnapshot.KeyCount:N0} keys ({BaselineSnapshot.Duration.TotalSeconds:F1}s)";

    public string TargetBadgeText => TargetSnapshot == null
        ? string.Empty
        : $"{TargetSnapshot.KeyCount:N0} keys ({TargetSnapshot.Duration.TotalSeconds:F1}s)";

    public int FilteredCount => FilteredKeys.Count;
    public bool HasFilteredResults => FilteredKeys.Count > 0;
    public bool HasNoFilteredResults => HasDiff && FilteredKeys.Count == 0;

    public string BaselineSummary => BaselineSnapshot == null
        ? "No baseline snapshot"
        : $"Captured {BaselineSnapshot.KeyCount:N0} keys in {BaselineSnapshot.Duration.TotalSeconds:F1}s";

    public string TargetSummary => TargetSnapshot == null
        ? "No target snapshot"
        : $"Captured {TargetSnapshot.KeyCount:N0} keys in {TargetSnapshot.Duration.TotalSeconds:F1}s";

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ScanProgressText
    {
        get => _scanProgressText;
        set => SetProperty(ref _scanProgressText, value);
    }

    public string ElapsedActionTime
    {
        get => _elapsedActionTime;
        set => SetProperty(ref _elapsedActionTime, value);
    }

    public bool IsActionPhaseActive
    {
        get => _isActionPhaseActive;
        set => SetProperty(ref _isActionPhaseActive, value);
    }

    public bool HideNoise
    {
        get => _hideNoise;
        set
        {
            if (SetProperty(ref _hideNoise, value))
            {
                UpdateFilteredList();
            }
        }
    }

    public bool PersistenceOnly
    {
        get => _persistenceOnly;
        set
        {
            if (SetProperty(ref _persistenceOnly, value))
            {
                UpdateFilteredList();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearchText));
                UpdateFilteredList();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public DiffChangeType? SelectedChangeType
    {
        get => _selectedChangeType;
        set
        {
            if (SetProperty(ref _selectedChangeType, value))
            {
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterAdded));
                OnPropertyChanged(nameof(IsFilterModified));
                OnPropertyChanged(nameof(IsFilterDeleted));
                UpdateFilteredList();
            }
        }
    }

    public bool IsFilterAll => !SelectedChangeType.HasValue;
    public bool IsFilterAdded => SelectedChangeType == DiffChangeType.Added;
    public bool IsFilterModified => SelectedChangeType == DiffChangeType.Modified;
    public bool IsFilterDeleted => SelectedChangeType == DiffChangeType.Deleted;

    // Statistics properties
    public int TotalChanges => DiffReport?.TotalKeysChanged ?? 0;
    public int AddedKeysCount => DiffReport?.AddedKeysCount ?? 0;
    public int DeletedKeysCount => DiffReport?.DeletedKeysCount ?? 0;
    public int ModifiedKeysCount => DiffReport?.ModifiedKeysCount ?? 0;
    public int TotalValuesChanged => DiffReport?.TotalValuesChanged ?? 0;
    public int PersistenceRisksCount => DiffReport?.PersistenceRisksCount ?? 0;
    public int NoiseKeysCount => DiffReport?.NoiseKeysCount ?? 0;
    public int TotalThreatsCount => ThreatItems.Count;
    public int CriticalThreatsCount => ThreatItems.Count(t => t.Severity == "CRITICAL" || t.Severity == "HIGH");

    // Command-line runtime (wrsd-cli.exe is framework-dependent)
    private bool _isCliRuntimeMissing;
    private bool _isInstallingCliRuntime;
    public bool IsCliRuntimeMissing
    {
        get => _isCliRuntimeMissing;
        private set { if (SetProperty(ref _isCliRuntimeMissing, value)) { OnPropertyChanged(nameof(CliRuntimeStatus)); CommandManager.InvalidateRequerySuggested(); } }
    }
    public bool IsInstallingCliRuntime
    {
        get => _isInstallingCliRuntime;
        private set { if (SetProperty(ref _isInstallingCliRuntime, value)) CommandManager.InvalidateRequerySuggested(); }
    }
    public string CliRuntimeStatus => IsCliRuntimeMissing
        ? "wrsd-cli.exe needs the .NET 10 runtime (about 55 MB, installed once). WRSD itself does not need it."
        : "Ready: the .NET 10 runtime is installed, so wrsd-cli.exe can run.";
    public ICommand InstallCliRuntimeCommand { get; private set; } = null!;

    private async Task InstallCliRuntimeAsync()
    {
        var answer = System.Windows.MessageBox.Show(
            "Install the .NET 10 runtime (about 55 MB) with the Windows Package Manager?\n\nA window shows the progress and Windows may ask for permission. If the Package Manager is not available, the download page opens instead.",
            "Install .NET 10 runtime", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (answer != System.Windows.MessageBoxResult.Yes) return;
        IsInstallingCliRuntime = true;
        try
        {
            IsCliRuntimeMissing = !await CommandLineRuntime.InstallAsync();
            StatusMessage = IsCliRuntimeMissing ? "The .NET 10 runtime is still missing." : "The .NET 10 runtime is installed.";
        }
        finally { IsInstallingCliRuntime = false; }
    }

    public const string RepositoryUrl = "https://github.com/md-anamul-hasan/wrsd";

    // Commands
    public ICommand SwitchNavTabCommand { get; }
    public ICommand ToggleSideBySideViewCommand { get; }

    public ICommand TakeBaselineCommand { get; }
    public ICommand LoadBaselineCommand { get; }
    public ICommand SaveBaselineCommand { get; }
    public ICommand TakeTargetAndDiffCommand { get; }
    public ICommand LoadTargetAndDiffCommand { get; }
    public ICommand SaveTargetCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand LaunchInstallerCommand { get; }
    public ICommand ResetCommand { get; }

    public ICommand ExportUndoRegCommand { get; }
    public ICommand ExportRedoRegCommand { get; }
    public ICommand ExportHtmlCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportThreatRollbackRegCommand { get; }

    public ICommand RefreshVaultCommand { get; }
    public ICommand CompareSelectedVaultSnapshotsCommand { get; }
    public ICommand DeleteVaultSnapshotCommand { get; }
    public ICommand ImportSnapshotToVaultCommand { get; }

    public ICommand ClearSearchCommand { get; }
    public ICommand CopyValueNameCommand { get; }
    public ICommand CopyBeforeValueCommand { get; }
    public ICommand CopyAfterValueCommand { get; }
    public ICommand CopyFullRowCommand { get; }
    public ICommand CopyKeyPathCommand { get; }
    public ICommand OpenInRegeditCommand { get; }

    public ICommand OpenRepositoryCommand { get; }

    public ICommand SetFilterAllCommand { get; }
    public ICommand SetFilterAddedCommand { get; }
    public ICommand SetFilterModifiedCommand { get; }
    public ICommand SetFilterDeletedCommand { get; }

    public ICommand ToggleThemeCommand { get; }
    public ICommand SetThemeSystemCommand { get; }
    public ICommand SetThemeDarkCommand { get; }
    public ICommand SetThemeLightCommand { get; }

    public AppThemeMode CurrentTheme => ThemeManager.CurrentMode;
    public bool IsThemeSystem => CurrentTheme == AppThemeMode.System;
    public bool IsThemeDark => CurrentTheme == AppThemeMode.Dark;
    public bool IsThemeLight => CurrentTheme == AppThemeMode.Light;

    public string ThemeLabelText => ThemeManager.CurrentMode switch
    {
        AppThemeMode.System => "System",
        AppThemeMode.Dark => "Dark",
        AppThemeMode.Light => "Light",
        _ => "System"
    };

    public string ThemeTooltipText => ThemeManager.CurrentMode switch
    {
        AppThemeMode.System => $"Theme: System Default ({(ThemeManager.IsDarkThemeActive ? "Dark" : "Light")}) — Click to change",
        AppThemeMode.Dark => "Theme: Dark — Click to switch to Light",
        AppThemeMode.Light => "Theme: Light — Click to switch to System Default",
        _ => "Toggle theme"
    };

    public Geometry? ThemeIconGeometry => ThemeManager.CurrentMode switch
    {
        AppThemeMode.System => Application.Current?.TryFindResource("GeomMonitor") as Geometry,
        AppThemeMode.Dark => Application.Current?.TryFindResource("GeomMoon") as Geometry,
        AppThemeMode.Light => Application.Current?.TryFindResource("GeomSun") as Geometry,
        _ => Application.Current?.TryFindResource("GeomMonitor") as Geometry
    };

    // Methods
    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* no browser */ }
    }

    private async Task TakeBaselineAsync()
    {
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        StatusMessage = $"Capturing Baseline Snapshot ({SelectedPreset})...";

        var progress = new Progress<RegistryScanProgress>(p =>
        {
            ScanProgressText = $"Keys: {p.KeysScanned:N0} | Values: {p.ValuesScanned:N0} ({p.Elapsed.TotalSeconds:F1}s)";
        });

        try
        {
            var snapshot = await _scanner.TakeSnapshotAsync(SelectedPreset, progress: progress, cancellationToken: _scanCts.Token);
            snapshot.Name = $"Baseline ({DateTime.Now:HH:mm:ss})";
            BaselineSnapshot = snapshot;
            StatusMessage = $"Baseline captured: {snapshot.KeyCount:N0} keys in {snapshot.Duration.TotalSeconds:F1}s.";

            // Save to Vault automatically for convenience
            try
            {
                string vaultFile = Path.Combine(VaultDirectory, $"baseline_{DateTime.Now:yyyyMMdd_HHmmss}.wrsd");
                await SnapshotSerializer.SaveToFileAsync(snapshot, vaultFile);
                RefreshVaultSnapshots();
            }
            catch
            {
            }

            // Start action timer
            _actionStopwatch = Stopwatch.StartNew();
            _actionTimer.Start();
            IsActionPhaseActive = true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Baseline scan cancelled by user.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to capture baseline: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Baseline capture failed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task TakeTargetAndDiffAsync()
    {
        if (BaselineSnapshot == null) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        StatusMessage = $"Capturing Target Snapshot ({SelectedPreset})...";

        var progress = new Progress<RegistryScanProgress>(p =>
        {
            ScanProgressText = $"Keys: {p.KeysScanned:N0} | Values: {p.ValuesScanned:N0} ({p.Elapsed.TotalSeconds:F1}s)";
        });

        try
        {
            var target = await _scanner.TakeSnapshotAsync(SelectedPreset, progress: progress, cancellationToken: _scanCts.Token);
            target.Name = $"Target ({DateTime.Now:HH:mm:ss})";
            TargetSnapshot = target;

            // Stop action timer
            if (_actionStopwatch != null)
            {
                _actionStopwatch.Stop();
                _actionTimer.Stop();
            }

            StatusMessage = "Calculating snapshot differences...";
            DiffReport = SnapshotDiffEngine.Compare(BaselineSnapshot, target);
            StatusMessage = $"Diff complete: {DiffReport.TotalKeysChanged:N0} keys changed in {DiffReport.CalculationTime.TotalMilliseconds:F0}ms.";

            // Save target to Vault automatically
            try
            {
                string vaultFile = Path.Combine(VaultDirectory, $"target_{DateTime.Now:yyyyMMdd_HHmmss}.wrsd");
                await SnapshotSerializer.SaveToFileAsync(target, vaultFile);
                RefreshVaultSnapshots();
            }
            catch
            {
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Target scan cancelled by user.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to capture target & diff: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Target capture failed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task LoadBaselineAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Load Baseline Snapshot",
            Filter = "WRSD Snapshot (*.wrsd)|*.wrsd|All Files (*.*)|*.*",
            InitialDirectory = VaultDirectory
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = $"Loading baseline snapshot from {Path.GetFileName(ofd.FileName)}...";
                IsScanning = true;
                BaselineSnapshot = await SnapshotSerializer.LoadFromFileAsync(ofd.FileName);
                StatusMessage = $"Loaded baseline: {BaselineSnapshot.KeyCount:N0} keys.";

                if (TargetSnapshot != null)
                {
                    DiffReport = SnapshotDiffEngine.Compare(BaselineSnapshot, TargetSnapshot);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load baseline: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Failed to load baseline.";
            }
            finally
            {
                IsScanning = false;
            }
        }
    }

    private async Task LoadTargetAndDiffAsync()
    {
        if (BaselineSnapshot == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "Load Target Snapshot",
            Filter = "WRSD Snapshot (*.wrsd)|*.wrsd|All Files (*.*)|*.*",
            InitialDirectory = VaultDirectory
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = $"Loading target snapshot from {Path.GetFileName(ofd.FileName)}...";
                IsScanning = true;
                TargetSnapshot = await SnapshotSerializer.LoadFromFileAsync(ofd.FileName);
                DiffReport = SnapshotDiffEngine.Compare(BaselineSnapshot, TargetSnapshot);
                StatusMessage = $"Loaded target & calculated diff: {DiffReport.TotalKeysChanged:N0} keys changed.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load target: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Failed to load target.";
            }
            finally
            {
                IsScanning = false;
            }
        }
    }

    private async Task SaveBaselineAsync()
    {
        if (BaselineSnapshot == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Save Baseline Snapshot",
            Filter = "WRSD Snapshot (*.wrsd)|*.wrsd",
            FileName = $"baseline_{DateTime.Now:yyyyMMdd_HHmmss}.wrsd",
            InitialDirectory = VaultDirectory
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await SnapshotSerializer.SaveToFileAsync(BaselineSnapshot, sfd.FileName);
                StatusMessage = $"Saved baseline snapshot to {Path.GetFileName(sfd.FileName)}";
                RefreshVaultSnapshots();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save baseline: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task SaveTargetAsync()
    {
        if (TargetSnapshot == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Save Target Snapshot",
            Filter = "WRSD Snapshot (*.wrsd)|*.wrsd",
            FileName = $"target_{DateTime.Now:yyyyMMdd_HHmmss}.wrsd",
            InitialDirectory = VaultDirectory
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await SnapshotSerializer.SaveToFileAsync(TargetSnapshot, sfd.FileName);
                StatusMessage = $"Saved target snapshot to {Path.GetFileName(sfd.FileName)}";
                RefreshVaultSnapshots();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save target: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CancelScan()
    {
        _scanCts?.Cancel();
    }

    private void LaunchInstaller()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Application / Installer to Track",
            Filter = "Installers & Executables (*.exe;*.msi;*.bat)|*.exe;*.msi;*.bat|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ofd.FileName) { UseShellExecute = true });
                StatusMessage = $"Launched installer: {Path.GetFileName(ofd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ResetWorkflow()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;

        _actionStopwatch?.Stop();
        _actionTimer.Stop();

        BaselineSnapshot = null;
        TargetSnapshot = null;
        DiffReport = null;

        ElapsedActionTime = "00:00:00";
        IsActionPhaseActive = false;
        ScanProgressText = string.Empty;
        StatusMessage = "Ready to take baseline snapshot";
        SelectedKey = null;
    }

    private async Task ExportUndoRegAsync()
    {
        if (DiffReport == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Rollback / Undo .reg Script",
            Filter = "Registry Script (*.reg)|*.reg",
            FileName = $"wrsd_rollback_{DateTime.Now:yyyyMMdd_HHmmss}.reg"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string regContent = RegFileExporter.GenerateUndoReg(DiffReport);
                await File.WriteAllTextAsync(sfd.FileName, regContent, System.Text.Encoding.Unicode);
                StatusMessage = $"Rollback script exported to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export rollback script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportRedoRegAsync()
    {
        if (DiffReport == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Apply / Redo .reg Script",
            Filter = "Registry Script (*.reg)|*.reg",
            FileName = $"wrsd_apply_{DateTime.Now:yyyyMMdd_HHmmss}.reg"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string regContent = RegFileExporter.GenerateRedoReg(DiffReport);
                await File.WriteAllTextAsync(sfd.FileName, regContent, System.Text.Encoding.Unicode);
                StatusMessage = $"Apply script exported to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export apply script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportHtmlAsync()
    {
        if (DiffReport == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Interactive HTML Report",
            Filter = "HTML Document (*.html)|*.html",
            FileName = $"wrsd_report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string html = HtmlReportExporter.GenerateHtml(DiffReport);
                await File.WriteAllTextAsync(sfd.FileName, html, System.Text.Encoding.UTF8);
                StatusMessage = $"HTML report exported to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export HTML report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportJsonAsync()
    {
        if (DiffReport == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Machine-Readable JSON Diff",
            Filter = "JSON Document (*.json)|*.json",
            FileName = $"wrsd_diff_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string json = JsonExporter.ExportReport(DiffReport);
                await File.WriteAllTextAsync(sfd.FileName, json, System.Text.Encoding.UTF8);
                StatusMessage = $"JSON diff exported to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export JSON report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportCsvAsync()
    {
        if (DiffReport == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export SIEM / CSV Diff Report",
            Filter = "CSV Spreadsheet (*.csv)|*.csv",
            FileName = $"wrsd_diff_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string csv = CsvDiffExporter.ExportToCsv(DiffReport);
                await File.WriteAllTextAsync(sfd.FileName, csv, System.Text.Encoding.UTF8);
                StatusMessage = $"CSV report exported to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportThreatRollbackRegAsync()
    {
        if (DiffReport == null || ThreatItems.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Threat Neutralization .reg File",
            Filter = "Registry Script (*.reg)|*.reg",
            FileName = $"wrsd_threat_remediation_{DateTime.Now:yyyyMMdd_HHmmss}.reg"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var persistenceKeys = DiffReport.AllKeyDiffs.Where(k => k.IsPersistenceRisk).ToList();
                var threatReport = new SnapshotDiffReport
                {
                    Baseline = DiffReport.Baseline,
                    Target = DiffReport.Target,
                    AllKeyDiffs = persistenceKeys
                };

                string regContent = RegFileExporter.GenerateUndoReg(threatReport);
                await File.WriteAllTextAsync(sfd.FileName, regContent, System.Text.Encoding.Unicode);
                StatusMessage = $"Threat remediation script saved to {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export remediation script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Vault Implementation
    private string VaultDirectory
    {
        get
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WRSD", "Snapshots");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }
    }

    private void RefreshVaultSnapshots()
    {
        VaultSnapshots.Clear();
        try
        {
            var files = Directory.GetFiles(VaultDirectory, "*.wrsd");
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                VaultSnapshots.Add(new SnapshotVaultItemViewModel
                {
                    FilePath = f,
                    FileName = fi.Name,
                    CreatedAt = fi.LastWriteTime,
                    FileSizeBytes = $"{fi.Length / 1024.0:F1} KB",
                    ScopePreset = "Saved Snapshot"
                });
            }
        }
        catch
        {
        }
    }

    private async Task CompareVaultSnapshotsAsync()
    {
        if (SelectedVaultSnapshotA == null || SelectedVaultSnapshotB == null)
        {
            MessageBox.Show("Please select both Snapshot A and Snapshot B from the vault to compare.", "Compare Snapshots", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusMessage = "Loading snapshots from vault...";
            IsScanning = true;
            var snapA = await SnapshotSerializer.LoadFromFileAsync(SelectedVaultSnapshotA.FilePath);
            var snapB = await SnapshotSerializer.LoadFromFileAsync(SelectedVaultSnapshotB.FilePath);

            BaselineSnapshot = snapA;
            TargetSnapshot = snapB;
            DiffReport = SnapshotDiffEngine.Compare(snapA, snapB);
            StatusMessage = $"Compared {SelectedVaultSnapshotA.FileName} vs {SelectedVaultSnapshotB.FileName}: {DiffReport.TotalKeysChanged} keys changed.";
            CurrentNavTab = EnterpriseNavTab.DiffInspector;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to compare vault snapshots: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task ImportSnapshotToVaultAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Import Snapshot to Vault",
            Filter = "WRSD Snapshot (*.wrsd)|*.wrsd|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                string dest = Path.Combine(VaultDirectory, Path.GetFileName(ofd.FileName));
                if (File.Exists(dest))
                {
                    dest = Path.Combine(VaultDirectory, $"{Path.GetFileNameWithoutExtension(dest)}_{Guid.NewGuid():N[..6]}.wrsd");
                }
                File.Copy(ofd.FileName, dest, true);
                RefreshVaultSnapshots();
                StatusMessage = $"Imported snapshot to vault: {Path.GetFileName(dest)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void DeleteSelectedVaultSnapshot(object? parameter)
    {
        if (parameter is SnapshotVaultItemViewModel item)
        {
            if (MessageBox.Show($"Are you sure you want to delete '{item.FileName}' from the vault?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                    RefreshVaultSnapshots();
                    StatusMessage = $"Deleted {item.FileName} from vault.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void CopyKeyPath()
    {
        if (SelectedKey != null)
        {
            Clipboard.SetText(SelectedKey.KeyPath);
            StatusMessage = "Copied key path to clipboard";
        }
    }

    private void OpenInRegedit()
    {
        if (SelectedKey == null) return;

        try
        {
            // Set LastKey in regedit so it opens directly to this key
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
            key?.SetValue("LastKey", SelectedKey.KeyPath);

            Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
            StatusMessage = $"Opened {SelectedKey.KeyPath} in Regedit";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch Regedit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RebuildKeyViewModels()
    {
        _allKeyViewModels.Clear();
        ThreatItems.Clear();
        if (_diffReport == null) return;

        foreach (var keyDiff in _diffReport.AllKeyDiffs)
        {
            var keyVm = new KeyDiffViewModel(keyDiff);
            _allKeyViewModels.Add(keyVm);

            if (keyDiff.IsPersistenceRisk)
            {
                var severity = ThreatItemViewModel.ResolveSeverity(keyDiff.RiskCategory);
                var threat = new ThreatItemViewModel
                {
                    KeyPath = keyDiff.KeyPath,
                    RiskCategory = keyDiff.RiskCategory ?? "Persistence Hook",
                    MitreAttackId = ThreatItemViewModel.ResolveMitreId(keyDiff.RiskCategory),
                    Severity = severity,
                    SeverityColor = ThreatItemViewModel.ResolveSeverityColor(severity),
                    ChangeType = keyDiff.ChangeType
                };
                foreach (var v in keyVm.Values)
                {
                    threat.Values.Add(v);
                }
                ThreatItems.Add(threat);
            }
        }

        OnPropertyChanged(nameof(TotalThreatsCount));
        OnPropertyChanged(nameof(CriticalThreatsCount));
    }

    private void UpdateFilteredList()
    {
        FilteredKeys.Clear();
        if (DiffReport == null || _allKeyViewModels.Count == 0)
        {
            SelectedKey = null;
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(HasFilteredResults));
            OnPropertyChanged(nameof(HasNoFilteredResults));
            return;
        }

        string search = SearchText.Trim();
        bool hasSearch = !string.IsNullOrEmpty(search);

        foreach (var vm in _allKeyViewModels)
        {
            if (HideNoise && vm.IsNoise) continue;
            if (PersistenceOnly && !vm.IsPersistenceRisk) continue;
            if (SelectedChangeType.HasValue && vm.ChangeType != SelectedChangeType.Value) continue;

            if (hasSearch)
            {
                bool match = vm.KeyPath.Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!match)
                {
                    match = vm.Source.ValueDiffs.Any(v =>
                        v.ValueName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        v.BeforeDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        v.AfterDisplay.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                if (!match) continue;
            }

            FilteredKeys.Add(vm);
        }

        if (FilteredKeys.Count > 0 && (SelectedKey == null || !FilteredKeys.Contains(SelectedKey)))
        {
            SelectedKey = FilteredKeys[0];
        }
        else if (FilteredKeys.Count == 0)
        {
            SelectedKey = null;
        }

        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(HasFilteredResults));
        OnPropertyChanged(nameof(HasNoFilteredResults));
    }
}

/// <summary>One selectable scan preset on the Settings page.</summary>
public sealed class PresetOption : ObservableObject
{
    private readonly Action<SnapshotScopePreset> _select;
    private bool _isSelected;

    public PresetOption(SnapshotScopePreset preset, string title, string description, Action<SnapshotScopePreset> select)
    {
        Preset = preset;
        Title = title;
        Description = description;
        _select = select;
    }

    public SnapshotScopePreset Preset { get; }
    public string Title { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value) _select(Preset);
        }
    }

    public void Sync(SnapshotScopePreset selected)
    {
        SetProperty(ref _isSelected, selected == Preset, nameof(IsSelected));
    }
}
