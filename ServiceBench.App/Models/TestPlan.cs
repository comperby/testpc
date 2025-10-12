namespace ServiceBench.App.Models;

public class TestPlan
{
    public bool UseAida { get; set; } = true;
    public bool UseOcct { get; set; } = true;
    public bool UseFurmark { get; set; } = true;

    public bool AidaCpu { get; set; } = true;
    public bool AidaFpu { get; set; } = true;
    public bool AidaCache { get; set; }
    public bool AidaMemory { get; set; }
    public bool AidaDisk { get; set; }
    public bool AidaGpu { get; set; }
    public int AidaDurationMinutes { get; set; } = 2;

    public bool OcctCpuSmall { get; set; } = true;
    public int OcctCpuMinutes { get; set; } = 5;
    public bool OcctGpu3D { get; set; } = true;
    public int OcctGpuMinutes { get; set; } = 5;
    public bool OcctVram { get; set; } = true;
    public int OcctVramMinutes { get; set; } = 5;

    public string SelectedFurmarkPreset { get; set; } = "1920x1080";
    public bool FurmarkCustom { get; set; }
    public int FurmarkWidth { get; set; } = 1920;
    public int FurmarkHeight { get; set; } = 1080;
    public bool FurmarkFullscreen { get; set; }
    public int FurmarkMinutes { get; set; } = 2;

    public bool SaveAidaCsv { get; set; }
    public bool CompressScreenshots { get; set; } = true;
}
