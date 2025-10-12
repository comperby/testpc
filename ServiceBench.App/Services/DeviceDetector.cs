using System;
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
        var gpu = GetGpuName();
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

    private static string GetGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = Convert.ToString(obj["Name"]);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch
        {
            // ignore
        }
        return "GPU";
    }
}
