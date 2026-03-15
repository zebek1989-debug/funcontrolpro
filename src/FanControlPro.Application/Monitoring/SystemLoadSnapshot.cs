namespace FanControlPro.Application.Monitoring;

public sealed record SystemLoadSnapshot(
    double CpuLoadPercent,
    double GpuLoadPercent,
    double MemoryLoadPercent);
