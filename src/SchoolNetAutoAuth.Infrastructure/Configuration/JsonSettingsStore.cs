using System.Text.Json;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

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
            if (settings is null) throw new InvalidDataException("Invalid settings");
            var migrated = settings.SchemaVersion switch
            {
                < 2 => settings with
                {
                    AutomaticAuthenticationEnabled = true,
                    BackgroundImagePath = null,
                    RecordedSequence = null
                },
                2 => settings with
                {
                    AutomaticAuthenticationEnabled = true
                },
                3 => settings with
                {
                    BackgroundImagePath = null
                },
                _ => settings
            };
            if (migrated.SchemaVersion < 5)
                migrated = migrated with { BackgroundImageOpacity = 0.45 };
            // 外观字段是后加的：无论版本号是多少，缺失/空值都要补默认，
            // 否则一个少写了字段的同版本配置会被判为无效并整份丢弃。
            migrated = migrated with
            {
                Theme = string.IsNullOrWhiteSpace(migrated.Theme) ? AppSettings.DarkTheme : migrated.Theme,
                AccentColor = string.IsNullOrWhiteSpace(migrated.AccentColor) ? AppSettings.DefaultAccentColor : migrated.AccentColor,
                InterfaceFontFamily = string.IsNullOrWhiteSpace(migrated.InterfaceFontFamily) ? null : migrated.InterfaceFontFamily
            };
            migrated = migrated with { SchemaVersion = AppSettings.CurrentSchemaVersion };
            if (!migrated.Validate().IsValid) throw new InvalidDataException("Invalid settings");
            return migrated;
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

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var tempPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // A canceled save must not hide its original error.
            }
        }
    }
}
