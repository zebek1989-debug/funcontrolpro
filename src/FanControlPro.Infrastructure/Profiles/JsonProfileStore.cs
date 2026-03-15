using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Domain.FanControl.Profiles;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Profiles;

public sealed class JsonProfileStore : IProfileStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptions<ProfileStorageOptions> _options;
    private readonly IBackupServiceV2? _backupService;

    public JsonProfileStore(
        IOptions<ProfileStorageOptions> options,
        IBackupServiceV2? backupService = null)
    {
        _options = options;
        _backupService = backupService;
    }

    public async Task<IReadOnlyList<FanProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profilesDirectory = ResolvePath(_options.Value.ProfilesDirectoryPath);
        if (!Directory.Exists(profilesDirectory))
        {
            return Array.Empty<FanProfile>();
        }

        var files = Directory.GetFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profiles = new List<FanProfile>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var profile = await DeserializeProfileAsync(file, cancellationToken).ConfigureAwait(false);

            if (profile is not null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task SaveCustomProfileAsync(FanProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_backupService is not null)
        {
            var backupCreated = await _backupService.CreateAtomicBackupAsync(cancellationToken).ConfigureAwait(false);
            if (!backupCreated)
            {
                throw new InvalidOperationException("Could not create atomic backup before saving profile.");
            }
        }

        var profilesDirectory = ResolvePath(_options.Value.ProfilesDirectoryPath);
        Directory.CreateDirectory(profilesDirectory);

        var safeName = ToSafeFileName(profile.Name);
        var filePath = Path.Combine(profilesDirectory, safeName + ".json");

        await using var stream = File.Create(filePath);
        var versioned = new VersionedDocument<FanProfile>(
            SchemaVersion: CurrentSchemaVersion,
            Payload: profile);

        await JsonSerializer.SerializeAsync(stream, versioned, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> LoadActiveProfileNameAsync(CancellationToken cancellationToken = default)
    {
        var activePath = ResolvePath(_options.Value.ActiveProfilePath);
        if (!File.Exists(activePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(activePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (TryDeserializeVersioned(document.RootElement, out ActiveProfilePayload? versioned))
        {
            return versioned?.ProfileName;
        }

        var legacy = document.RootElement.Deserialize<ActiveProfilePayload>(SerializerOptions);
        return legacy?.ProfileName;
    }

    public async Task SaveActiveProfileNameAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));
        }

        if (_backupService is not null)
        {
            var backupCreated = await _backupService.CreateAtomicBackupAsync(cancellationToken).ConfigureAwait(false);
            if (!backupCreated)
            {
                throw new InvalidOperationException("Could not create atomic backup before saving active profile.");
            }
        }

        var activePath = ResolvePath(_options.Value.ActiveProfilePath);
        var directory = Path.GetDirectoryName(activePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(activePath);
        var payload = new ActiveProfilePayload(profileName.Trim());
        var versioned = new VersionedDocument<ActiveProfilePayload>(
            SchemaVersion: CurrentSchemaVersion,
            Payload: payload);

        await JsonSerializer.SerializeAsync(stream, versioned, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string ToSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeChars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(safeChars);
    }

    private static string ResolvePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = Path.Combine("data", "profiles");
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private static async Task<FanProfile?> DeserializeProfileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (TryDeserializeVersioned(document.RootElement, out FanProfile? payload))
        {
            return payload;
        }

        return document.RootElement.Deserialize<FanProfile>(SerializerOptions);
    }

    private static bool TryDeserializeVersioned<TPayload>(JsonElement root, out TPayload? payload)
    {
        payload = default;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("schemaVersion", out _))
        {
            return false;
        }

        if (!root.TryGetProperty("payload", out var payloadElement))
        {
            return false;
        }

        payload = payloadElement.Deserialize<TPayload>(SerializerOptions);
        return payload is not null;
    }

    private sealed record ActiveProfilePayload(string ProfileName);

    private sealed record VersionedDocument<TPayload>(int SchemaVersion, TPayload Payload);
}
