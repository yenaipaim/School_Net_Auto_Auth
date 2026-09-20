using System.Text.Json;
using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class RecordingConfigurationSerializer
{
    public const string Format = "SchoolNetAutoAuth.Recording";
    public const int Version = 1;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string Serialize(RecordedClickSequence sequence)
    {
        ValidateSequence(sequence);
        return JsonSerializer.Serialize(new RecordingConfigurationFile(Format, Version, DateTimeOffset.UtcNow, sequence), Options);
    }

    public RecordedClickSequence Deserialize(string json)
    {
        RecordingConfigurationFile file;
        try { file = JsonSerializer.Deserialize<RecordingConfigurationFile>(json, Options) ?? throw new InvalidDataException("录制配置为空。"); }
        catch (JsonException ex) { throw new InvalidDataException("录制配置不是有效的 JSON。", ex); }
        if (!string.Equals(file.Format, Format, StringComparison.Ordinal) || file.Version != Version)
            throw new InvalidDataException("不支持的录制配置格式或版本。");
        var errors = Validate(file);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return file.RecordedSequence;
    }

    public IReadOnlyList<string> Validate(RecordingConfigurationFile file)
    {
        var errors = new List<string>();
        if (file is null) errors.Add("录制配置为空。");
        else
        {
            if (!string.Equals(file.Format, Format, StringComparison.Ordinal)) errors.Add("录制配置格式无效。");
            if (file.Version != Version) errors.Add("录制配置版本无效。");
            try { ValidateSequence(file.RecordedSequence); } catch (InvalidDataException ex) { errors.Add(ex.Message); }
        }
        return errors;
    }

    private static void ValidateSequence(RecordedClickSequence sequence)
    {
        if (sequence is null || sequence.Credentials is null || sequence.Credentials.Username is null || sequence.Credentials.Password is null)
            throw new InvalidDataException("录制配置缺少账号或密码定位信息。");
        if (sequence.Clicks is null || sequence.Clicks.Count == 0)
            throw new InvalidDataException("录制配置没有点击步骤。");
        var expected = 1;
        foreach (var step in sequence.Clicks.OrderBy(x => x.Order))
        {
            if (step.Order != expected++ || string.IsNullOrWhiteSpace(step.PageKey) || string.IsNullOrWhiteSpace(step.UrlPattern) || step.Locator is null)
                throw new InvalidDataException("录制步骤顺序或定位信息无效。");
        }
    }
}
