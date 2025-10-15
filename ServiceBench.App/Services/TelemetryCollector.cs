using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class TelemetryCollector
{
    private readonly Config _config;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(1);
    private CancellationTokenSource? _cts;

    public event EventHandler<TelemetrySample>? SampleCollected;

    public TelemetryCollector(Config config)
    {
        _config = config;
    }

    public void Start(string csvPath)
    {
        Stop();
        _cts = new CancellationTokenSource();
        Task.Run(() => LoopAsync(csvPath, _cts.Token));
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task LoopAsync(string csvPath, CancellationToken token)
    {
        var lastLength = 0L;
        while (!token.IsCancellationRequested)
        {
            if (File.Exists(csvPath))
            {
                try
                {
                    using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length != lastLength)
                    {
                        lastLength = stream.Length;
                        using var reader = new StreamReader(stream, leaveOpen: true);
                        var content = await reader.ReadToEndAsync();
                        var lines = content.Trim().Split('\n');
                        if (lines.Length > 1)
                        {
                            var headers = lines[0].Trim().Split(';');
                            var lastLine = lines[^1].Trim().Split(';');
                            var sample = ParseSample(headers, lastLine);
                            SampleCollected?.Invoke(this, sample);
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            await Task.Delay(_interval, token);
        }
    }

    private static TelemetrySample ParseSample(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        var sample = new TelemetrySample();
        for (var i = 0; i < headers.Count && i < values.Count; i++)
        {
            var header = headers[i].Trim();
            var value = values[i].Trim();
            double parsed = ParseDouble(value);
            if (header.Contains("CPU Temperature", StringComparison.OrdinalIgnoreCase) || header.Contains("CPU Diode", StringComparison.OrdinalIgnoreCase))
            {
                sample.CpuTemp = parsed;
            }
            else if (header.Contains("GPU Temperature", StringComparison.OrdinalIgnoreCase) || header.Contains("GPU Diode", StringComparison.OrdinalIgnoreCase))
            {
                sample.GpuTemp = parsed;
            }
            else if (header.Contains("CPU Fan", StringComparison.OrdinalIgnoreCase))
            {
                sample.CpuFan = parsed;
            }
            else if (header.Contains("GPU Fan", StringComparison.OrdinalIgnoreCase))
            {
                sample.GpuFan = parsed;
            }
            else if (header.Contains("CPU Clock", StringComparison.OrdinalIgnoreCase) || header.Contains("Effective Clock", StringComparison.OrdinalIgnoreCase))
            {
                sample.CpuFreq = parsed;
            }
            else if (header.Contains("GPU Core Clock", StringComparison.OrdinalIgnoreCase))
            {
                sample.GpuCore = parsed;
            }
            else if (header.Contains("GPU Memory Clock", StringComparison.OrdinalIgnoreCase))
            {
                sample.GpuMem = parsed;
            }
        }

        sample.Timestamp = DateTime.Now;
        return sample;
    }

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        return 0;
    }
}

public class TelemetrySample : EventArgs
{
    public DateTime Timestamp { get; set; }
        = DateTime.Now;
    public double CpuTemp { get; set; }
        = 0;
    public double GpuTemp { get; set; }
        = 0;
    public double CpuFan { get; set; }
        = 0;
    public double GpuFan { get; set; }
        = 0;
    public double CpuFreq { get; set; }
        = 0;
    public double GpuCore { get; set; }
        = 0;
    public double GpuMem { get; set; }
        = 0;
}
