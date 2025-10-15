using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ServiceBench.App.Models;

public class RunJson
{
    [JsonPropertyName("device")]
    public RunJsonDevice Device { get; set; } = new();

    [JsonPropertyName("session")]
    public RunJsonSession Session { get; set; } = new();

    [JsonPropertyName("tests")]
    public List<RunJsonTest> Tests { get; set; } = new();

    [JsonPropertyName("telemetry")]
    public RunJsonTelemetry Telemetry { get; set; } = new();

    [JsonPropertyName("screens")]
    public List<RunJsonScreen> Screens { get; set; } = new();

    [JsonPropertyName("brand")]
    public RunJsonBrand Brand { get; set; } = new();

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();
}

public class RunJsonDevice
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("cpu")]
    public string Cpu { get; set; } = string.Empty;

    [JsonPropertyName("gpu")]
    public string Gpu { get; set; } = string.Empty;
}

public class RunJsonSession
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }
        = DateTime.UtcNow;

    [JsonPropertyName("endedAt")]
    public DateTime EndedAt { get; set; }
        = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "OK";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class RunJsonTest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("durationSec")]
    public int DurationSec { get; set; }
        = 0;
}

public class RunJsonTelemetry
{
    [JsonPropertyName("hz")]
    public int Hz { get; set; } = 1;

    [JsonPropertyName("timeline")]
    public List<RunJsonTelemetrySample> Timeline { get; set; } = new();

    [JsonPropertyName("peaks")]
    public RunJsonPeaks Peaks { get; set; } = new();
}

public class RunJsonTelemetrySample
{
    [JsonPropertyName("t")]
    public double T { get; set; }
        = 0;

    [JsonPropertyName("phase")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phase { get; set; }
        = null;

    [JsonPropertyName("test")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TestName { get; set; }
        = null;

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }
        = null;

    [JsonPropertyName("cpuT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CpuT { get; set; }
        = null;

    [JsonPropertyName("gpuT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GpuT { get; set; }
        = null;

    [JsonPropertyName("cpuRpm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CpuRpm { get; set; }
        = null;

    [JsonPropertyName("gpuRpm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GpuRpm { get; set; }
        = null;

    [JsonPropertyName("gpuFanPct")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GpuFanPct { get; set; }
        = null;

    [JsonPropertyName("cpuMHz")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CpuMHz { get; set; }
        = null;

    [JsonPropertyName("gpuCore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GpuCore { get; set; }
        = null;

    [JsonPropertyName("gpuMem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GpuMem { get; set; }
        = null;

    [JsonPropertyName("cpuCoresMHz")]
    public Dictionary<string, double?> CpuCoresMHz { get; set; } = new();

    [JsonPropertyName("gpuTemps")]
    public Dictionary<string, double?> GpuTemps { get; set; } = new();

    [JsonPropertyName("gpuClocks")]
    public Dictionary<string, double?> GpuClocks { get; set; } = new();

    [JsonPropertyName("ramTemps")]
    public Dictionary<string, double?> RamTemps { get; set; } = new();

    [JsonPropertyName("ramClocks")]
    public Dictionary<string, double?> RamClocks { get; set; } = new();
}

public class RunJsonPeaks
{
    [JsonPropertyName("cpuTempMax")]
    public double CpuTempMax { get; set; }
        = 0;

    [JsonPropertyName("gpuTempMax")]
    public double GpuTempMax { get; set; }
        = 0;

    [JsonPropertyName("cpuFanMax")]
    public double CpuFanMax { get; set; }
        = 0;

    [JsonPropertyName("gpuFanMax")]
    public double GpuFanMax { get; set; }
        = 0;

    [JsonPropertyName("gpuFanPctMax")]
    public double GpuFanPctMax { get; set; }
        = 0;

    [JsonPropertyName("cpuFreqAvg")]
    public double CpuFreqAvg { get; set; }
        = 0;

    [JsonPropertyName("gpuCoreAvg")]
    public double GpuCoreAvg { get; set; }
        = 0;

    [JsonPropertyName("gpuMemAvg")]
    public double GpuMemAvg { get; set; }
        = 0;
}

public class RunJsonScreen
{
    [JsonPropertyName("t")]
    public string T { get; set; } = "0";

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;
}

public class RunJsonBrand
{
    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("workingHours")]
    public string WorkingHours { get; set; } = string.Empty;

    [JsonPropertyName("logoRelPath")]
    public string? LogoRelPath { get; set; }
        = null;
}
