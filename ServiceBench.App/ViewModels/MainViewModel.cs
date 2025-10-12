using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly TelemetryCollector _telemetry;
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

    private string _statusMessage = "Готово";
    private string _logText = string.Empty;
    private string _runTag = "before";
    private string _customRunTag = string.Empty;
    private bool _isCustomTag;
    private string? _stopStatus;

    public MainViewModel()
    {
        _config.Initialize(_settingsService);
        _reportGenerator = new ReportGenerator(_config);
        Plan = new TestPlan();
        LiveTelemetry = new LiveTelemetry();
        _aida = new AidaController(_config, _processRunner);
        _occt = new OcctController(_config, _processRunner);
        _furmark = new FurMarkController(_config, _processRunner);
        _telemetry = new TelemetryCollector(_config);
        _telemetry.SampleCollected += OnSampleCollected;
        _hotkeyService.EscPressed += (_, _) => StopByUser();

        StartCommand = new RelayCommand(async () => await StartAsync(), () => _runCts == null);
        StopCommand = new RelayCommand(StopByUser, () => _runCts != null);
        OpenDeviceFolderCommand = new RelayCommand(OpenDeviceFolder);
        OpenDashboardCommand = new RelayCommand(OpenDashboard);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
    }

    public TestPlan Plan { get; }
    public LiveTelemetry LiveTelemetry { get; }
    public ObservableCollection<string> FurmarkPresets => _furmarkPresets;
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand OpenDeviceFolderCommand { get; }
    public RelayCommand OpenDashboardCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

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

    public void AttachHotkey(Window window) => _hotkeyService.Register(window);

    public void DetachHotkey()
    {
        _hotkeyService.Unregister();
        _telemetry.SampleCollected -= OnSampleCollected;
        _telemetry.Stop();
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
            StatusMessage = "Запуск тестов...";
            AppendLog("Начало прогона");

            _device = _deviceDetector.Detect();
            var tag = RunTag == "custom" ? (string.IsNullOrWhiteSpace(CustomRunTag) ? "custom" : CustomRunTag) : RunTag;
            tag = SlugHelper.Slugify(tag);
            var startedAt = DateTime.UtcNow;
            _runFolder = FileManager.BuildRunFolder(_device, tag, startedAt);
            _csvPath = Plan.SaveAidaCsv ? Path.Combine(_runFolder, "aida_sensors.csv") : Path.Combine(Path.GetTempPath(), $"servicebench_{Guid.NewGuid():N}.csv");

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
                tests.Add(new RunJsonTest { Name = "OCCT CPU Small", DurationSec = Plan.OcctCpuMinutes * 60 });
                tests.Add(new RunJsonTest { Name = "OCCT GPU 3D", DurationSec = Plan.OcctGpuMinutes * 60 });
                tests.Add(new RunJsonTest { Name = "OCCT VRAM", DurationSec = Plan.OcctVramMinutes * 60 });
            }
            if (Plan.UseFurmark)
            {
                tests.Add(new RunJsonTest { Name = "FurMark", DurationSec = Plan.FurmarkMinutes * 60 });
            }
            _currentRun.Tests = tests;

            _reportGenerator.EnsureAssets(_device);
            _telemetry.Start(_csvPath);
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
                CaptureScreenshot("final_sensors.png");
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
                    await _occt.StartAsync(Plan, token);
                    var totalMinutes = Math.Max(1, Plan.OcctCpuMinutes + Plan.OcctGpuMinutes + Plan.OcctVramMinutes);
                    await WaitWithCancellation(TimeSpan.FromMinutes(totalMinutes), token);
                }
                finally
                {
                    await _occt.StopAsync();
                }
                CaptureScreenshot($"occt_final_{DateTime.Now:HHmmss}.png");
            }

            if (token.IsCancellationRequested)
            {
                CompleteRunAndNotify(_stopStatus ?? "STOP_BY_USER");
                return;
            }

            if (Plan.UseFurmark)
            {
                AppendLog("Запуск FurMark");
                try
                {
                    _furmark.Start(Plan);
                    await WaitWithCancellation(TimeSpan.FromMinutes(Math.Max(1, Plan.FurmarkMinutes)), token);
                }
                finally
                {
                    await _furmark.StopAsync();
                }
                CaptureScreenshot($"furmark_final_{DateTime.Now:HHmmss}.png");
            }

            CompleteRunAndNotify(_stopStatus ?? "OK");
        }
        finally
        {
            _telemetry.Stop();
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

        FinishRun(status, reason);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            var (message, icon) = status switch
            {
                "STOP_BY_OVERHEAT" => ("Тест завершён в связи с перегревом", MessageBoxImage.Error),
                "STOP_BY_USER" => ("Тест успешно остановлен", MessageBoxImage.Information),
                "OK" => ("Все тесты пройдены", MessageBoxImage.Information),
                _ => ("Произошла ошибка", MessageBoxImage.Error)
            };

            MessageBox.Show(message, "ServiceBench", MessageBoxButton.OK, icon);
        });
    }

    private void FinishRun(string status, string reason)
    {
        if (_currentRun == null || _device == null || _runFolder == null)
        {
            return;
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

        _currentRun.Telemetry.Peaks.CpuTempMax = _samples.Max(s => s.CpuT);
        _currentRun.Telemetry.Peaks.GpuTempMax = _samples.Max(s => s.GpuT);
        _currentRun.Telemetry.Peaks.CpuFanMax = _samples.Max(s => s.CpuRpm);
        _currentRun.Telemetry.Peaks.GpuFanMax = _samples.Max(s => s.GpuRpm);
        _currentRun.Telemetry.Peaks.CpuFreqAvg = _samples.Average(s => s.CpuMHz);
        _currentRun.Telemetry.Peaks.GpuCoreAvg = _samples.Average(s => s.GpuCore);
        _currentRun.Telemetry.Peaks.GpuMemAvg = _samples.Average(s => s.GpuMem);
    }

    private void AppendLog(string message)
    {
        LogText = new StringBuilder(LogText).AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}").ToString();
    }

    private void OnSampleCollected(object? sender, TelemetrySample e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LiveTelemetry.CpuTemp = e.CpuTemp;
            LiveTelemetry.GpuTemp = e.GpuTemp;
            LiveTelemetry.CpuFreq = e.CpuFreq;
            LiveTelemetry.GpuCore = e.GpuCore;
            LiveTelemetry.GpuMem = e.GpuMem;
            LiveTelemetry.CpuFan = e.CpuFan;
            LiveTelemetry.GpuFan = e.GpuFan;
        });

        if (_currentRun == null)
        {
            return;
        }

        var start = _currentRun.Session.StartedAt;
        lock (_lock)
        {
            var sample = new RunJsonTelemetrySample
            {
                T = (DateTime.UtcNow - start).TotalSeconds,
                CpuT = e.CpuTemp,
                GpuT = e.GpuTemp,
                CpuRpm = e.CpuFan,
                GpuRpm = e.GpuFan,
                CpuMHz = e.CpuFreq,
                GpuCore = e.GpuCore,
                GpuMem = e.GpuMem
            };
            _samples.Add(sample);
        }

        CheckThresholds(e);
    }

    private void CheckThresholds(TelemetrySample sample)
    {
        if (_device == null || _runCts == null || _isStopping)
        {
            return;
        }

        var cpuLimit = _device.Type == DeviceType.Laptop ? _config.LaptopCpuLimit : _config.DesktopCpuLimit;
        if (sample.CpuTemp >= cpuLimit || sample.GpuTemp >= _config.GpuLimit)
        {
            _isStopping = true;
            _stopStatus = "STOP_BY_OVERHEAT";
            _runCts.Cancel();
        }
    }

    private void StopByUser()
    {
        if (_runCts == null)
        {
            return;
        }

        _stopStatus = "STOP_BY_USER";
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
            _screenshotService.CaptureWindow(Config.DefaultSensorWindowTitle, path);
            _currentRun?.Screens.Add(new RunJsonScreen
            {
                T = fileName.Replace("_sensors.png", string.Empty),
                File = Path.Combine("screenshots", fileName).Replace('\\', '/')
            });
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
