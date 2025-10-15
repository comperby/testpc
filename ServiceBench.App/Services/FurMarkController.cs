using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public sealed class FurMarkController
{
    private const int OutputLinesLimit = 200;

    private static readonly Dictionary<string, string> PresetFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1280x720"] = "--p720",
        ["1920x1080"] = "--p1080",
        ["2560x1440"] = "--p1440",
        ["3840x2160"] = "--p2160"
    };

    private readonly Config _config;
    private readonly ProcessRunner _runner;

    private ProcessStartResult? _startResult;
    private Process? _process;
    private CancellationTokenSource? _watchdogCts;
    private bool? _supportsMaxTime;

    private string? _switchLog;
    private string? _argsLog;
    private string? _stopLog;

    public FurMarkController(Config config, ProcessRunner runner)
    {
        _config = config;
        _runner = runner;
    }

    public string? SwitchLog => _switchLog;
    public string? LastArgumentLog => _argsLog;
    public string? LastStopLog => _stopLog;

    public string? GetOutputSnippet(int maxLines = OutputLinesLimit)
    {
        if (_startResult == null)
        {
            return null;
        }

        var text = _startResult.GetOutputSnapshot();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= maxLines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        return string.Join(Environment.NewLine, lines.Take(maxLines));
    }

    public async Task StartAsync(TestPlan plan)
    {
        var exe = ResolveExecutable();
        var resolution = ResolveResolution(plan);
        var seconds = Math.Max(1, plan.FurmarkMinutes) * 60;
        var presetFlag = ResolvePreset(plan, resolution);

        _switchLog = _config.FurmarkSwitchedToGui
            ? "furmark: switching to FurMark_GUI.exe (CLI detected via .bat)"
            : null;
        _stopLog = null;

        var supportsMaxTime = await SupportsMaxTimeAsync(exe);
        var arguments = BuildArguments(presetFlag, resolution, seconds, plan.FurmarkCustom, plan.FurmarkFullscreen, supportsMaxTime);

        _startResult = await _runner.StartAsync(
            exe,
            arguments,
            Path.GetDirectoryName(exe),
            hidden: false,
            captureOutput: true,
            timeoutStartMs: 8000);

        _process = _startResult.Process;
        if (_process == null || !_startResult.Started)
        {
            var failure = _startResult.GetOutputSnapshot();
            throw new InvalidOperationException($"FurMark не принял аргументы. Путь: {exe}{Environment.NewLine}{failure}");
        }

        _argsLog = $"furmark args: gui {arguments} [max-time: {(supportsMaxTime ? "yes" : "no")}]";

        if (!supportsMaxTime)
        {
            _watchdogCts = new CancellationTokenSource();
            _ = WatchdogAsync(_process, seconds, _watchdogCts.Token);
        }
    }

    public async Task StopAsync()
    {
        _watchdogCts?.Cancel();
        _watchdogCts = null;

        if (_process != null)
        {
            await _runner.TryCloseGracefully(_process);
            _process = null;
        }
    }

    private string ResolveExecutable()
    {
        if (File.Exists(_config.FurmarkPath))
        {
            return _config.FurmarkPath;
        }

        throw new FileNotFoundException($"Не найден FurMark по пути {_config.FurmarkPath}. RootBase: {_config.RootBase}");
    }

    private static Resolution ResolveResolution(TestPlan plan)
    {
        if (plan.FurmarkCustom && plan.FurmarkWidth > 0 && plan.FurmarkHeight > 0)
        {
            return new Resolution(plan.FurmarkWidth, plan.FurmarkHeight);
        }

        var preset = plan.SelectedFurmarkPreset ?? "1920x1080";
        if (TryParseResolution(preset, out var parsed))
        {
            return parsed;
        }

        return new Resolution(1920, 1080);
    }

    private static bool TryParseResolution(string input, out Resolution resolution)
    {
        resolution = default;
        var parts = input.Split('x', 'X');
        if (parts.Length != 2)
        {
            return false;
        }

        if (int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w > 0 && h > 0)
        {
            resolution = new Resolution(w, h);
            return true;
        }

        return false;
    }

    private static string? ResolvePreset(TestPlan plan, Resolution resolution)
    {
        if (plan.FurmarkCustom)
        {
            return null;
        }

        if (plan.SelectedFurmarkPreset is string preset && PresetFlags.TryGetValue(preset, out var flag))
        {
            return flag;
        }

        if (PresetFlags.TryGetValue($"{resolution.Width}x{resolution.Height}", out var flagFromResolution))
        {
            return flagFromResolution;
        }

        return null;
    }

    private async Task<bool> SupportsMaxTimeAsync(string exe)
    {
        if (_supportsMaxTime.HasValue)
        {
            return _supportsMaxTime.Value;
        }

        try
        {
            var help = await _runner.RunForOutputAsync(exe, "--help", Path.GetDirectoryName(exe), 8000);
            _supportsMaxTime = help.Output.IndexOf("--max-time", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            _supportsMaxTime = false;
        }

        return _supportsMaxTime.Value;
    }

    private static string BuildArguments(
        string? presetFlag,
        Resolution resolution,
        int seconds,
        bool isCustom,
        bool fullscreen,
        bool supportsMaxTime)
    {
        var parts = new List<string>
        {
            "--demo furmark-gl",
            "--benchmark"
        };

        if (!string.IsNullOrEmpty(presetFlag) && !isCustom)
        {
            parts.Add(presetFlag);
        }
        else
        {
            parts.Add($"--width {resolution.Width}");
            parts.Add($"--height {resolution.Height}");
        }

        if (supportsMaxTime)
        {
            parts.Add($"--max-time {seconds}");
        }

        if (fullscreen)
        {
            parts.Add("--fullscreen");
        }

        parts.Add("--no-score-box");
        return string.Join(' ', parts);
    }

    private async Task WatchdogAsync(Process process, int seconds, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            if (token.IsCancellationRequested || process.HasExited)
            {
                return;
            }

            _stopLog = $"furmark stop: watchdog after {seconds} sec";

            try
            {
                process.CloseMainWindow();
            }
            catch
            {
                // ignore
            }

            var exited = await Task.Run(() => process.WaitForExit(3000), token);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
    }

    private readonly record struct Resolution(int Width, int Height);
}
