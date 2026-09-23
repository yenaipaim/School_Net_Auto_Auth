using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class JsonCourseScheduleStore : ICourseScheduleStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _schedulePath;

    public JsonCourseScheduleStore(string rootDirectory)
    {
        var scheduleDirectory = Path.Combine(rootDirectory, "schedules");
        Directory.CreateDirectory(scheduleDirectory);
        _schedulePath = Path.Combine(scheduleDirectory, "current.json");
    }

    public async Task<CourseSchedule> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_schedulePath)) return CourseSchedule.CreateDefault();

        try
        {
            await using var stream = File.OpenRead(_schedulePath);
            var schedule = await JsonSerializer.DeserializeAsync<CourseSchedule>(
                stream,
                JsonOptions,
                cancellationToken);
            if (schedule is null || schedule.SchemaVersion > CourseSchedule.CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported course schedule schema.");
            var validation = schedule.Validate();
            if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
            return schedule;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            var directory = Path.GetDirectoryName(_schedulePath)!;
            var backup = Path.Combine(directory, $"current.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            File.Move(_schedulePath, backup, overwrite: true);
            return CourseSchedule.CreateDefault();
        }
    }

    public async Task SaveAsync(CourseSchedule schedule, CancellationToken cancellationToken)
    {
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));

        var tempPath = _schedulePath + ".tmp";
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, schedule, JsonOptions, cancellationToken);
        File.Move(tempPath, _schedulePath, overwrite: true);
    }
}
