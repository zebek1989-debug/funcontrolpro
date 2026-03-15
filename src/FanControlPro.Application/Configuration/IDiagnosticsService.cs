namespace FanControlPro.Application.Configuration;

public interface IDiagnosticsService
{
    Task<IReadOnlyList<DiagnosticEvent>> GetRecentEventsAsync(
        int maxEvents = 50,
        CancellationToken cancellationToken = default);
}
