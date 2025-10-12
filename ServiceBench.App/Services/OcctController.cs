using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class OcctController
{
    private readonly Config _config;
    private readonly ProcessRunner _processRunner;
    private readonly List<Process> _processes = new();

    public OcctController(Config config, ProcessRunner processRunner)
    {
        _config = config;
        _processRunner = processRunner;
    }

    public async Task<List<Process>> StartAsync(TestPlan plan, CancellationToken token)
    {
        if (!File.Exists(_config.OcctPath))
        {
            throw new FileNotFoundException($"Не найден OCCT по пути {_config.OcctPath}. RootBase: {_config.RootBase}");
        }

        _processes.Clear();

        bool delay = false;
        if (plan.OcctCpuMinutes > 0)
        {
            _processes.Add(await StartProfileAsync("OCCT CPU Small", plan.OcctCpuMinutes, delay, token));
            delay = true;
        }
        if (plan.OcctGpuMinutes > 0)
        {
            _processes.Add(await StartProfileAsync("OCCT GPU 3D", plan.OcctGpuMinutes, delay, token));
            delay = true;
        }
        if (plan.OcctVramMinutes > 0)
        {
            _processes.Add(await StartProfileAsync("OCCT VRAM", plan.OcctVramMinutes, delay, token));
        }

        return _processes;
    }

    private async Task<Process> StartProfileAsync(string name, int minutes, bool delayBeforeStart, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (delayBeforeStart)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
        var process = _processRunner.Start(_config.OcctPath, string.Empty);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), token);
                await _processRunner.TryCloseGracefully(process);
            }
            catch (TaskCanceledException)
            {
                await _processRunner.TryCloseGracefully(process);
            }
        }, token);
        return process;
    }

    public async Task StopAsync()
    {
        foreach (var process in _processes)
        {
            await _processRunner.TryCloseGracefully(process);
        }
        _processes.Clear();
    }
}
