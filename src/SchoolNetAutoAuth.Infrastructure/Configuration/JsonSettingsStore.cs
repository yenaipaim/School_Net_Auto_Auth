using System.Text.Json;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _settingsPath;

    public JsonSettingsStore(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _settingsPath = Path.Combine(rootDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath)) return AppSettings.CreateDefault();
        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            if (settings is null || !settings.Validate().IsValid) throw new InvalidDataException("Invalid settings");
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            var backup = Path.Combine(Path.GetDirectoryName(_settingsPath)!, $"settings.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            File.Move(_settingsPath, backup, overwrite: true);
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var validation = settings.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        var tempPath = _settingsPath + ".tmp";
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }
}
