using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ServiceBench.App.Models;
using ServiceBench.App.Services;
using ServiceBench.App.Utilities;
using ServiceBench.App.Views;

namespace ServiceBench.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly Config _config = new();
    private readonly ProcessRunner _processRunner = new();
    private readonly AidaController _aida;
    private readonly OcctController _occt;
    private readonly FurMarkController _furmark;
    private readonly HwTelemetryService _hwTelemetry;
    private readonly ScreenshotService _screenshotService = new();
    private readonly DeviceDetector _deviceDetector = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly ReportGenerator _reportGenerator;

    private readonly ObservableCollection<string> _furmarkPresets = new(new[]
    {
        "1280x720", "1366x768", "1600x900", "1920x1080", "2560x1080", "2560x1440", "3440x1440", "3840x2160"
    });

    private CancellationTokenSource? _runCts;
    private DeviceInfo? _device;
    private RunJson? _currentRun;
    private string? _runFolder;
    private string? _csvPath;
    private readonly List<RunJsonTelemetrySample> _samples = new();
    private readonly object _lock = new();
    private bool _isStopping;
    private System.Timers.Timer? _screenshotTimer;
    private string _currentPhase = "IDLE";
    private string _currentTestName = "IDLE";
    private string _currentPhaseStatus = "READY";
    private RunJsonTelemetrySample? _lastTelemetrySample;
    private HwSample? _lastHwSample;

    private string _statusMessage = "Готово";
    private string _logText = string.Empty;
    private string _runTag = "before";
    private string _customRunTag = string.Empty;
    private bool _isCustomTag;
    private string? _stopStatus;

    private string? _liveCpuTemp;
    private string? _liveGpuTemp;
    private string? _liveCpuFreq;
    private string? _liveGpuCore;
    private string? _liveGpuMem;
    private string? _liveCpuFan;
    private string? _liveGpuFan;
    private string? _liveGpuFanPct;
    private bool _fanSummaryLogged;
    private readonly ObservableCollection<MetricRow> _basicMetrics = new();
    private readonly ObservableCollection<MetricRow> _cpuMetrics = new();
    private readonly ObservableCollection<MetricRow> _gpuMetrics = new();
    private readonly ObservableCollection<MetricRow> _ramMetrics = new();
    private bool _showCpuTab;
    private bool _showGpuTab;
    private bool _showRamTab;

    public MainViewModel()
    {
        _config.Initialize(_settingsService);
        _reportGenerator = new ReportGenerator(_config);
        Plan = new TestPlan();
        _aida = new AidaController(_config, _processRunner);
        _occt = new OcctController(_config, _processRunner);
        _furmark = new FurMarkController(_config, _processRunner);
        _hwTelemetry = new HwTelemetryService();
        _hwTelemetry.OnSample += OnHwSample;
        _hwTelemetry.Start(1);
        _hotkeyService.EscPressed += (_, _) => StopByUser();

        StartCommand = new RelayCommand(async () => await StartAsync(), () => _runCts == null);
        StopCommand = new RelayCommand(StopByUser, () => _runCts != null);
        OpenDeviceFolderCommand = new RelayCommand(OpenDeviceFolder);
        OpenDashboardCommand = new RelayCommand(OpenDashboard);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
    }

    public TestPlan Plan { get; }
    public ObservableCollection<string> FurmarkPresets => _furmarkPresets;
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand OpenDeviceFolderCommand { get; }
    public RelayCommand OpenDashboardCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetField(ref _statusMessage, value))
            {
                RaisePropertyChanged(nameof(Status));
            }
        }
    }

    public string Status => StatusMessage;

    public string LogText
    {
        get => _logText;
        set => SetField(ref _logText, value);
    }

    public string RunTag
    {
        get => _runTag;
        set
        {
            if (SetField(ref _runTag, value))
            {
                IsCustomTag = value == "custom";
                if (!IsCustomTag)
                {
                    CustomRunTag = string.Empty;
                }
            }
        }
    }

    public string CustomRunTag
    {
        get => _customRunTag;
        set => SetField(ref _customRunTag, value);
    }

    public bool IsCustomTag
    {
        get => _isCustomTag;
        set => SetField(ref _isCustomTag, value);
    }

    public string? LiveCpuTemp
    {
        get => _liveCpuTemp;
        set => SetField(ref _liveCpuTemp, value);
    }

    public string? LiveGpuTemp
    {
        get => _liveGpuTemp;
        set => SetField(ref _liveGpuTemp, value);
    }

    public string? LiveCpuFreq
    {
        get => _liveCpuFreq;
        set => SetField(ref _liveCpuFreq, value);
    }

    public string? LiveGpuCore
    {
        get => _liveGpuCore;
        set => SetField(ref _liveGpuCore, value);
    }

    public string? LiveGpuMem
    {
        get => _liveGpuMem;
        set => SetField(ref _liveGpuMem, value);
    }

    public string? LiveCpuFan
    {
        get => _liveCpuFan;
        set => SetField(ref _liveCpuFan, value);
    }

    public string? LiveGpuFan
    {
        get => _liveGpuFan;
        set => SetField(ref _liveGpuFan, value);
    }

    public string? LiveGpuFanPct
    {
        get => _liveGpuFanPct;
        set => SetField(ref _liveGpuFanPct, value);
    }

    public ObservableCollection<MetricRow> BasicMetrics => _basicMetrics;

    public ObservableCollection<MetricRow> CpuMetrics => _cpuMetrics;

    public ObservableCollection<MetricRow> GpuMetrics => _gpuMetrics;

    public ObservableCollection<MetricRow> RamMetrics => _ramMetrics;

    public bool ShowCpuTab
    {
        get => _showCpuTab;
        set => SetField(ref _showCpuTab, value);
    }

    public bool ShowGpuTab
    {
        get => _showGpuTab;
        set => SetField(ref _showGpuTab, value);
    }

    public bool ShowRamTab
    {
        get => _showRamTab;
        set => SetField(ref _showRamTab, value);
    }

    public void AttachHotkey(Window window) => _hotkeyService.Register(window);

    public void DetachHotkey()
    {
        _hotkeyService.Unregister();
        _hwTelemetry.OnSample -= OnHwSample;
        _hwTelemetry.Stop();
        _hwTelemetry.Dispose();
    }

    private async Task StartAsync()
    {
        if (_runCts != null)
        {
            return;
        }

        try
        {
            _runCts = new CancellationTokenSource();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            _stopStatus = null;
            _isStopping = false;
            _samples.Clear();
            _fanSummaryLogged = false;
            _lastTelemetrySample = null;
            _lastHwSample = null;
            SetPhase("IDLE", "IDLE", "READY");
            StatusMessage = "Запуск тестов...";
            AppendLog("Начало прогона");

            _device = _deviceDetector.Detect();
            var tag = RunTag == "custom" ? (string.IsNullOrWhiteSpace(CustomRunTag) ? "custom" : CustomRunTag) : RunTag;
            tag = SlugHelper.Slugify(tag);
            var startedAt = DateTime.UtcNow;
            _runFolder = FileManager.BuildRunFolder(_device, tag, startedAt);
            _csvPath = Plan.SaveAidaCsv ? Path.Combine(_runFolder, "aida_sensors.csv") : Path.Combine(Path.GetTempPath(), $"servicebench_{Guid.NewGuid():N}.csv");

            _config.SaveAidaCsv = Plan.SaveAidaCsv;
            _config.CompressScreenshots = Plan.CompressScreenshots;

            _currentRun = new RunJson
            {
                Device = new RunJsonDevice
                {
                    Type = _device.Type.ToString(),
                    Model = _device.Model,
                    Cpu = _device.Cpu,
                    Gpu = _device.Gpu
                },
                Session = new RunJsonSession
                {
                    Tag = tag,
                    StartedAt = startedAt,
                    Status = "RUNNING"
                },
                Telemetry = new RunJsonTelemetry
                {
                    Hz = 1,
                    Timeline = new List<RunJsonTelemetrySample>(),
                    Peaks = new RunJsonPeaks()
                },
                Notes = new List<string>(),
                Brand = new RunJsonBrand
                {
                    CompanyName = _config.Branding.CompanyName,
                    Address = _config.Branding.Address,
                    WorkingHours = _config.Branding.WorkingHours
                }
            };

            if (!string.IsNullOrWhiteSpace(_config.Branding.LogoPath))
            {
                _currentRun.Brand.LogoRelPath = _settingsService.TryCopyLogoToAssets(_device, _config.Branding.LogoPath);
            }

            var tests = new List<RunJsonTest>();
            if (Plan.UseAida)
            {
                tests.Add(new RunJsonTest { Name = "AIDA64", DurationSec = Plan.AidaDurationMinutes * 60 });
            }
            if (Plan.UseOcct)
            {
                if (Plan.OcctCpuSmall)
                {
                    tests.Add(new RunJsonTest { Name = "OCCT CPU Small", DurationSec = Plan.OcctCpuMinutes * 60 });
                }
                if (Plan.OcctGpu3D)
                {
                    tests.Add(new RunJsonTest { Name = "OCCT GPU 3D", DurationSec = Plan.OcctGpuMinutes * 60 });
                }
                if (Plan.OcctVram)
                {
                    tests.Add(new RunJsonTest { Name = "OCCT VRAM", DurationSec = Plan.OcctVramMinutes * 60 });
                }
            }
            if (Plan.UseFurmark)
            {
                tests.Add(new RunJsonTest { Name = "FurMark", DurationSec = Plan.FurmarkMinutes * 60 });
            }
            _currentRun.Tests = tests;

            _reportGenerator.EnsureAssets(_device);
            StartScreenshotTimer();
            CaptureScreenshot("0000_sensors.png");

            await ExecutePlanAsync(_runCts.Token);
        }
        catch (Exception ex)
        {
            FinishRun("ERROR", ex.Message);
            MessageBox.Show($"Произошла ошибка: {ex.Message}", "ServiceBench", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _runCts = null;
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ExecutePlanAsync(CancellationToken token)
    {
        try
        {
            if (_currentRun == null || _device == null)
            {
                return;
            }

            if (Plan.UseAida)
            {
                await UpdatePhaseAsync("AIDA", "AIDA64", "RUNNING");
                AppendLog("Запуск AIDA64");
                try
                {
                    _aida.StartSst(Plan, _csvPath);
                    await WaitWithCancellation(TimeSpan.FromMinutes(Math.Max(1, Plan.AidaDurationMinutes)), token);
                }
                finally
                {
                    await _aida.StopAsync();
                }
                await UpdatePhaseAsync("AIDA", "AIDA64", "COMPLETED");
                CaptureScreenshot("final_sensors.png");
                await UpdatePhaseAsync("IDLE", "IDLE", "WAITING");
            }

            if (token.IsCancellationRequested)
            {
                CompleteRunAndNotify(_stopStatus ?? "STOP_BY_USER");
                return;
            }

            if (Plan.UseOcct)
            {
                AppendLog("Запуск OCCT профилей");
                try
                {
                    await _occt.RunAsync(
                        Plan,
                        UpdatePhaseAsync,
                        AppendLog,
                        ShowOcctPromptAsync,
                        token);
                }
                finally
                {
                    await _occt.StopAsync();
                }
                CaptureScreenshot($"occt_final_{DateTime.Now:HHmmss}.png");
                await UpdatePhaseAsync("IDLE", "IDLE", "WAITING");
            }

            if (token.IsCancellationRequested)
            {
                CompleteRunAndNotify(_stopStatus ?? "STOP_BY_USER");
                return;
            }

            if (Plan.UseFurmark)
            {
                await UpdatePhaseAsync("FURMARK", "FurMark", "RUNNING");
                AppendLog("Запуск FurMark");
                try
                {
                    await _furmark.StartAsync(Plan);
                    AddRunNote(_furmark.SwitchLog);
                    AddRunNote(_furmark.LastArgumentLog);
                    if (!string.IsNullOrWhiteSpace(_furmark.SwitchLog))
                    {
                        AppendLog(_furmark.SwitchLog!);
                    }
                    if (!string.IsNullOrWhiteSpace(_furmark.LastArgumentLog))
                    {
                        AppendLog(_furmark.LastArgumentLog!);
                    }
                    AppendLog("FurMark GUI запущен");
                    await WaitWithCancellation(TimeSpan.FromMinutes(Math.Max(1, Plan.FurmarkMinutes)), token);
                }
                finally
                {
                    await _furmark.StopAsync();
                    AddRunNote(_furmark.LastStopLog);
                    if (!string.IsNullOrWhiteSpace(_furmark.LastStopLog))
                    {
                        AppendLog(_furmark.LastStopLog!);
                    }
                    AddRunNote(FormatFurmarkOutputNote());
                }
                CaptureScreenshot($"furmark_final_{DateTime.Now:HHmmss}.png");
                await UpdatePhaseAsync("FURMARK", "FurMark", "COMPLETED");
                await UpdatePhaseAsync("IDLE", "IDLE", "WAITING");
            }

            CompleteRunAndNotify(_stopStatus ?? "OK");
        }
        finally
        {
            StopScreenshotTimer();
        }
    }

    private async Task WaitWithCancellation(TimeSpan duration, CancellationToken token)
    {
        var elapsed = TimeSpan.Zero;
        var step = TimeSpan.FromSeconds(1);
        while (elapsed < duration && !token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(step, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            elapsed += step;
        }
    }

    private void CompleteRunAndNotify(string status)
    {
        string reason = status switch
        {
            "STOP_BY_OVERHEAT" => "Тест завершён в связи с перегревом",
            "STOP_BY_USER" => "Тест успешно остановлен",
            "OK" => string.Empty,
            _ => "Тест прерван"
        };

        CaptureScreenshot($"final_{DateTime.Now:HHmmss}.png");
        SetPhase("IDLE", "IDLE", "COMPLETE");
        FinishRun(status, reason);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var (cpuTemp, gpuTemp) = GetLatestTemperatures();
            var (message, icon) = status switch
            {
                "STOP_BY_OVERHEAT" => ("Тест завершён в связи с перегревом", MessageBoxImage.Error),
                "STOP_BY_USER" => ("Тест успешно остановлен", MessageBoxImage.Information),
                "OK" => ("Все тесты пройдены", MessageBoxImage.Information),
                _ => ("Произошла ошибка", MessageBoxImage.Error)
            };

            var details = $"CPU: {cpuTemp}\nGPU: {gpuTemp}";
            MessageBox.Show($"{message}\n{details}", "ServiceBench", MessageBoxButton.OK, icon);
        });
    }

    private void FinishRun(string status, string reason)
    {
        if (_currentRun == null || _device == null || _runFolder == null)
        {
            return;
        }

        if (!_fanSummaryLogged)
        {
            AddRunNote("Fans: CPU —, GPU RPM —, GPU % —");
            _fanSummaryLogged = true;
        }

        _currentRun.Session.Status = status;
        _currentRun.Session.Reason = reason;
        _currentRun.Session.EndedAt = DateTime.UtcNow;
        _currentRun.Telemetry.Timeline = _samples.ToList();
        CalculatePeaks();
        _reportGenerator.GenerateRunReport(_device, _currentRun, _runFolder);
        _reportGenerator.GenerateIndex(_device);
        if (!Plan.SaveAidaCsv && _csvPath != null && File.Exists(_csvPath))
        {
            File.Delete(_csvPath);
        }

        StatusMessage = status switch
        {
            "OK" => "Тесты завершены",
            "STOP_BY_OVERHEAT" => "Остановлено по перегреву",
            "STOP_BY_USER" => "Остановлено пользователем",
            _ => "Ошибка"
        };

        _currentRun = null;
        _runFolder = null;
        _csvPath = null;
        _samples.Clear();
    }

    private void CalculatePeaks()
    {
        if (_currentRun == null)
        {
            return;
        }

        if (_samples.Count == 0)
        {
            return;
        }

        double MaxOrDefault(Func<RunJsonTelemetrySample, double?> selector)
            => _samples.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();

        double AvgOrDefault(Func<RunJsonTelemetrySample, double?> selector)
        {
            var values = _samples.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            return values.Length == 0 ? 0 : values.Average();
        }

        _currentRun.Telemetry.Peaks.CpuTempMax = MaxOrDefault(s => s.CpuT);
        _currentRun.Telemetry.Peaks.GpuTempMax = MaxOrDefault(s => s.GpuT);
        _currentRun.Telemetry.Peaks.CpuFanMax = MaxOrDefault(s => s.CpuRpm);
        _currentRun.Telemetry.Peaks.GpuFanMax = MaxOrDefault(s => s.GpuRpm);
        _currentRun.Telemetry.Peaks.GpuFanPctMax = MaxOrDefault(s => s.GpuFanPct);
        _currentRun.Telemetry.Peaks.CpuFreqAvg = AvgOrDefault(s => s.CpuMHz);
        _currentRun.Telemetry.Peaks.GpuCoreAvg = AvgOrDefault(s => s.GpuCore);
        _currentRun.Telemetry.Peaks.GpuMemAvg = AvgOrDefault(s => s.GpuMem);
    }

    private (string Cpu, string Gpu) GetLatestTemperatures()
    {
        double? cpu = _lastTelemetrySample?.CpuT;
        double? gpu = _lastTelemetrySample?.GpuT;

        if (!cpu.HasValue || !gpu.HasValue)
        {
            var lastSample = _samples.LastOrDefault();
            cpu ??= lastSample?.CpuT;
            gpu ??= lastSample?.GpuT;
        }

        if (!cpu.HasValue && _lastHwSample != null)
        {
            cpu = _lastHwSample.CpuTemp;
        }

        if (!gpu.HasValue && _lastHwSample != null)
        {
            gpu = _lastHwSample.GpuTemp;
        }

        static string Format(double? value) => value.HasValue ? $"{value.Value:F1} °C" : "нет данных";

        return (Format(cpu), Format(gpu));
    }

    private void AppendLog(string message)
    {
        LogText = new StringBuilder(LogText).AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}").ToString();
    }

    private void OnHwSample(HwSample sample)
    {
        _lastHwSample = sample;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            dispatcher.InvokeAsync(() => UpdateLive(sample));
        }
        else
        {
            UpdateLive(sample);
        }

        TryAutoStopOnLimits(sample);
        AppendRunSample(sample);
    }

    private void UpdateLive(HwSample sample)
    {
        LiveCpuTemp = FormatValue(sample.CpuTemp, "F1");
        LiveGpuTemp = FormatValue(sample.GpuTemp, "F1");
        LiveCpuFreq = FormatValue(sample.CpuMHz, "F0");
        LiveGpuCore = FormatValue(sample.GpuCoreMHz, "F0");
        LiveGpuMem = FormatValue(sample.GpuMemMHz, "F0");
        LiveCpuFan = FormatValue(sample.CpuFanRpm, "F0");
        LiveGpuFan = FormatValue(sample.GpuFanRpm, "F0");
        LiveGpuFanPct = FormatValue(sample.GpuFanPct, "F0");

        var basicUpdates = new List<MetricUpdate>
        {
            new MetricUpdate("basic-cpu-temp", "CPU °C", "°C", sample.CpuTemp),
            new MetricUpdate("basic-cpu-mhz", "CPU MHz", "MHz", sample.CpuMHz),
            new MetricUpdate("basic-gpu-temp", "GPU °C", "°C", sample.GpuTemp),
            new MetricUpdate("basic-gpu-core", "GPU Core MHz", "MHz", sample.GpuCoreMHz),
            new MetricUpdate("basic-gpu-mem", "GPU Mem MHz", "MHz", sample.GpuMemMHz),
            new MetricUpdate("basic-cpu-fan", "CPU Fan", "RPM", sample.CpuFanRpm)
        };
        var gpuFanUnit = sample.GpuFanRpm.HasValue ? "RPM" : sample.GpuFanPct.HasValue ? "%" : "RPM";
        if (!sample.GpuFanRpm.HasValue && !sample.GpuFanPct.HasValue)
        {
            var existingGpuFan = _basicMetrics.FirstOrDefault(r => r.Key == "basic-gpu-fan");
            if (existingGpuFan != null)
            {
                gpuFanUnit = existingGpuFan.Unit;
            }
        }
        var gpuFanValue = sample.GpuFanRpm ?? sample.GpuFanPct;
        basicUpdates.Add(new MetricUpdate("basic-gpu-fan", "GPU Fan", gpuFanUnit, gpuFanValue));

        var cpuUpdates = new List<MetricUpdate>();
        if (sample.CpuTemp.HasValue)
        {
            cpuUpdates.Add(new MetricUpdate("cpu-temp", "Температура CPU", "°C", sample.CpuTemp));
        }
        if (sample.CpuMHz.HasValue)
        {
            cpuUpdates.Add(new MetricUpdate("cpu-mhz-avg", "Частота (средняя)", "MHz", sample.CpuMHz));
        }
        foreach (var kv in sample.CpuCoreMHz.OrderBy(k => k.Key))
        {
            if (kv.Value.HasValue)
            {
                cpuUpdates.Add(new MetricUpdate($"cpu-core-{kv.Key}", $"Core #{kv.Key}", "MHz", kv.Value));
            }
        }

        var gpuUpdates = new List<MetricUpdate>();
        if (sample.GpuTemp.HasValue)
        {
            gpuUpdates.Add(new MetricUpdate("gpu-temp", "Температура GPU", "°C", sample.GpuTemp));
        }
        foreach (var kv in sample.GpuTemps.OrderBy(k => k.Key))
        {
            if (kv.Value.HasValue)
            {
                gpuUpdates.Add(new MetricUpdate($"gpu-temp-{kv.Key}", kv.Key, "°C", kv.Value));
            }
        }
        if (sample.GpuCoreMHz.HasValue)
        {
            gpuUpdates.Add(new MetricUpdate("gpu-core", "Частота ядра", "MHz", sample.GpuCoreMHz));
        }
        if (sample.GpuMemMHz.HasValue)
        {
            gpuUpdates.Add(new MetricUpdate("gpu-mem", "Частота памяти", "MHz", sample.GpuMemMHz));
        }
        foreach (var kv in sample.GpuClocks.OrderBy(k => k.Key))
        {
            if (kv.Value.HasValue && kv.Key is not null)
            {
                gpuUpdates.Add(new MetricUpdate($"gpu-clock-{kv.Key}", kv.Key, "MHz", kv.Value));
            }
        }
        if (sample.GpuFanRpm.HasValue)
        {
            gpuUpdates.Add(new MetricUpdate("gpu-fan-rpm", "Вентилятор (RPM)", "RPM", sample.GpuFanRpm));
        }
        if (sample.GpuFanPct.HasValue)
        {
            gpuUpdates.Add(new MetricUpdate("gpu-fan-pct", "Вентилятор (%)", "%", sample.GpuFanPct));
        }

        var ramUpdates = new List<MetricUpdate>();
        foreach (var kv in sample.RamTemps.OrderBy(k => k.Key))
        {
            if (kv.Value.HasValue)
            {
                ramUpdates.Add(new MetricUpdate($"ram-temp-{kv.Key}", kv.Key, "°C", kv.Value));
            }
        }
        foreach (var kv in sample.RamClocks.OrderBy(k => k.Key))
        {
            if (kv.Value.HasValue)
            {
                ramUpdates.Add(new MetricUpdate($"ram-clock-{kv.Key}", kv.Key, "MHz", kv.Value));
            }
        }

        SyncMetrics(_basicMetrics, basicUpdates);
        SyncMetrics(_cpuMetrics, cpuUpdates);
        SyncMetrics(_gpuMetrics, gpuUpdates);
        SyncMetrics(_ramMetrics, ramUpdates);

        ShowCpuTab = sample.Availability.HasCpu && _cpuMetrics.Count > 0;
        ShowGpuTab = sample.Availability.HasAnyGpu && _gpuMetrics.Count > 0;
        ShowRamTab = sample.Availability.HasRam && _ramMetrics.Count > 0;
    }

    private void SyncMetrics(ObservableCollection<MetricRow> target, List<MetricUpdate> updates)
    {
        var updateDict = updates.ToDictionary(u => u.Key);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            var row = target[i];
            if (!updateDict.TryGetValue(row.Key, out var update))
            {
                target.RemoveAt(i);
                continue;
            }

            row.Label = update.Label;
            row.Unit = update.Unit;
            row.Value = update.Value;
            updateDict.Remove(row.Key);
        }

        foreach (var update in updates)
        {
            if (updateDict.Remove(update.Key))
            {
                target.Add(new MetricRow(update.Key, update.Label, update.Unit, update.Value));
            }
        }
    }

    private static string? FormatValue(double? value, string format)
        => value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : null;

    private void TryAutoStopOnLimits(HwSample sample)
    {
        if (_device == null || _runCts == null || _isStopping)
        {
            return;
        }

        var cpuLimit = _device.Type == DeviceType.Laptop ? _config.LaptopCpuLimit : _config.DesktopCpuLimit;
        if ((sample.CpuTemp ?? 0) >= cpuLimit || (sample.GpuTemp ?? 0) >= _config.GpuLimit)
        {
            _isStopping = true;
            _stopStatus = "STOP_BY_OVERHEAT";
            SetPhase(_currentPhase, _currentTestName, "STOPPING");
            AddRunNote($"Авто-стоп: CPU {(sample.CpuTemp.HasValue ? sample.CpuTemp.Value.ToString("F1", CultureInfo.InvariantCulture) : "—")} °C, GPU {(sample.GpuTemp.HasValue ? sample.GpuTemp.Value.ToString("F1", CultureInfo.InvariantCulture) : "—")} °C");
            _runCts.Cancel();
        }
    }

    private void SetPhase(string phase, string testName, string status)
    {
        _currentPhase = phase;
        _currentTestName = testName;
        _currentPhaseStatus = status;
    }

    private Task UpdatePhaseAsync(string phase, string testName, string status)
    {
        if (Application.Current?.Dispatcher != null)
        {
            return Application.Current.Dispatcher.InvokeAsync(() => SetPhase(phase, testName, status)).Task;
        }

        SetPhase(phase, testName, status);
        return Task.CompletedTask;
    }

    private Task ShowOcctPromptAsync(string message)
    {
        AddRunNote(message);
        if (Application.Current?.Dispatcher != null)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "ServiceBench", MessageBoxButton.OK, MessageBoxImage.Information);
            }).Task;
        }

        MessageBox.Show(message, "ServiceBench", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    private void AppendRunSample(HwSample sample)
    {
        if (_currentRun == null)
        {
            return;
        }

        lock (_lock)
        {
            if (!_fanSummaryLogged && _currentRun != null)
            {
                var fanLine = $"Fans: CPU {(sample.CpuFanRpm.HasValue ? "✓" : "—")}, GPU RPM {(sample.GpuFanRpm.HasValue ? "✓" : "—")}, GPU % {(sample.GpuFanPct.HasValue ? "✓" : "—")}";
                AddRunNote(fanLine);
                _fanSummaryLogged = true;
            }

            var start = _currentRun.Session.StartedAt;
            var elapsed = (sample.Ts.ToUniversalTime() - start).TotalSeconds;
            var telemetrySample = new RunJsonTelemetrySample
            {
                T = elapsed,
                Phase = _currentPhase,
                TestName = _currentTestName,
                Status = _currentPhaseStatus,
                CpuT = sample.CpuTemp,
                GpuT = sample.GpuTemp,
                CpuRpm = sample.CpuFanRpm,
                GpuRpm = sample.GpuFanRpm,
                CpuMHz = sample.CpuMHz,
                GpuCore = sample.GpuCoreMHz,
                GpuMem = sample.GpuMemMHz,
                GpuFanPct = sample.GpuFanPct
            };

            foreach (var kv in sample.CpuCoreMHz)
            {
                telemetrySample.CpuCoresMHz[$"Core #{kv.Key}"] = kv.Value;
            }

            foreach (var kv in sample.GpuTemps)
            {
                telemetrySample.GpuTemps[kv.Key] = kv.Value;
            }

            foreach (var kv in sample.GpuClocks)
            {
                telemetrySample.GpuClocks[kv.Key] = kv.Value;
            }

            foreach (var kv in sample.RamTemps)
            {
                telemetrySample.RamTemps[kv.Key] = kv.Value;
            }

            foreach (var kv in sample.RamClocks)
            {
                telemetrySample.RamClocks[kv.Key] = kv.Value;
            }

            _samples.Add(telemetrySample);
            _lastTelemetrySample = telemetrySample;
        }
    }

    private void AddRunNote(string? note)
    {
        if (_currentRun == null || string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        if (!_currentRun.Notes.Contains(note))
        {
            _currentRun.Notes.Add(note);
        }
    }

    private string? FormatFurmarkOutputNote()
    {
        var snippet = _furmark.GetOutputSnippet();
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return null;
        }

        return $"furmark output (first 200 lines):{Environment.NewLine}{snippet}";
    }

    private void StopByUser()
    {
        if (_runCts == null)
        {
            return;
        }

        _stopStatus = "STOP_BY_USER";
        SetPhase(_currentPhase, _currentTestName, "STOPPING");
        _runCts.Cancel();
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow();
        window.ShowDialog();
        _config.Initialize(_settingsService);
    }

    private void OpenDeviceFolder()
    {
        if (_device == null)
        {
            return;
        }

        var folder = FileManager.BuildDeviceRoot(_device);
        if (Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private void OpenDashboard()
    {
        if (_device == null)
        {
            return;
        }

        var dashboard = FileManager.DeviceDashboardPath(_device);
        if (File.Exists(dashboard))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dashboard,
                UseShellExecute = true
            });
        }
    }

    private void CaptureScreenshot(string fileName)
    {
        if (_runFolder == null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_runFolder, "screenshots", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var element = Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Application.Current?.MainWindow is FrameworkElement windowRoot)
                {
                    return windowRoot.FindName("LivePanel") as FrameworkElement;
                }

                return null;
            });

            var saved = false;
            if (element != null)
            {
                saved = _screenshotService.SaveElementPng(element, path, _config.CompressScreenshots);
            }

            if (!saved)
            {
                try
                {
                    _screenshotService.CaptureWindow(Config.DefaultSensorWindowTitle, path, _config.CompressScreenshots);
                    saved = true;
                }
                catch
                {
                    // ignore
                }
            }

            if (saved && _currentRun != null)
            {
                _currentRun.Screens.Add(new RunJsonScreen
                {
                    T = Path.GetFileNameWithoutExtension(fileName),
                    File = Path.Combine("screenshots", fileName).Replace('\\', '/')
                });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Не удалось сделать скриншот: {ex.Message}");
        }
    }

    private void StartScreenshotTimer()
    {
        StopScreenshotTimer();
        _screenshotTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
        _screenshotTimer.Elapsed += (_, _) =>
        {
            var name = $"{(int)(DateTime.UtcNow - (_currentRun?.Session.StartedAt ?? DateTime.UtcNow)).TotalSeconds:0000}_sensors.png";
            CaptureScreenshot(name);
        };
        _screenshotTimer.Start();
    }

    private void StopScreenshotTimer()
    {
        if (_screenshotTimer != null)
        {
            _screenshotTimer.Stop();
            _screenshotTimer.Dispose();
            _screenshotTimer = null;
        }
    }
}

public sealed class MetricRow : ViewModelBase
{
    public MetricRow(string key, string label, string unit, double? value)
    {
        Key = key;
        _label = label;
        _unit = unit;
        _value = value;
    }

    public string Key { get; }

    private string _label;
    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    private string _unit;
    public string Unit
    {
        get => _unit;
        set
        {
            if (SetField(ref _unit, value))
            {
                RaisePropertyChanged(nameof(DisplayValue));
            }
        }
    }

    private double? _value;
    public double? Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                RaisePropertyChanged(nameof(DisplayValue));
            }
        }
    }

    public string DisplayValue => FormatDisplay();

    private string FormatDisplay()
    {
        if (!_value.HasValue)
        {
            return "—";
        }

        var format = _unit switch
        {
            "°C" => "F1",
            "MHz" => "F0",
            "RPM" => "F0",
            "%" => "F0",
            _ => "F1"
        };

        return _value.Value.ToString(format, CultureInfo.InvariantCulture);
    }
}

internal readonly record struct MetricUpdate(string Key, string Label, string Unit, double? Value);
