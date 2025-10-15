using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using ServiceBench.App.Models;

namespace ServiceBench.App.Services;

public class DeviceDetector
{
    public DeviceInfo Detect()
    {
        var type = DetectDeviceType();
        var model = GetComputerModel();
        var cpu = GetCpuName();
        var gpu = GetGpuName(type);
        return new DeviceInfo(type, model, cpu, gpu);
    }

    private static DeviceType DetectDeviceType()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            using var collection = searcher.Get();
            if (collection.Count > 0)
            {
                return DeviceType.Laptop;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemEnclosure");
            foreach (ManagementObject obj in searcher.Get())
            {
                var chassisTypes = obj["ChassisTypes"] as ushort[];
                if (chassisTypes != null && chassisTypes.Any(t => t is 8 or 9 or 10 or 14))
                {
                    return DeviceType.Laptop;
                }
            }
        }
        catch
        {
            // ignore
        }

        return DeviceType.Desktop;
    }

    private static string GetComputerModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToString(obj["Model"]) ?? "Unknown";
            }
        }
        catch
        {
            // ignore
        }
        return "Unknown";
    }

    private static string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToString(obj["Name"]) ?? "CPU";
            }
        }
        catch
        {
            // ignore
        }
        return "CPU";
    }

    private static string GetGpuName(DeviceType deviceType)
    {
        static bool IsDiscrete(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var lowered = name.ToLowerInvariant();
            if (lowered.Contains("nvidia") || lowered.Contains("geforce") || lowered.Contains("quadro"))
            {
                return true;
            }

            if (lowered.Contains("amd") || lowered.Contains("radeon") || lowered.Contains("rx ") || lowered.Contains("rtx"))
            {
                return true;
            }

            if (lowered.Contains("arc"))
            {
                return true;
            }

            return false;
        }

        try
        {
            var controllers = new System.Collections.Generic.List<string>();
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = Convert.ToString(obj["Name"]);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    controllers.Add(name);
                }
            }

            if (controllers.Count == 0)
            {
                return "GPU";
            }

            var discrete = controllers.FirstOrDefault(IsDiscrete);
            if (!string.IsNullOrWhiteSpace(discrete))
            {
                return discrete;
            }

            if (deviceType == DeviceType.Laptop)
            {
                return controllers[0];
            }

            // for desktops prefer any non Intel adapter if available
            var nonIntel = controllers.FirstOrDefault(name => !name.Contains("Intel", StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrWhiteSpace(nonIntel) ? nonIntel! : controllers[0];
        }
        catch
        {
            // ignore
        }
        return "GPU";
    }
}
