using System;
using System.IO;
using System.Linq;
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
            .Replace("{{duration}}", (run.Session.EndedAt - run.Session.StartedAt).ToString());

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
            logBuilder.AppendLine($"{run.Session.StartedAt.AddSeconds(sample.T):yyyy-MM-dd HH:mm:ss};RUN;{sample.CpuT};{sample.GpuT};{sample.CpuRpm};{sample.GpuRpm};{sample.CpuMHz};{sample.GpuCore};{sample.GpuMem};{run.Session.Tag};{run.Session.Status}");
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
                items.AppendLine($"<li><a href=\"runs/{runName}/run.html\">{runName}</a> — {run.Session.Status} — CPU max {run.Telemetry.Peaks.CpuTempMax:F1} °C / GPU max {run.Telemetry.Peaks.GpuTempMax:F1} °C</li>");
            }
            catch
            {
                // ignore malformed entries
            }
        }

        var html = template.Replace("{{runs}}", items.ToString());
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
}
