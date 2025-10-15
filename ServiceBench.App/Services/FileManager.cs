using System;
using System.IO;
using System.Linq;
using ServiceBench.App.Models;
using ServiceBench.App.Utilities;

namespace ServiceBench.App.Services;

public static class FileManager
{
    public static string FindRootBase(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        for (int i = 0; i < Config.MaxRootLevels && directory != null; i++)
        {
            if (directory.GetDirectories().Any(d => d.Name.Equals(Config.TestFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return startPath;
    }

    public static string BuildDeviceRoot(DeviceInfo device)
    {
        var rootBase = FindRootBase(AppContext.BaseDirectory);
        var root = Path.Combine(rootBase, "Test", "results", device.Slug);
        Directory.CreateDirectory(root);
        return root;
    }

    public static string BuildRunFolder(DeviceInfo device, string tag, DateTime startedAt)
    {
        var rootBase = FindRootBase(AppContext.BaseDirectory);
        var deviceRoot = Path.Combine(rootBase, "Test", "results", device.Slug);
        Directory.CreateDirectory(deviceRoot);
        var sanitizedTag = SlugHelper.Slugify(tag);
        var runFolder = Path.Combine(deviceRoot, "runs", $"{startedAt:yyyy-MM-ddTHH-mm}_{sanitizedTag}");
        Directory.CreateDirectory(runFolder);
        Directory.CreateDirectory(Path.Combine(runFolder, "screenshots"));
        Directory.CreateDirectory(Path.Combine(deviceRoot, "assets"));
        return runFolder;
    }

    public static string DeviceDashboardPath(DeviceInfo device)
    {
        var rootBase = FindRootBase(AppContext.BaseDirectory);
        var deviceRoot = Path.Combine(rootBase, "Test", "results", device.Slug);
        return Path.Combine(deviceRoot, "index.html");
    }

    public static string RunsFolder(DeviceInfo device)
    {
        var rootBase = FindRootBase(AppContext.BaseDirectory);
        return Path.Combine(rootBase, "Test", "results", device.Slug, "runs");
    }
}
