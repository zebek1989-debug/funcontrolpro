using FanControlPro.Application.Onboarding;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FanControlPro.Infrastructure.Onboarding;

public sealed class JsonOnboardingStateStore : IOnboardingStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptions<OnboardingOptions> _options;

    public JsonOnboardingStateStore(IOptions<OnboardingOptions> options)
    {
        _options = options;
    }

    public async Task<OnboardingState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var filePath = ResolvePath();

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var state = await JsonSerializer.DeserializeAsync<OnboardingState>(stream, SerializerOptions, cancellationToken);
        return state;
    }

    public async Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default)
    {
        var filePath = ResolvePath();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private string ResolvePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "FanControlPro");
        return Path.Combine(appFolder, _options.Value.StateFilePath);
    }
}