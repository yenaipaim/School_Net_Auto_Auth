using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.Infrastructure.Configuration;

public sealed class WordCourseScheduleImporter
{
    private const long MaximumDocumentBytes = 20 * 1024 * 1024;
    private static readonly XNamespace WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly Regex PeriodRegex = new(
        @"第\s*\d+(?:\s*-\s*\d+)?\s*节\s*\(\s*(?<start>\d{1,2}:\d{2})\s*-\s*(?<end>\d{1,2}:\d{2})\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CourseNameRegex = new(
        @"^(?<name>.+?)\[[^\]]+\]\s*(?<credits>[0-9]+(?:\.[0-9]+)?)\s*学分",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WeekRegex = new(
        @"(?<start>\d+)(?:\s*-\s*(?<end>\d+))?\s*周\s*(?:\((?<parity>单|双)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ModeRegex = new(
        @"^\s*\[[^\]]+\]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public CourseSchedule Import(Stream document, DateOnly semesterStartDate)
    {
        if (document.CanSeek && document.Length > MaximumDocumentBytes)
            throw new InvalidDataException("Word 文件过大。");

        using var archive = new ZipArchive(document, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("Word 文件缺少 document.xml。");
        using var entryStream = entry.Open();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(entryStream, settings);
        var documentXml = XDocument.Load(reader);
        var table = documentXml.Descendants(WordNamespace + "tbl").FirstOrDefault()
            ?? throw new InvalidDataException("Word 文件中没有课程表。");

        var cells = ParseTable(table);
        var header = cells
            .Where(cell => cell.StartRow >= 0)
            .GroupBy(cell => cell.StartRow)
            .OrderBy(group => group.Key)
            .Select(group => group.ToArray())
            .FirstOrDefault(row => row.Count(cell => TryParseWeekday(cell.Text, out _)) >= 2)
            ?? throw new InvalidDataException("Word 表格中找不到星期表头。");

        var dayColumns = new Dictionary<int, DayOfWeek>();
        foreach (var cell in header)
        {
            if (TryParseWeekday(cell.Text, out var weekday))
                dayColumns[cell.StartColumn] = weekday;
        }
        if (dayColumns.Count == 0)
            throw new InvalidDataException("Word 表格中找不到星期列。");

        var periodRows = new Dictionary<int, (TimeOnly Start, TimeOnly End)>();
        foreach (var cell in cells.Where(cell => cell.StartRow > header[0].StartRow))
        {
            if (!TryParsePeriod(cell.Text, out var start, out var end)) continue;
            periodRows[cell.StartRow] = (start, end);
        }
        if (periodRows.Count == 0)
            throw new InvalidDataException("Word 表格中找不到节次时间。");

        var courses = new List<CourseEntry>();
        foreach (var cell in cells)
        {
            if (cell.StartRow <= header[0].StartRow || string.IsNullOrWhiteSpace(cell.Text)) continue;
            if (!dayColumns.TryGetValue(cell.StartColumn, out var weekday)) continue;
            if (!periodRows.TryGetValue(cell.StartRow, out var startPeriod)
                || !periodRows.TryGetValue(cell.EndRow, out var endPeriod))
            {
                continue;
            }

            foreach (var parsed in ParseCourseCell(cell.Text))
            {
                foreach (var range in parsed.WeekRanges)
                {
                    courses.Add(new(
                        Guid.NewGuid(),
                        parsed.Name,
                        parsed.Teacher,
                        parsed.Location,
                        weekday,
                        startPeriod.Start,
                        endPeriod.End,
                        range.FirstWeek,
                        range.LastWeek,
                        range.Parity));
                }
            }
        }
        if (courses.Count == 0) throw new InvalidDataException("Word 表格中没有识别到课程。");

        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            semesterStartDate,
            MergeAdjacentRanges(courses));
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    private static IReadOnlyList<WordGridCell> ParseTable(XElement table)
    {
        var cells = new List<WordGridCell>();
        var activeVerticalMerges = new Dictionary<int, WordGridCell>();
        var rows = table.Elements(WordNamespace + "tr").ToArray();
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var columnIndex = 0;
            foreach (var cell in rows[rowIndex].Elements(WordNamespace + "tc"))
            {
                var properties = cell.Element(WordNamespace + "tcPr");
                var columnSpan = ParseGridSpan(properties);
                var merge = properties?.Element(WordNamespace + "vMerge");
                var mergeValue = merge?.Attribute(WordNamespace + "val")?.Value;
                var isContinue = merge is not null
                    && !string.Equals(mergeValue, "restart", StringComparison.OrdinalIgnoreCase);
                if (isContinue)
                {
                    if (activeVerticalMerges.TryGetValue(columnIndex, out var activeCell))
                        activeCell.EndRow = rowIndex;
                }
                else
                {
                    var gridCell = new WordGridCell(
                        rowIndex,
                        rowIndex,
                        columnIndex,
                        columnSpan,
                        ExtractCellText(cell));
                    cells.Add(gridCell);
                    if (merge is not null) activeVerticalMerges[columnIndex] = gridCell;
                    else activeVerticalMerges.Remove(columnIndex);
                }
                columnIndex += columnSpan;
            }
        }
        return cells;
    }

    private static int ParseGridSpan(XElement? properties)
    {
        var value = properties?.Element(WordNamespace + "gridSpan")
            ?.Attribute(WordNamespace + "val")?.Value;
        return int.TryParse(value, out var span) && span > 0 ? span : 1;
    }

    private static string ExtractCellText(XElement cell)
    {
        var builder = new StringBuilder();
        foreach (var node in cell.Descendants())
        {
            if (node.Name == WordNamespace + "t") builder.Append(node.Value);
            else if (node.Name == WordNamespace + "br") builder.Append('\n');
            else if (node.Name == WordNamespace + "tab") builder.Append('\t');
        }
        return builder.ToString().Trim();
    }

    private static IReadOnlyList<ParsedCourse> ParseCourseCell(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("选课人数", StringComparison.Ordinal))
            .ToArray();
        var courses = new List<ParsedCourse>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseCourseName(lines[index], out var name)) continue;
            var offerings = new List<string>();
            var next = index + 1;
            while (next < lines.Length && !TryParseCourseName(lines[next], out _))
            {
                if (WeekRegex.IsMatch(lines[next])) offerings.Add(lines[next]);
                next++;
            }
            foreach (var offering in offerings)
            {
                var ranges = ParseWeekRanges(offering);
                if (ranges.Count == 0) continue;
                var metadata = ParseOfferingMetadata(offering);
                courses.Add(new(name, metadata.Teacher, metadata.Location, ranges));
            }
            index = next - 1;
        }
        return courses;
    }

    private static bool TryParseCourseName(string line, out string name)
    {
        var match = CourseNameRegex.Match(line);
        if (match.Success)
        {
            name = match.Groups["name"].Value.Trim();
            return true;
        }

        var creditIndex = line.IndexOf("学分", StringComparison.Ordinal);
        if (creditIndex <= 0)
        {
            name = string.Empty;
            return false;
        }
        name = line[..creditIndex].Trim();
        name = Regex.Replace(name, @"\[[^\]]+\]\s*$", string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static IReadOnlyList<WeekRange> ParseWeekRanges(string line)
    {
        var ranges = new List<WeekRange>();
        foreach (Match match in WeekRegex.Matches(line))
        {
            if (!int.TryParse(match.Groups["start"].Value, out var firstWeek)) continue;
            var lastWeek = match.Groups["end"].Success
                ? int.Parse(match.Groups["end"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : firstWeek;
            var parity = match.Groups["parity"].Value switch
            {
                "单" => WeekParity.Odd,
                "双" => WeekParity.Even,
                _ => WeekParity.All
            };
            if (firstWeek > 0 && lastWeek >= firstWeek)
                ranges.Add(new(firstWeek, lastWeek, parity));
        }
        return MergeRanges(ranges);
    }

    private static (string Teacher, string Location) ParseOfferingMetadata(string line)
    {
        var withoutWeeks = WeekRegex
            .Replace(line, string.Empty)
            .Trim()
            .Trim(',', '，')
            .Trim();
        var metadata = ModeRegex.Replace(withoutWeeks, " ").Trim();
        var parts = metadata.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (string.Empty, string.Empty);
        return (parts[0], parts.Length > 1 ? string.Join(' ', parts[1..]) : string.Empty);
    }

    private static IReadOnlyList<WeekRange> MergeRanges(IEnumerable<WeekRange> ranges)
    {
        var ordered = ranges
            .OrderBy(range => range.FirstWeek)
            .ThenBy(range => range.LastWeek)
            .ToArray();
        if (ordered.Length == 0) return [];

        var merged = new List<WeekRange> { ordered[0] };
        foreach (var range in ordered.Skip(1))
        {
            var previous = merged[^1];
            if (previous.Parity == range.Parity && range.FirstWeek <= previous.LastWeek + 1)
                merged[^1] = previous with { LastWeek = Math.Max(previous.LastWeek, range.LastWeek) };
            else
                merged.Add(range);
        }
        return merged;
    }

    private static IReadOnlyList<CourseEntry> MergeAdjacentRanges(IReadOnlyList<CourseEntry> courses)
    {
        var result = new List<CourseEntry>();
        foreach (var group in courses.GroupBy(course => new
        {
            course.Name,
            course.Teacher,
            course.Location,
            course.Weekday,
            course.StartTime,
            course.EndTime,
            course.Parity
        }))
        {
            var ordered = group.OrderBy(course => course.FirstWeek).ToArray();
            var current = ordered[0];
            foreach (var next in ordered.Skip(1))
            {
                if (next.FirstWeek <= current.LastWeek + 1)
                {
                    current = current with { LastWeek = Math.Max(current.LastWeek, next.LastWeek) };
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }
            result.Add(current);
        }
        return result;
    }

    private static bool TryParseWeekday(string text, out DayOfWeek weekday)
    {
        var normalized = text.Trim();
        weekday = normalized switch
        {
            "星期一" or "周一" or "Monday" => DayOfWeek.Monday,
            "星期二" or "周二" or "Tuesday" => DayOfWeek.Tuesday,
            "星期三" or "周三" or "Wednesday" => DayOfWeek.Wednesday,
            "星期四" or "周四" or "Thursday" => DayOfWeek.Thursday,
            "星期五" or "周五" or "Friday" => DayOfWeek.Friday,
            "星期六" or "周六" or "Saturday" => DayOfWeek.Saturday,
            "星期日" or "星期天" or "周日" or "Sunday" => DayOfWeek.Sunday,
            _ => default
        };
        return normalized is "星期一" or "周一" or "Monday"
            or "星期二" or "周二" or "Tuesday"
            or "星期三" or "周三" or "Wednesday"
            or "星期四" or "周四" or "Thursday"
            or "星期五" or "周五" or "Friday"
            or "星期六" or "周六" or "Saturday"
            or "星期日" or "星期天" or "周日" or "Sunday";
    }

    private static bool TryParsePeriod(string text, out TimeOnly start, out TimeOnly end)
    {
        var match = PeriodRegex.Match(text);
        if (!match.Success
            || !TimeOnly.TryParseExact(match.Groups["start"].Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start)
            || !TimeOnly.TryParseExact(match.Groups["end"].Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out end))
        {
            start = default;
            end = default;
            return false;
        }
        return true;
    }

    private sealed class WordGridCell(
        int startRow,
        int endRow,
        int startColumn,
        int columnSpan,
        string text)
    {
        public int StartRow { get; } = startRow;
        public int EndRow { get; set; } = endRow;
        public int StartColumn { get; } = startColumn;
        public int ColumnSpan { get; } = columnSpan;
        public string Text { get; } = text;
    }

    private sealed record ParsedCourse(
        string Name,
        string Teacher,
        string Location,
        IReadOnlyList<WeekRange> WeekRanges);

    private sealed record WeekRange(int FirstWeek, int LastWeek, WeekParity Parity);
}
