using ServiceBench.App.Utilities;

namespace ServiceBench.App.Models;

public enum DeviceType
{
    Desktop,
    Laptop
}

public record DeviceInfo(DeviceType Type, string Model, string Cpu, string Gpu)
{
    public string Slug => SlugHelper.Slugify(Type == DeviceType.Laptop ? Model : $"{Gpu} + {Cpu}");
}
