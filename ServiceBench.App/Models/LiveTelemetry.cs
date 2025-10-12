using ServiceBench.App.ViewModels;

namespace ServiceBench.App.Models;

public class LiveTelemetry : ViewModelBase
{
    private double _cpuTemp;
    private double _gpuTemp;
    private double _cpuFreq;
    private double _gpuCore;
    private double _gpuMem;
    private double _cpuFan;
    private double _gpuFan;

    public double CpuTemp
    {
        get => _cpuTemp;
        set => SetField(ref _cpuTemp, value);
    }

    public double GpuTemp
    {
        get => _gpuTemp;
        set => SetField(ref _gpuTemp, value);
    }

    public double CpuFreq
    {
        get => _cpuFreq;
        set => SetField(ref _cpuFreq, value);
    }

    public double GpuCore
    {
        get => _gpuCore;
        set => SetField(ref _gpuCore, value);
    }

    public double GpuMem
    {
        get => _gpuMem;
        set => SetField(ref _gpuMem, value);
    }

    public double CpuFan
    {
        get => _cpuFan;
        set => SetField(ref _cpuFan, value);
    }

    public double GpuFan
    {
        get => _gpuFan;
        set => SetField(ref _gpuFan, value);
    }
}
