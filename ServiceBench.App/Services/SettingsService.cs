using System;
using System.IO;
using System.Text.Json;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var root = FileManager.FindRootBase(AppContext.BaseDirectory);
        var configDir = Path.Combine(root, "config");
        Directory.CreateDirectory(configDir);
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_settingsPath, json);
    }

    public string? TryCopyLogoToAssets(DeviceInfo device, string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            return null;
        }

        var deviceRoot = FileManager.BuildDeviceRoot(device);
        var assets = Path.Combine(deviceRoot, "assets");
        Directory.CreateDirectory(assets);
        var extension = Path.GetExtension(logoPath);
        var destFile = Path.Combine(assets, $"logo{extension}");
        File.Copy(logoPath, destFile, overwrite: true);
        return Path.Combine("assets", Path.GetFileName(destFile));
    }
}
