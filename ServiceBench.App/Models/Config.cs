using System;
using System.IO;
using System.Text.Json;
using ServiceBench.App.Services;

namespace ServiceBench.App.Models;

public class Config
{
    public const string TestFolderName = "Test";
    public const int MaxRootLevels = 12;
    public const string DefaultSensorWindowTitle = "AIDA64";

    public string RootBase { get; private set; }
        = AppContext.BaseDirectory;

    public string AidaPath { get; private set; } = string.Empty;
    public string OcctPath { get; private set; } = string.Empty;
    public string FurmarkPath { get; private set; } = string.Empty;

    public bool SaveAidaCsv { get; set; }
        = false;

    public bool CompressScreenshots { get; set; }
        = true;

    public double DesktopCpuLimit { get; set; } = 100.0;
    public double LaptopCpuLimit { get; set; } = 103.0;
    public double GpuLimit { get; set; } = 100.0;

    public Branding Branding { get; private set; } = new();

    public AppSettings CurrentSettings { get; private set; } = new();

    public void Initialize(SettingsService settingsService)
    {
        RootBase = FileManager.FindRootBase(AppContext.BaseDirectory);
        CurrentSettings = settingsService.Load();
        ApplySettings(CurrentSettings);
    }

    public void ApplySettings(AppSettings settings)
    {
        CurrentSettings = settings;
        Branding = settings.Brand;
        AidaPath = ResolveToolPath(settings.AidaPath, Path.Combine(RootBase, "Test", "AIDA64", "aida64.exe"));
        OcctPath = ResolveToolPath(settings.OcctPath, Path.Combine(RootBase, "Test", "OCCT.exe"));
        var defaultFurmark = Path.Combine(RootBase, "Test", "FurMark", "FurMark_GUI.exe");
        var resolvedFurmark = ResolveToolPath(settings.FurmarkPath, defaultFurmark);
        FurmarkSwitchedToGui = false;
        FurmarkOriginalCliPath = null;

        if (resolvedFurmark.EndsWith("FurMark.exe", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(resolvedFurmark) ?? RootBase;
            var guiCandidate = Path.Combine(dir, "FurMark_GUI.exe");
            if (File.Exists(guiCandidate))
            {
                FurmarkOriginalCliPath = resolvedFurmark;
                resolvedFurmark = guiCandidate;
                FurmarkSwitchedToGui = true;
            }
        }

        FurmarkPath = resolvedFurmark;
    }

    private static string ResolveToolPath(string? overridePath, string defaultPath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        return defaultPath;
    }

    public bool FurmarkSwitchedToGui { get; private set; }

    public string? FurmarkOriginalCliPath { get; private set; }

    public string ToJson() => JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions
    {
        WriteIndented = true
    });
}
