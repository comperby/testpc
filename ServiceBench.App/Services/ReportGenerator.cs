using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class ReportGenerator
{
    private readonly Config _config;

    public ReportGenerator(Config config)
    {
        _config = config;
    }

    public void EnsureAssets(DeviceInfo device)
    {
        var deviceRoot = FileManager.BuildDeviceRoot(device);
        var assetsFolder = Path.Combine(deviceRoot, "assets");
        Directory.CreateDirectory(assetsFolder);
        CopyAsset("styles.css", assetsFolder);
        CopyAsset("chart.min.js", assetsFolder);
    }

    public void GenerateRunReport(DeviceInfo device, RunJson run, string runFolder)
    {
        EnsureAssets(device);
        var jsonPath = Path.Combine(runFolder, "run.json");
        var htmlPath = Path.Combine(runFolder, "run.html");
        var logPath = Path.Combine(runFolder, "run.txt");

        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json, Encoding.UTF8);

        var template = LoadAsset("report-template.html");
        var html = template
            .Replace("{{companyName}}", run.Brand.CompanyName)
            .Replace("{{address}}", run.Brand.Address)
            .Replace("{{workingHours}}", run.Brand.WorkingHours)
            .Replace("{{logo}}", string.IsNullOrWhiteSpace(run.Brand.LogoRelPath) ? string.Empty : $"<img src=\"../{run.Brand.LogoRelPath}\" alt=\"Logo\" class=\"logo\"/>")
            .Replace("{{device}}", $"{run.Device.Model} / {run.Device.Cpu} / {run.Device.Gpu}")
            .Replace("{{tag}}", run.Session.Tag)
            .Replace("{{status}}", run.Session.Status)
            .Replace("{{started}}", run.Session.StartedAt.ToLocalTime().ToString("g"))
            .Replace("{{ended}}", run.Session.EndedAt.ToLocalTime().ToString("g"))
            .Replace("{{duration}}", (run.Session.EndedAt - run.Session.StartedAt).ToString())
            .Replace("{{cpuTempMax}}", FormatNumber(run.Telemetry.Peaks.CpuTempMax, "F1"))
            .Replace("{{gpuTempMax}}", FormatNumber(run.Telemetry.Peaks.GpuTempMax, "F1"))
            .Replace("{{cpuFanMax}}", FormatNumber(run.Telemetry.Peaks.CpuFanMax, "F0"))
            .Replace("{{gpuFanMax}}", FormatNumber(run.Telemetry.Peaks.GpuFanMax, "F0"))
            .Replace("{{gpuFanPctMax}}", FormatNumber(run.Telemetry.Peaks.GpuFanPctMax, "F0"))
            .Replace("{{cpuFreqAvg}}", FormatNumber(run.Telemetry.Peaks.CpuFreqAvg, "F0"))
            .Replace("{{gpuCoreAvg}}", FormatNumber(run.Telemetry.Peaks.GpuCoreAvg, "F0"))
            .Replace("{{gpuMemAvg}}", FormatNumber(run.Telemetry.Peaks.GpuMemAvg, "F0"))
            .Replace("{{notes}}", BuildNotesHtml(run.Notes));

        File.WriteAllText(htmlPath, html, Encoding.UTF8);

        var logBuilder = new StringBuilder();
        if (run.Notes != null)
        {
            foreach (var note in run.Notes)
            {
                logBuilder.AppendLine(note);
            }
        }
        foreach (var sample in run.Telemetry.Timeline)
        {
            logBuilder.AppendLine(string.Join(';', new[]
            {
                run.Session.StartedAt.AddSeconds(sample.T).ToString("yyyy-MM-dd HH:mm:ss"),
                sample.Phase ?? string.Empty,
                FormatNullable(sample.CpuT),
                FormatNullable(sample.GpuT),
                FormatNullable(sample.CpuRpm),
                FormatNullable(sample.GpuRpm),
                FormatNullable(sample.CpuMHz),
                FormatNullable(sample.GpuCore),
                FormatNullable(sample.GpuMem),
                sample.TestName ?? string.Empty,
                sample.Status ?? run.Session.Status
            }));
        }
        File.WriteAllText(logPath, logBuilder.ToString(), Encoding.UTF8);
    }

    public void GenerateIndex(DeviceInfo device)
    {
        var deviceRoot = FileManager.BuildDeviceRoot(device);
        var runsFolder = Path.Combine(deviceRoot, "runs");
        Directory.CreateDirectory(runsFolder);
        var template = LoadAsset("index-template.html");
        var items = new StringBuilder();
        var runs = new List<RunJson>();
        var orderedRuns = new List<(RunJson Run, string FolderName, DateTime StartedAt)>();
        foreach (var runDir in Directory.GetDirectories(runsFolder).OrderByDescending(d => d))
        {
            var jsonPath = Path.Combine(runDir, "run.json");
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
                var run = JsonSerializer.Deserialize<RunJson>(json);
                if (run == null)
                {
                    continue;
                }

                var runName = Path.GetFileName(runDir);
                orderedRuns.Add((run, runName, run.Session.StartedAt));
                runs.Add(run);
            }
            catch
            {
                // ignore malformed entries
            }
        }

        foreach (var info in orderedRuns.OrderByDescending(r => r.StartedAt))
        {
            items.AppendLine($"<li><a href=\"runs/{info.FolderName}/run.html\">{info.FolderName}</a> — {info.Run.Session.Status} — CPU max {info.Run.Telemetry.Peaks.CpuTempMax:F1} °C / GPU max {info.Run.Telemetry.Peaks.GpuTempMax:F1} °C</li>");
        }

        var summaryHtml = BuildSummaryHtml(runs);

        var html = template
            .Replace("{{summary}}", summaryHtml)
            .Replace("{{runs}}", items.ToString());
        File.WriteAllText(Path.Combine(deviceRoot, "index.html"), html, Encoding.UTF8);
    }

    private void CopyAsset(string assetName, string destination)
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", assetName);
        if (File.Exists(assetPath))
        {
            File.Copy(assetPath, Path.Combine(destination, assetName), overwrite: true);
        }
    }

    private string LoadAsset(string assetName)
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", assetName);
        return File.Exists(assetPath) ? File.ReadAllText(assetPath, Encoding.UTF8) : string.Empty;
    }

    private static string FormatNullable(double? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        var number = value.Value;
        var format = Math.Abs(number % 1) < double.Epsilon ? "F0" : "F1";
        return number.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(double value, string format)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "—";
        }

        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string BuildNotesHtml(IReadOnlyCollection<string>? notes)
    {
        if (notes == null || notes.Count == 0)
        {
            return "<p>Заметок нет.</p>";
        }

        var sb = new StringBuilder();
        sb.AppendLine("<ul class=\"notes\">");
        foreach (var note in notes)
        {
            sb.Append("<li>");
            sb.Append(System.Net.WebUtility.HtmlEncode(note));
            sb.AppendLine("</li>");
        }

        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    private static string BuildSummaryHtml(IReadOnlyCollection<RunJson> runs)
    {
        if (runs.Count == 0)
        {
            return "<p>Пока нет завершённых прогонов.</p>";
        }

        var before = runs
            .Where(r => string.Equals(r.Session.Tag, "before", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Session.StartedAt)
            .FirstOrDefault();
        var after = runs
            .Where(r => string.Equals(r.Session.Tag, "after", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Session.StartedAt)
            .FirstOrDefault();

        if (before == null || after == null)
        {
            return "<p>Для сравнения before/after требуется минимум два прогона с соответствующими тегами.</p>";
        }

        double Delta(double a, double b) => b - a;

        string FormatDelta(double beforeValue, double afterValue, string unit)
        {
            var delta = Delta(beforeValue, afterValue);
            var sign = delta > 0 ? "+" : string.Empty;
            return $"{afterValue:F1}{unit} ({sign}{delta:F1}{unit})";
        }

        var sb = new StringBuilder();
        sb.AppendLine("<table class=\"comparison\"><thead><tr><th>Метрика</th><th>before</th><th>after</th></tr></thead><tbody>");
        sb.AppendLine($"<tr><td>Макс. CPU °C</td><td>{before.Telemetry.Peaks.CpuTempMax:F1}°C</td><td>{FormatDelta(before.Telemetry.Peaks.CpuTempMax, after.Telemetry.Peaks.CpuTempMax, "°C")}</td></tr>");
        sb.AppendLine($"<tr><td>Макс. GPU °C</td><td>{before.Telemetry.Peaks.GpuTempMax:F1}°C</td><td>{FormatDelta(before.Telemetry.Peaks.GpuTempMax, after.Telemetry.Peaks.GpuTempMax, "°C")}</td></tr>");
        sb.AppendLine($"<tr><td>Сред. CPU MHz</td><td>{before.Telemetry.Peaks.CpuFreqAvg:F0} MHz</td><td>{FormatDelta(before.Telemetry.Peaks.CpuFreqAvg, after.Telemetry.Peaks.CpuFreqAvg, " MHz")}</td></tr>");
        sb.AppendLine($"<tr><td>Сред. GPU Core MHz</td><td>{before.Telemetry.Peaks.GpuCoreAvg:F0} MHz</td><td>{FormatDelta(before.Telemetry.Peaks.GpuCoreAvg, after.Telemetry.Peaks.GpuCoreAvg, " MHz")}</td></tr>");
        sb.AppendLine($"<tr><td>Макс. GPU Fan RPM</td><td>{before.Telemetry.Peaks.GpuFanMax:F0} RPM</td><td>{FormatDelta(before.Telemetry.Peaks.GpuFanMax, after.Telemetry.Peaks.GpuFanMax, " RPM")}</td></tr>");
        sb.AppendLine("</tbody></table>");

        return sb.ToString();
    }
}
