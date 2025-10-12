using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class AidaController
{
    private readonly Config _config;
    private readonly ProcessRunner _processRunner;
    private Process? _process;

    public AidaController(Config config, ProcessRunner processRunner)
    {
        _config = config;
        _processRunner = processRunner;
    }

    public string BuildSstArgs(TestPlan plan, string? csvPath)
    {
        var tests = new List<string>();
        if (plan.AidaCpu) tests.Add("CPU");
        if (plan.AidaFpu) tests.Add("FPU");
        if (plan.AidaCache) tests.Add("CACHE");
        if (plan.AidaMemory) tests.Add("MEM");
        if (plan.AidaDisk) tests.Add("DISK");
        if (plan.AidaGpu) tests.Add("GPU");
        if (tests.Count == 0)
        {
            tests.Add("CPU");
        }

        var duration = Math.Max(1, plan.AidaDurationMinutes) * 60;
        var args = $"/SILENT /SST {string.Join(',', tests)} /DURATION {duration}";
        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            args += $" /SENSORLOG \"{csvPath}\"";
        }

        return args;
    }

    public Process StartSst(TestPlan plan, string? csvPath)
    {
        if (!File.Exists(_config.AidaPath))
        {
            throw new FileNotFoundException($"Не найден AIDA64 по пути {_config.AidaPath}. RootBase: {_config.RootBase}");
        }

        var args = BuildSstArgs(plan, csvPath);
        _process = _processRunner.Start(_config.AidaPath, args);
        return _process;
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
