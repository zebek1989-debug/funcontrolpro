using FanControlPro.Domain.FanControl.Profiles;

namespace FanControlPro.Application.FanControl.Profiles;

public interface IProfileStore
{
    Task<IReadOnlyList<FanProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default);

    Task SaveCustomProfileAsync(FanProfile profile, CancellationToken cancellationToken = default);

    Task<string?> LoadActiveProfileNameAsync(CancellationToken cancellationToken = default);

    Task SaveActiveProfileNameAsync(string profileName, CancellationToken cancellationToken = default);
}
