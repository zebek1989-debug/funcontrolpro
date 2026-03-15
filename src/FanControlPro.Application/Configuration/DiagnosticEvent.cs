namespace FanControlPro.Application.Configuration;

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string Level,
    string Message);
