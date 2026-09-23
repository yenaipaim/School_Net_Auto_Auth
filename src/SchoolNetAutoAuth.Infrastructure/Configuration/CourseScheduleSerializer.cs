using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class CourseScheduleSerializer
{
    private static readonly string[] CsvHeaders =
    [
        "Name",
        "Teacher",
        "Location",
        "Weekday",
        "StartTime",
        "EndTime",
        "FirstWeek",
        "LastWeek",
        "Parity"
    ];

    public string SerializeJson(CourseSchedule schedule) =>
        JsonSerializer.Serialize(schedule, JsonCourseScheduleStore.JsonOptions);

    public CourseSchedule DeserializeJson(string content)
    {
        var schedule = JsonSerializer.Deserialize<CourseSchedule>(content, JsonCourseScheduleStore.JsonOptions)
            ?? throw new InvalidDataException("JSON 课程表内容为空。");
        if (schedule.SchemaVersion > CourseSchedule.CurrentSchemaVersion)
            throw new InvalidDataException("课程表版本高于当前程序支持版本。");
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    public string SerializeCsv(CourseSchedule schedule)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', CsvHeaders.Select(EscapeCsv)));
        foreach (var course in schedule.Courses.OrderBy(course => course.Weekday).ThenBy(course => course.StartTime))
        {
            builder.AppendLine(string.Join(',',
                EscapeCsv(course.Name),
                EscapeCsv(course.Teacher),
                EscapeCsv(course.Location),
                EscapeCsv(course.Weekday.ToString()),
                EscapeCsv(course.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
                EscapeCsv(course.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
                course.FirstWeek.ToString(CultureInfo.InvariantCulture),
                course.LastWeek.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(course.Parity.ToString())));
        }
        return builder.ToString();
    }

    public CourseSchedule DeserializeCsv(string content, DateOnly semesterStartDate)
    {
        using var parser = new TextFieldParser(new StringReader(content))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
        };
        parser.SetDelimiters(",");
        if (parser.EndOfData) throw new InvalidDataException("CSV 文件为空。");

        var headers = parser.ReadFields() ?? throw new InvalidDataException("CSV 表头无效。");
        var headerMap = headers
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var courses = new List<CourseEntry>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            courses.Add(ParseCsvCourse(fields, headerMap));
        }
        if (courses.Count == 0) throw new InvalidDataException("CSV 中没有课程。");

        var schedule = new CourseSchedule(CourseSchedule.CurrentSchemaVersion, semesterStartDate, courses);
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    public string SerializeIcs(CourseSchedule schedule)
    {
        var builder = new StringBuilder();
        builder.Append("BEGIN:VCALENDAR\r\n");
        builder.Append("VERSION:2.0\r\n");
        builder.Append("PRODID:-//SchoolNetAutoAuth//Course Schedule//ZH-CN\r\n");
        builder.Append("CALSCALE:GREGORIAN\r\n");

        foreach (var course in schedule.Courses.OrderBy(course => course.Weekday).ThenBy(course => course.StartTime))
        {
            var firstDate = DateForWeekday(schedule.SemesterStartDate, course.FirstWeek, course.Weekday);
            var lastDate = DateForWeekday(schedule.SemesterStartDate, course.LastWeek, course.Weekday);
            builder.Append("BEGIN:VEVENT\r\n");
            builder.Append("UID:").Append(course.Id.ToString("N")).Append("@school-net-auto-auth\r\n");
            builder.Append("DTSTAMP:").Append(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)).Append("\r\n");
            builder.Append("DTSTART:").Append(FormatIcsDateTime(firstDate, course.StartTime)).Append("\r\n");
            builder.Append("DTEND:").Append(FormatIcsDateTime(firstDate, course.EndTime)).Append("\r\n");
            builder.Append("RRULE:FREQ=WEEKLY;BYDAY=").Append(ToIcsWeekday(course.Weekday))
                .Append(";UNTIL=").Append(lastDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append("T235959\r\n");
            builder.Append("SUMMARY:").Append(EscapeIcs(course.Name)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(course.Location))
                builder.Append("LOCATION:").Append(EscapeIcs(course.Location)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(course.Teacher))
                builder.Append("DESCRIPTION:教师：").Append(EscapeIcs(course.Teacher)).Append("\r\n");
            builder.Append("END:VEVENT\r\n");
        }

        builder.Append("END:VCALENDAR\r\n");
        return builder.ToString();
    }

    public CourseSchedule DeserializeIcs(string content)
    {
        var events = ParseIcsEvents(content);
        if (events.Count == 0) throw new InvalidDataException("ICS 中没有课程事件。");

        var parsedEvents = events.Select(ParseIcsEvent).ToArray();
        var semesterStart = parsedEvents.Min(item => item.StartDate);
        var courses = new List<CourseEntry>();
        foreach (var item in parsedEvents)
        {
            var firstWeek = WeekNumber(semesterStart, item.StartDate);
            var lastWeek = item.UntilDate is null
                ? Math.Min(firstWeek + 51, 52)
                : Math.Max(firstWeek, WeekNumber(semesterStart, item.UntilDate.Value));
            foreach (var weekday in item.Weekdays)
            {
                courses.Add(new(
                    Guid.NewGuid(),
                    item.Name,
                    item.Teacher,
                    item.Location,
                    weekday,
                    item.StartTime,
                    item.EndTime,
                    firstWeek,
                    lastWeek,
                    WeekParity.All));
            }
        }

        var schedule = new CourseSchedule(CourseSchedule.CurrentSchemaVersion, semesterStart, courses);
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    private static CourseEntry ParseCsvCourse(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> headers)
    {
        var name = GetField(fields, headers, "Name");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("CSV 课程名称不能为空。");
        var weekday = ParseEnum<DayOfWeek>(GetField(fields, headers, "Weekday"), "星期");
        var parity = headers.ContainsKey("Parity")
            ? ParseEnum<WeekParity>(GetField(fields, headers, "Parity"), "单双周")
            : WeekParity.All;
        if (!TimeOnly.TryParse(GetField(fields, headers, "StartTime"), CultureInfo.InvariantCulture, out var start))
            throw new InvalidDataException($"课程“{name}”的开始时间无效。");
        if (!TimeOnly.TryParse(GetField(fields, headers, "EndTime"), CultureInfo.InvariantCulture, out var end))
            throw new InvalidDataException($"课程“{name}”的结束时间无效。");
        if (!int.TryParse(GetField(fields, headers, "FirstWeek"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstWeek))
            throw new InvalidDataException($"课程“{name}”的起始周无效。");
        if (!int.TryParse(GetField(fields, headers, "LastWeek"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastWeek))
            throw new InvalidDataException($"课程“{name}”的结束周无效。");

        return new(
            Guid.NewGuid(),
            name.Trim(),
            GetOptionalField(fields, headers, "Teacher"),
            GetOptionalField(fields, headers, "Location"),
            weekday,
            start,
            end,
            firstWeek,
            lastWeek,
            parity);
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> headers,
        string name)
    {
        if (!headers.TryGetValue(name, out var index) || index >= fields.Count)
            throw new InvalidDataException($"CSV 缺少“{name}”列。");
        return fields[index].Trim();
    }

    private static string GetOptionalField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> headers,
        string name)
    {
        if (!headers.TryGetValue(name, out var index) || index >= fields.Count) return string.Empty;
        return fields[index].Trim();
    }

    private static T ParseEnum<T>(string value, string label) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"{label}“{value}”无效。");

    private static string EscapeCsv(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\r') && !value.Contains('\n'))
            return value;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static List<Dictionary<string, string>> ParseIcsEvents(string content)
    {
        var events = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (var line in UnfoldIcsLines(content))
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null) events.Add(current);
                current = null;
                continue;
            }
            if (current is null) continue;

            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            var key = line[..separator].Split(';', 2)[0].Trim();
            current[key] = line[(separator + 1)..].Trim();
        }
        return events;
    }

    private static IEnumerable<string> UnfoldIcsLines(string content)
    {
        string? previous = null;
        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if (rawLine.StartsWith(' ') || rawLine.StartsWith('\t'))
            {
                previous += rawLine[1..];
                continue;
            }
            if (previous is not null) yield return previous;
            previous = rawLine;
        }
        if (previous is not null) yield return previous;
    }

    private static ParsedIcsEvent ParseIcsEvent(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("DTSTART", out var startValue) || !TryParseIcsDateTime(startValue, out var startDate, out var startTime))
            throw new InvalidDataException("ICS 事件缺少有效的开始时间。");
        if (!values.TryGetValue("DTEND", out var endValue) || !TryParseIcsDateTime(endValue, out _, out var endTime))
            throw new InvalidDataException("ICS 事件缺少有效的结束时间。");
        if (!values.TryGetValue("SUMMARY", out var summary) || string.IsNullOrWhiteSpace(summary))
            throw new InvalidDataException("ICS 事件缺少课程名称。");

        var weekdays = ParseIcsWeekdays(values.GetValueOrDefault("RRULE"), startDate.DayOfWeek);
        var untilDate = ParseIcsUntil(values.GetValueOrDefault("RRULE"));
        var teacher = string.Empty;
        if (values.TryGetValue("DESCRIPTION", out var description))
        {
            var normalized = UnescapeIcs(description);
            const string teacherPrefix = "教师：";
            teacher = normalized.StartsWith(teacherPrefix, StringComparison.Ordinal)
                ? normalized[teacherPrefix.Length..].Trim()
                : string.Empty;
        }

        return new(
            UnescapeIcs(summary).Trim(),
            teacher,
            values.TryGetValue("LOCATION", out var location) ? UnescapeIcs(location).Trim() : string.Empty,
            startDate,
            startTime,
            endTime,
            weekdays,
            untilDate);
    }

    private static IReadOnlyList<DayOfWeek> ParseIcsWeekdays(string? rule, DayOfWeek defaultWeekday)
    {
        if (string.IsNullOrWhiteSpace(rule)) return [defaultWeekday];
        var byDay = rule.Split(';')
            .FirstOrDefault(part => part.StartsWith("BYDAY=", StringComparison.OrdinalIgnoreCase));
        if (byDay is null) return [defaultWeekday];

        var weekdays = byDay[6..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value[^2..] switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => throw new InvalidDataException($"ICS 星期“{value}”无效。")
            })
            .Distinct()
            .ToArray();
        return weekdays.Length == 0 ? [defaultWeekday] : weekdays;
    }

    private static DateOnly? ParseIcsUntil(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule)) return null;
        var until = rule.Split(';')
            .FirstOrDefault(part => part.StartsWith("UNTIL=", StringComparison.OrdinalIgnoreCase));
        if (until is null) return null;
        var value = until[6..].Trim().TrimEnd('Z');
        if (value.Length < 8 || !DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidDataException("ICS 重复结束日期无效。");
        return date;
    }

    private static bool TryParseIcsDateTime(string value, out DateOnly date, out TimeOnly time)
    {
        time = default;
        var normalized = value.Trim().TrimEnd('Z');
        var separator = normalized.IndexOf('T');
        if (separator != 8)
        {
            date = default;
            time = default;
            return false;
        }
        if (!DateOnly.TryParseExact(normalized[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return false;
        var timeValue = normalized[(separator + 1)..];
        if (timeValue.Length < 4) return false;
        return TimeOnly.TryParseExact(timeValue[..4], "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
            || TimeOnly.TryParseExact(timeValue[..6], "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private static string FormatIcsDateTime(DateOnly date, TimeOnly time) =>
        date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "T" + time.ToString("HHmmss", CultureInfo.InvariantCulture);

    private static string ToIcsWeekday(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Monday => "MO",
        DayOfWeek.Tuesday => "TU",
        DayOfWeek.Wednesday => "WE",
        DayOfWeek.Thursday => "TH",
        DayOfWeek.Friday => "FR",
        DayOfWeek.Saturday => "SA",
        _ => "SU"
    };

    private static string EscapeIcs(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string UnescapeIcs(string value) => value
        .Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase)
        .Replace("\\,", ",", StringComparison.Ordinal)
        .Replace("\\;", ";", StringComparison.Ordinal)
        .Replace("\\\\", "\\", StringComparison.Ordinal);

    private static DateOnly DateForWeekday(DateOnly semesterStartDate, int week, DayOfWeek weekday)
    {
        var monday = CourseSchedule.StartOfWeek(semesterStartDate).AddDays((week - 1) * 7);
        var offset = ((int)weekday + 6) % 7;
        return monday.AddDays(offset);
    }

    private static int WeekNumber(DateOnly semesterStartDate, DateOnly date)
    {
        var semesterMonday = CourseSchedule.StartOfWeek(semesterStartDate);
        var dateMonday = CourseSchedule.StartOfWeek(date);
        return ((dateMonday.DayNumber - semesterMonday.DayNumber) / 7) + 1;
    }

    private sealed record ParsedIcsEvent(
        string Name,
        string Teacher,
        string Location,
        DateOnly StartDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<DayOfWeek> Weekdays,
        DateOnly? UntilDate);
}
