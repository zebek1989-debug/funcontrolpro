using FanControlPro.Domain.FanControl.Profiles;

namespace FanControlPro.Application.FanControl.Profiles;

public interface IProfileService
{
    Task<IReadOnlyList<FanProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    Task<FanProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    Task<ProfileActivationResult> ActivateProfileAsync(string name, CancellationToken cancellationToken = default);

    Task SaveProfileAsync(FanProfile profile, CancellationToken cancellationToken = default);
}
