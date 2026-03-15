namespace FanControlPro.Application.Monitoring;

public interface ISensorReader
{
    Task<double?> ReadTemperatureAsync(string sensorId, CancellationToken cancellationToken = default);

    Task<int?> ReadFanSpeedAsync(string fanId, CancellationToken cancellationToken = default);

    Task<SystemLoadSnapshot> ReadSystemLoadAsync(CancellationToken cancellationToken = default);
}
