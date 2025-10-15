using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace ServiceBench.App.Services;

public record HwSample(
    DateTime Ts,
    double? CpuTemp,
    double? GpuTemp,
    double? CpuFanRpm,
    double? GpuFanRpm,
    double? GpuFanPct,
    double? CpuMHz,
    double? GpuCoreMHz,
    double? GpuMemMHz,
    IReadOnlyDictionary<int, double?> CpuCoreMHz,
    IReadOnlyDictionary<string, double?> GpuTemps,
    IReadOnlyDictionary<string, double?> GpuClocks,
    IReadOnlyDictionary<string, double?> RamTemps,
    IReadOnlyDictionary<string, double?> RamClocks,
    HwAvailability Availability);

public sealed class HwAvailability
{
    public bool HasCpu { get; init; }
    public bool HasDiscreteGpu { get; init; }
    public bool HasAnyGpu { get; init; }
    public bool HasRam { get; init; }
}

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

                var sensors = CaptureSensorsSnapshot();
                var availability = BuildAvailability();

                double? Max(Func<ISensor, bool> predicate)
                    => sensors.Where(predicate).Select(s => s.Value).Where(v => v.HasValue).DefaultIfEmpty().Max();

                double? Avg(Func<ISensor, bool> predicate)
                {
                    var values = sensors.Where(predicate).Select(s => s.Value).Where(v => v.HasValue).Select(v => (double)v!.Value).ToArray();
                    return values.Length == 0 ? (double?)null : values.Average();
                }

                bool IsCpu(ISensor s) => s.Hardware.HardwareType == HardwareType.Cpu;
                bool IsGpuAny(ISensor s) => s.Hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

                var cpuTemp = Max(s => IsCpu(s) && s.SensorType == SensorType.Temperature);
                var cpuFan = Max(s =>
                    s.SensorType == SensorType.Fan &&
                    (s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                     s.Hardware.HardwareType == HardwareType.Motherboard));
                var cpuClockAvg = Avg(s => IsCpu(s) && s.SensorType == SensorType.Clock &&
                                             (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                              s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase)));

                var gpuTemp = Max(s => IsGpuAny(s) && s.SensorType == SensorType.Temperature);
                var gpuFanRpm = Max(s => IsGpuAny(s) && s.SensorType == SensorType.Fan);
                var gpuFanPct = Max(s => IsGpuAny(s) && s.SensorType == SensorType.Control &&
                                             s.Name.Contains("Fan", StringComparison.OrdinalIgnoreCase));
                var gpuCore = Avg(s => IsGpuAny(s) && s.SensorType == SensorType.Clock &&
                                         s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase));
                var gpuMem = Avg(s => IsGpuAny(s) && s.SensorType == SensorType.Clock &&
                                        s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase));

                var cpuCoreDict = BuildCpuCoreDictionary(sensors);
                var gpuTemps = BuildDictionary(sensors, SensorType.Temperature, IsGpuAny);
                var gpuClocks = BuildDictionary(sensors, SensorType.Clock, IsGpuAny);
                var ramTemps = BuildDictionary(sensors, SensorType.Temperature, s => s.Hardware.HardwareType == HardwareType.Memory);
                var ramClocks = BuildDictionary(sensors, SensorType.Clock, s => s.Hardware.HardwareType == HardwareType.Memory);

                var sample = new HwSample(
                    DateTime.Now,
                    cpuTemp,
                    gpuTemp,
                    cpuFan,
                    gpuFanRpm,
                    gpuFanPct,
                    cpuClockAvg,
                    gpuCore,
                    gpuMem,
                    cpuCoreDict,
                    gpuTemps,
                    gpuClocks,
                    ramTemps,
                    ramClocks,
                    availability);

                OnSample?.Invoke(sample);
            }
            catch
            {
                // ignored
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

    private ISensor[] CaptureSensorsSnapshot()
    {
        var list = new List<ISensor>();
        foreach (var hardware in _computer.Hardware)
        {
            CollectSensors(hardware, list);
        }

        return list.ToArray();
    }

    private static void CollectSensors(IHardware hardware, ICollection<ISensor> destination)
    {
        foreach (var sensor in hardware.Sensors)
        {
            destination.Add(sensor);
        }

        foreach (var sub in hardware.SubHardware)
        {
            CollectSensors(sub, destination);
        }
    }

    private IReadOnlyDictionary<int, double?> BuildCpuCoreDictionary(ISensor[] sensors)
    {
        var coreGroups = sensors
            .Where(s => s.Hardware.HardwareType == HardwareType.Cpu && s.SensorType == SensorType.Clock)
            .Select(s => new { Sensor = s, Index = ParseCoreIndex(s.Name) })
            .Where(x => x.Index.HasValue)
            .GroupBy(x => x.Index!.Value);

        var result = new Dictionary<int, double?>();
        foreach (var group in coreGroups)
        {
            var values = group.Select(g => g.Sensor.Value).Where(v => v.HasValue).Select(v => (double)v!.Value).ToArray();
            result[group.Key] = values.Length == 0 ? (double?)null : values.Average();
        }

        return result;
    }

    private static int? ParseCoreIndex(string name)
    {
        var idx = name.IndexOf('#');
        if (idx < 0 || idx + 1 >= name.Length)
        {
            return null;
        }

        idx += 1;
        var end = idx;
        while (end < name.Length && char.IsDigit(name[end]))
        {
            end++;
        }

        if (int.TryParse(name[idx..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, double?> BuildDictionary(
        ISensor[] sensors,
        SensorType type,
        Func<ISensor, bool> predicate)
    {
        var dict = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (var sensor in sensors.Where(s => predicate(s) && s.SensorType == type))
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var key = sensor.Name.Trim();
            if (dict.ContainsKey(key))
            {
                key = $"{key} ({sensor.Hardware.Name.Trim()})";
            }

            dict[key] = sensor.Value;
        }

        return dict;
    }

    private HwAvailability BuildAvailability()
    {
        var types = _computer.Hardware.Select(h => h.HardwareType).ToArray();
        return new HwAvailability
        {
            HasCpu = types.Contains(HardwareType.Cpu),
            HasDiscreteGpu = types.Contains(HardwareType.GpuAmd) || types.Contains(HardwareType.GpuNvidia),
            HasAnyGpu = types.Contains(HardwareType.GpuAmd) || types.Contains(HardwareType.GpuNvidia) || types.Contains(HardwareType.GpuIntel),
            HasRam = types.Contains(HardwareType.Memory)
        };
    }

    public void Dispose()
    {
        Stop();
        _computer.Close();
    }
}
