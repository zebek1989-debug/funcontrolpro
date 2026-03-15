using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Domain.Hardware.Models;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.HardwareDetection;

public sealed class HardwareJsonCacheStore : IHardwareCacheStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptions<HardwareDetectionOptions> _options;
    private readonly IBackupServiceV2? _backupService;

    public HardwareJsonCacheStore(
        IOptions<HardwareDetectionOptions> options,
        IBackupServiceV2? backupService = null)
    {
        _options = options;
        _backupService = backupService;
    }

    public async Task SaveAsync(DetectionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_backupService is not null)
        {
            var backupCreated = await _backupService.CreateAtomicBackupAsync(cancellationToken).ConfigureAwait(false);
            if (!backupCreated)
            {
                throw new InvalidOperationException("Could not create atomic backup before saving hardware cache.");
            }
        }

        var cachePath = ResolveCachePath();
        var directory = Path.GetDirectoryName(cachePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(cachePath);
        var versioned = new VersionedDocument<DetectionResult>(
            SchemaVersion: CurrentSchemaVersion,
            Payload: result);

        await JsonSerializer.SerializeAsync(stream, versioned, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DetectionResult?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cachePath = ResolveCachePath();

        if (!File.Exists(cachePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(cachePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (TryDeserializeVersioned(document.RootElement, out DetectionResult? payload))
        {
            return payload;
        }

        return document.RootElement.Deserialize<DetectionResult>(SerializerOptions);
    }

    private string ResolveCachePath()
    {
        var configuredPath = _options.Value.CachePath;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(AppContext.BaseDirectory, "data", "hardware.json");
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
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

    private sealed record VersionedDocument<TPayload>(int SchemaVersion, TPayload Payload);
}
