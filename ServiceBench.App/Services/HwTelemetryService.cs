using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace ServiceBench.App.Services;

public record HwSample(DateTime Ts,
    double? CpuTemp,
    double? GpuTemp,
    double? CpuFanRpm,
    double? GpuFanRpm,
    double? CpuMHz,
    double? GpuCoreMHz,
    double? GpuMemMHz,
    double? GpuFanPct);

public sealed class HwTelemetryService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
        IsMemoryEnabled = true,
        IsControllerEnabled = true
    };

    private CancellationTokenSource? _cts;

    public event Action<HwSample>? OnSample;

    public void Start(int hz = 1)
    {
        if (_cts != null)
        {
            return;
        }

        _computer.Open();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => LoopAsync(_cts.Token, hz));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task LoopAsync(CancellationToken token, int hz)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Max(200, 1000 / Math.Max(1, hz)));
        while (!token.IsCancellationRequested)
        {
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var sub in hardware.SubHardware)
                    {
                        sub.Update();
                    }
                }

                var sensors = _computer.Hardware.SelectMany(h => h.Sensors).ToArray();

                double? Max(Func<ISensor, bool> predicate)
                    => sensors.Where(predicate).Select(s => s.Value).Where(v => v.HasValue).DefaultIfEmpty().Max();

                double? Avg(Func<ISensor, bool> predicate)
                {
                    var values = sensors.Where(predicate).Select(s => s.Value).Where(v => v.HasValue).ToArray();
                    return values.Length == 0 ? null : values.Average();
                }

                bool IsGpu(ISensor sensor)
                    => sensor.Hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd;

                var cpuTemp = Max(s => s.Hardware.HardwareType == HardwareType.Cpu && s.SensorType == SensorType.Temperature);
                var cpuFan = Max(s =>
                    s.SensorType == SensorType.Fan &&
                    (s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                     s.Hardware.HardwareType == HardwareType.Motherboard));
                var cpuClock = Avg(s => s.Hardware.HardwareType == HardwareType.Cpu &&
                                        s.SensorType == SensorType.Clock &&
                                        (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                         s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase)));

                var gpuTemp = Max(s => IsGpu(s) && s.SensorType == SensorType.Temperature);
                var gpuFan = Max(s => IsGpu(s) && s.SensorType == SensorType.Fan);
                var gpuFanPct = Max(s => IsGpu(s) &&
                                          s.SensorType == SensorType.Control &&
                                          s.Name.Contains("Fan", StringComparison.OrdinalIgnoreCase));
                var gpuCore = Avg(s => IsGpu(s) && s.SensorType == SensorType.Clock &&
                                        s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase));
                var gpuMem = Avg(s => IsGpu(s) && s.SensorType == SensorType.Clock &&
                                       s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase));

                OnSample?.Invoke(new HwSample(DateTime.Now, cpuTemp, gpuTemp, cpuFan, gpuFan, cpuClock, gpuCore, gpuMem, gpuFanPct));
            }
            catch
            {
                // ignore and continue gathering data
            }

            try
            {
                await Task.Delay(delay, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _computer.Close();
    }
}
