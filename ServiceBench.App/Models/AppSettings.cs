namespace ServiceBench.App.Models;

public class AppSettings
{
    public string? AidaPath { get; set; }
        = null;

    public string? OcctPath { get; set; }
        = null;

    public string? FurmarkPath { get; set; }
        = null;

    public Branding Brand { get; set; } = new();
}
