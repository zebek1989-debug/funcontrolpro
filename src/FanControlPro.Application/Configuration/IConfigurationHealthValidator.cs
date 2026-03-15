namespace FanControlPro.Application.Configuration;

public interface IConfigurationHealthValidator
{
    Task<ConfigurationValidationResult> ValidateCurrentConfigurationAsync(CancellationToken cancellationToken = default);
}
