using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.FanControl;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Onboarding;

public sealed class JsonControlConsentStore : IControlConsentStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptions<ControlOnboardingOptions> _options;
    private readonly IBackupServiceV2? _backupService;

    public JsonControlConsentStore(
        IOptions<ControlOnboardingOptions> options,
        IBackupServiceV2? backupService = null)
    {
        _options = options;
        _backupService = backupService;
    }

    public async Task<ControlOnboardingState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var filePath = ResolvePath();
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (TryDeserializeVersioned(document.RootElement, out var payload))
        {
            return payload;
        }

        return document.RootElement.Deserialize<ControlOnboardingState>(SerializerOptions);
    }

    public async Task SaveAsync(ControlOnboardingState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var filePath = ResolvePath();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (_backupService is not null)
        {
            var backupCreated = await _backupService.CreateAtomicBackupAsync(cancellationToken).ConfigureAwait(false);
            if (!backupCreated)
            {
                throw new InvalidOperationException("Could not create atomic backup before saving control consent.");
            }
        }

        await using var stream = File.Create(filePath);
        var document = new VersionedDocument<ControlOnboardingState>(
            SchemaVersion: CurrentSchemaVersion,
            Payload: state);

        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private string ResolvePath()
    {
        var configured = _options.Value.ConsentFilePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Path.Combine("data", "control-consent.json");
        }

        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    private static bool TryDeserializeVersioned(JsonElement root, out ControlOnboardingState? state)
    {
        state = null;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("schemaVersion", out _))
        {
            return false;
        }

        if (!root.TryGetProperty("payload", out var payload))
        {
            return false;
        }

        state = payload.Deserialize<ControlOnboardingState>(SerializerOptions);
        return state is not null;
    }

    private sealed record VersionedDocument<TPayload>(int SchemaVersion, TPayload Payload);
}
