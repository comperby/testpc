using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class FurMarkController
{
    private readonly Config _config;
    private readonly ProcessRunner _processRunner;
    private Process? _process;
    private string? _lastArgsLog;

    public FurMarkController(Config config, ProcessRunner processRunner)
    {
        _config = config;
        _processRunner = processRunner;
    }

    public string? LastArgumentLog => _lastArgsLog;

    public async Task<int> StartAsync(TestPlan plan)
    {
        if (!File.Exists(_config.FurmarkPath))
        {
            throw new FileNotFoundException($"Не найден FurMark по пути {_config.FurmarkPath}. RootBase: {_config.RootBase}");
        }

        _lastArgsLog = null;
        var resolution = ResolveResolution(plan);
        var duration = Math.Max(1, plan.FurmarkMinutes) * 60;

        var outcome = await TryStartFurmarkAsync(
            _config.FurmarkPath,
            resolution,
            duration,
            plan.FurmarkFullscreen);

        if (outcome.Format == 0 || outcome.Process == null)
        {
            var details = string.IsNullOrWhiteSpace(outcome.Output)
                ? string.Empty
                : $"{Environment.NewLine}{outcome.Output.Trim()}";
            throw new InvalidOperationException(
                $"FurMark не принял аргументы. Путь: {_config.FurmarkPath}{details}");
        }

        _process = outcome.Process;
        _lastArgsLog = outcome.Format switch
        {
            1 => $"furmark args: legacy({outcome.Args})",
            2 => $"furmark args: modern({outcome.Args})",
            _ => null
        };

        return outcome.Format;
    }

    private static Resolution ResolveResolution(TestPlan plan)
    {
        if (plan.FurmarkCustom && plan.FurmarkWidth > 0 && plan.FurmarkHeight > 0)
        {
            return new Resolution(plan.FurmarkWidth, plan.FurmarkHeight);
        }

        var preset = plan.SelectedFurmarkPreset ?? "1920x1080";
        var parts = preset.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        {
            return new Resolution(Math.Max(1, w), Math.Max(1, h));
        }

        return new Resolution(1920, 1080);
    }

    private async Task<FurmarkLaunchResult> TryStartFurmarkAsync(string exe, Resolution resolution, int seconds, bool fullscreen)
    {
        var workingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;

        string BuildLegacyArgs()
            => $"/width={resolution.Width} /height={resolution.Height} /time={seconds}" +
               (fullscreen ? " /fullscreen" : string.Empty);

        string BuildModernArgs()
            => $"--width {resolution.Width} --height {resolution.Height} --duration {seconds}" +
               (fullscreen ? " --fullscreen" : string.Empty);

        var legacyArgs = BuildLegacyArgs();
        var legacy = await _processRunner.StartAsync(
            exe,
            legacyArgs,
            workingDirectory,
            hidden: false,
            captureOutput: true,
            timeoutStartMs: 8000);

        if (legacy.Process != null && legacy.Started && !ContainsUnusedParameter(legacy.Output))
        {
            return new FurmarkLaunchResult(1, legacyArgs, legacy.Process, legacy.Output);
        }

        await _processRunner.TryStopAsync(legacy);

        var modernArgs = BuildModernArgs();
        var modern = await _processRunner.StartAsync(
            exe,
            modernArgs,
            workingDirectory,
            hidden: false,
            captureOutput: true,
            timeoutStartMs: 8000);

        if (modern.Process != null && modern.Started)
        {
            return new FurmarkLaunchResult(2, modernArgs, modern.Process, modern.Output);
        }

        await _processRunner.TryStopAsync(modern);

        var combinedOutput = string.Join(
            Environment.NewLine,
            new[] { legacy.Output, modern.Output }.Where(o => !string.IsNullOrWhiteSpace(o)));

        return new FurmarkLaunchResult(0, modernArgs, null, combinedOutput);
    }

    public async Task StopAsync()
    {
        if (_process != null)
        {
            await _processRunner.TryCloseGracefully(_process);
            _process = null;
        }
    }

    private static bool ContainsUnusedParameter(string output)
        => output.Contains("unused parameter", StringComparison.OrdinalIgnoreCase);

    private sealed record Resolution(int Width, int Height);

    private sealed record FurmarkLaunchResult(int Format, string Args, Process? Process, string Output);
}
