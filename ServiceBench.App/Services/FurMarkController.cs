using System;
using System.Diagnostics;
using System.IO;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class FurMarkController
{
    private readonly Config _config;
    private readonly ProcessRunner _processRunner;
    private Process? _process;

    public FurMarkController(Config config, ProcessRunner processRunner)
    {
        _config = config;
        _processRunner = processRunner;
    }

    public Process Start(TestPlan plan)
    {
        if (!File.Exists(_config.FurmarkPath))
        {
            throw new FileNotFoundException($"Не найден FurMark по пути {_config.FurmarkPath}. RootBase: {_config.RootBase}");
        }

        var width = plan.FurmarkCustom ? plan.FurmarkWidth : ParseWidth(plan.SelectedFurmarkPreset);
        var height = plan.FurmarkCustom ? plan.FurmarkHeight : ParseHeight(plan.SelectedFurmarkPreset);
        var duration = Math.Max(1, plan.FurmarkMinutes) * 60;
        var args = $"/width={width} /height={height} /time={duration}";
        if (plan.FurmarkFullscreen)
        {
            args += " /fullscreen";
        }

        _process = _processRunner.Start(_config.FurmarkPath, args);
        return _process;
    }

    private static int ParseWidth(string preset)
    {
        var parts = preset.Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) ? w : 1920;
    }

    private static int ParseHeight(string preset)
    {
        var parts = preset.Split('x');
        return parts.Length == 2 && int.TryParse(parts[1], out var h) ? h : 1080;
    }

    public async Task StopAsync()
    {
        if (_process != null)
        {
            await _processRunner.TryCloseGracefully(_process);
            _process = null;
        }
    }
}
