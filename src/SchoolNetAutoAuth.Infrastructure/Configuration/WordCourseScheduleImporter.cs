using ExcelDataReader;
using System.IO.Compression;
using System.Net;
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
        @"第\s*(?<number>\d+)(?:\s*-\s*\d+)?\s*节\s*\(\s*(?<start>\d{1,2}:\d{2})\s*-\s*(?<end>\d{1,2}:\d{2})\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CoursePeriodRegex = new(
        @"第\s*(?<start>\d+)(?:\s*节)?(?:\s*-\s*第?\s*(?<end>\d+)\s*节)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CourseNameRegex = new(
        @"^(?<name>.+?)\[[^\]]+\]\s*(?<credits>[0-9]+(?:\.[0-9]+)?)\s*学分",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CreditRegex = new(
        @"(?<credits>[0-9]+(?:\.[0-9]+)?)\s*学分",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WeekRegex = new(
        @"(?<start>\d+)(?:\s*-\s*(?<end>\d+))?\s*周\s*(?:\((?<parity>单|双)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ModeRegex = new(
        @"(?:^|\s)\[[^\]]+\]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlCharsetRegex = new(
        @"charset\s*=\s*[""']?(?<charset>[\w-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTableRegex = new(
        @"<table\b(?<attributes>[^>]*)>(?<body>.*?)</table>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlRowRegex = new(
        @"<tr\b[^>]*>(?<body>.*?)</tr>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlCellRegex = new(
        @"<t[dh]\b(?<attributes>[^>]*)>(?<body>.*?)</t[dh]>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlAttributeRegex = new(
        @"(?<name>rowspan|colspan)\s*=\s*[""']?(?<value>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlCourseBlockRegex = new(
        @"<div\b(?=[^>]*padding-bottom\s*:\s*5px)[^>]*>(?<body>.*?)</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlScheduleRegex = new(
        @"^(?<weeks>.+?)\[(?<start>\d+)\s*-\s*(?<end>\d+)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlWeekRegex = new(
        @"(?<start>\d+)(?:\s*-\s*(?<end>\d+))?\s*周?\s*(?:\((?<parity>单|双)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlBreakRegex = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        var periodRows = new Dictionary<int, (int Number, TimeOnly Start, TimeOnly End)>();
        var periodsByNumber = new Dictionary<int, (TimeOnly Start, TimeOnly End)>();
        foreach (var cell in cells.Where(cell => cell.StartRow > header[0].StartRow))
        {
            if (!TryParsePeriod(cell.Text, out var number, out var start, out var end)) continue;
            periodRows[cell.StartRow] = (number, start, end);
            periodsByNumber[number] = (start, end);
        }
        if (periodRows.Count == 0)
            throw new InvalidDataException("Word 表格中找不到节次时间。");

        var courseSessionCounts = CountWeeklySessions(
            cells,
            dayColumns,
            periodRows,
            periodsByNumber,
            header[0].StartRow);
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
                    var courseStart = startPeriod.Start;
                    var courseEnd = endPeriod.End;
                    if (parsed.PeriodRange is { } requestedRange
                        && periodsByNumber.TryGetValue(requestedRange.First, out var requestedStart)
                        && periodsByNumber.TryGetValue(requestedRange.Last, out var requestedEnd))
                    {
                        courseStart = requestedStart.Start;
                        courseEnd = requestedEnd.End;
                    }
                    else if (endPeriod.Number == startPeriod.Number
                        && periodsByNumber.TryGetValue(
                            startPeriod.Number
                                + InferPeriodCount(
                                    parsed.Credits,
                                    courseStart,
                                    parsed.Location,
                                    courseSessionCounts.GetValueOrDefault(parsed.Name, 1))
                                - 1,
                            out var inferredEnd))
                    {
                        courseEnd = inferredEnd.End;
                    }

                    courses.Add(new(
                        Guid.NewGuid(),
                        parsed.Name,
                        parsed.Teacher,
                        parsed.Location,
                        weekday,
                        courseStart,
                        courseEnd,
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
            MergeAdjacentRanges(courses),
            periodRows.Values
                .Select(period => new SchedulePeriodEntry(
                    period.Number,
                    period.Start,
                    period.End))
                .DistinctBy(period => period.Number)
                .OrderBy(period => period.Number)
                .ToArray());
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    public CourseSchedule ImportExcel(Stream document, DateOnly semesterStartDate)
    {
        if (document.CanSeek && document.Length > MaximumDocumentBytes)
            throw new InvalidDataException("Excel 文件过大。");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = ReadDocumentBytes(document, "Excel 文件过大。");
        if (LooksLikeHtml(bytes))
            return ImportHtmlSchedule(DecodeHtml(bytes), semesterStartDate);

        using var excelStream = new MemoryStream(bytes, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        do
        {
            var rows = ReadExcelRows(reader);
            var schedule = TryImportExcelRows(rows, semesterStartDate);
            if (schedule is not null) return schedule;
        }
        while (reader.NextResult());

        throw new InvalidDataException("Excel 文件中没有识别到课程表。");
    }

    private CourseSchedule ImportHtmlSchedule(string html, DateOnly semesterStartDate)
    {
        foreach (Match table in HtmlTableRegex.Matches(html))
        {
            var schedule = TryImportHtmlTable(table.Groups["body"].Value, semesterStartDate);
            if (schedule is not null) return schedule;
        }

        throw new InvalidDataException("Excel 文件中没有识别到课程表。");
    }

    private CourseSchedule? TryImportHtmlTable(string tableBody, DateOnly semesterStartDate)
    {
        var cells = ParseHtmlTable(tableBody);
        var header = cells
            .Where(cell => cell.StartRow >= 0)
            .GroupBy(cell => cell.StartRow)
            .OrderBy(group => group.Key)
            .Select(group => group.ToArray())
            .FirstOrDefault(row => row.Count(cell => TryParseWeekday(cell.Text, out _)) >= 2);
        if (header is null) return null;

        var dayColumns = new Dictionary<int, DayOfWeek>();
        foreach (var cell in header)
        {
            if (TryParseWeekday(cell.Text, out var weekday))
                dayColumns[cell.StartColumn] = weekday;
        }
        if (dayColumns.Count == 0) return null;

        var headerRow = header[0].StartRow;
        var periodRows = new Dictionary<int, (int Number, TimeOnly Start, TimeOnly End)>();
        var periodsByNumber = DefaultPeriods.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
        foreach (var cell in cells.Where(cell => cell.StartRow > headerRow))
        {
            if (!TryParseHtmlPeriod(cell.Text, out var number, out var start, out var end)) continue;
            periodRows[cell.StartRow] = (number, start, end);
            periodsByNumber[number] = (start, end);
        }
        if (periodRows.Count == 0) return null;

        var courses = new List<CourseEntry>();
        foreach (var cell in cells)
        {
            if (cell.StartRow <= headerRow
                || !dayColumns.TryGetValue(cell.StartColumn, out var weekday)
                || !periodRows.TryGetValue(cell.StartRow, out var defaultPeriod))
            {
                continue;
            }

            foreach (var parsed in ParseHtmlCourseCell(cell.HtmlBody))
            {
                foreach (var range in parsed.WeekRanges)
                {
                    var start = defaultPeriod.Start;
                    var end = defaultPeriod.End;
                    if (parsed.PeriodRange is { } requestedRange
                        && periodsByNumber.TryGetValue(requestedRange.First, out var requestedStart)
                        && periodsByNumber.TryGetValue(requestedRange.Last, out var requestedEnd))
                    {
                        start = requestedStart.Start;
                        end = requestedEnd.End;
                    }

                    courses.Add(new(
                        Guid.NewGuid(),
                        parsed.Name,
                        parsed.Teacher,
                        parsed.Location,
                        weekday,
                        start,
                        end,
                        range.FirstWeek,
                        range.LastWeek,
                        range.Parity));
                }
            }
        }
        if (courses.Count == 0) return null;

        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            semesterStartDate,
            MergeAdjacentRanges(courses),
            DefaultPeriods
                .Select(pair => new SchedulePeriodEntry(
                    pair.Key,
                    pair.Value.Start,
                    pair.Value.End))
                .OrderBy(period => period.Number)
                .ToArray());
        var validation = schedule.Validate();
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        return schedule;
    }

    private static IReadOnlyList<HtmlGridCell> ParseHtmlTable(string tableBody)
    {
        var cells = new List<HtmlGridCell>();
        var activeRowSpans = new Dictionary<int, int>();
        var rowIndex = 0;
        foreach (Match row in HtmlRowRegex.Matches(tableBody))
        {
            var columnIndex = 0;
            foreach (Match cell in HtmlCellRegex.Matches(row.Groups["body"].Value))
            {
                while (activeRowSpans.TryGetValue(columnIndex, out var lastRow)
                    && lastRow >= rowIndex)
                {
                    columnIndex++;
                }

                var attributes = cell.Groups["attributes"].Value;
                var rowSpan = ParseHtmlGridSpan(attributes, "rowspan");
                var columnSpan = ParseHtmlGridSpan(attributes, "colspan");
                cells.Add(new(
                    rowIndex,
                    rowIndex + rowSpan - 1,
                    columnIndex,
                    columnSpan,
                    cell.Groups["body"].Value));

                if (rowSpan > 1)
                {
                    for (var column = columnIndex; column < columnIndex + columnSpan; column++)
                        activeRowSpans[column] = rowIndex + rowSpan - 1;
                }

                columnIndex += columnSpan;
            }
            rowIndex++;
        }
        return cells;
    }

    private static int ParseHtmlGridSpan(string attributes, string name)
    {
        var match = HtmlAttributeRegex.Match(attributes);
        while (match.Success)
        {
            if (string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(match.Groups["value"].Value, out var span)
                && span > 0)
            {
                return span;
            }
            match = match.NextMatch();
        }
        return 1;
    }

    private static IReadOnlyList<ParsedCourse> ParseHtmlCourseCell(string html)
    {
        var courses = new List<ParsedCourse>();
        foreach (Match block in HtmlCourseBlockRegex.Matches(html))
        {
            var parsed = ParseHtmlCourseBlock(block.Groups["body"].Value);
            if (parsed is not null) courses.Add(parsed);
        }
        return courses;
    }

    private static ParsedCourse? ParseHtmlCourseBlock(string html)
    {
        var lines = ExtractHtmlLines(html);
        for (var scheduleIndex = 2; scheduleIndex < lines.Count; scheduleIndex++)
        {
            var schedule = HtmlScheduleRegex.Match(lines[scheduleIndex]);
            if (!schedule.Success) continue;
            if (!int.TryParse(schedule.Groups["start"].Value, out var start)
                || !int.TryParse(schedule.Groups["end"].Value, out var end)
                || start <= 0
                || end < start)
            {
                return null;
            }

            var weekRanges = ParseHtmlWeekRanges(schedule.Groups["weeks"].Value);
            if (weekRanges.Count == 0) return null;

            var name = lines[scheduleIndex - 2];
            var teacher = lines[scheduleIndex - 1];
            var location = scheduleIndex + 1 < lines.Count ? lines[scheduleIndex + 1] : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return null;
            return new(
                name,
                teacher,
                location,
                weekRanges,
                0,
                new CoursePeriodRange(start, end));
        }
        return null;
    }

    private static IReadOnlyList<string> ExtractHtmlLines(string html)
    {
        var withBreaks = HtmlBreakRegex.Replace(html, "\n");
        var withoutTags = HtmlTagRegex.Replace(withBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return decoded
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<WeekRange> ParseHtmlWeekRanges(string text)
    {
        var ranges = new List<WeekRange>();
        foreach (Match match in HtmlWeekRegex.Matches(text))
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

    private static bool TryParseHtmlPeriod(
        string text,
        out int number,
        out TimeOnly start,
        out TimeOnly end)
    {
        var normalized = Regex.Replace(text, @"\s+", string.Empty);
        number = normalized switch
        {
            "一" => 1,
            "二" => 2,
            "三" => 3,
            "四" => 4,
            "五" => 5,
            "六" => 6,
            "七" => 7,
            "八" => 8,
            "九" => 9,
            "十" => 10,
            "十一" => 11,
            "十二" => 12,
            "十三" => 13,
            "十四" => 14,
            _ => 0
        };
        if (number > 0 && DefaultPeriods.TryGetValue(number, out var period))
        {
            start = period.Start;
            end = period.End;
            return true;
        }

        return TryParsePeriod(text, out number, out start, out end);
    }

    private static byte[] ReadDocumentBytes(Stream document, string tooLargeMessage)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;
        int read;
        while ((read = document.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > MaximumDocumentBytes) throw new InvalidDataException(tooLargeMessage);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static bool LooksLikeHtml(ReadOnlySpan<byte> bytes)
    {
        var previewLength = Math.Min(bytes.Length, 4096);
        var preview = Encoding.ASCII.GetString(bytes[..previewLength]);
        return preview.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("<table", StringComparison.OrdinalIgnoreCase);
    }

    private static string DecodeHtml(ReadOnlySpan<byte> bytes)
    {
        var previewLength = Math.Min(bytes.Length, 4096);
        var preview = Encoding.ASCII.GetString(bytes[..previewLength]);
        var charset = HtmlCharsetRegex.Match(preview).Groups["charset"].Value;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset).GetString(bytes);
            }
            catch (ArgumentException)
            {
            }
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static IReadOnlyList<string[]> ReadExcelRows(IExcelDataReader reader)
    {
        var rows = new List<string[]>();
        while (reader.Read())
        {
            var values = new string[reader.FieldCount];
            for (var column = 0; column < reader.FieldCount; column++)
            {
                values[column] = Convert.ToString(
                    reader.GetValue(column),
                    System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            }
            rows.Add(values);
        }
        return rows;
    }

    private CourseSchedule? TryImportExcelRows(
        IReadOnlyList<string[]> rows,
        DateOnly semesterStartDate)
    {
        var headerRow = -1;
        Dictionary<int, DayOfWeek>? dayColumns = null;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var candidates = new Dictionary<int, DayOfWeek>();
            for (var column = 0; column < rows[rowIndex].Length; column++)
            {
                if (TryParseWeekday(rows[rowIndex][column], out var weekday))
                    candidates[column] = weekday;
            }
            if (candidates.Count < 2) continue;
            headerRow = rowIndex;
            dayColumns = candidates;
            break;
        }
        if (dayColumns is null) return null;

        var periodRows = new Dictionary<int, (int Number, TimeOnly Start, TimeOnly End)>();
        var periodsByNumber = new Dictionary<int, (TimeOnly Start, TimeOnly End)>();
        for (var rowIndex = headerRow + 1; rowIndex < rows.Count; rowIndex++)
        {
            for (var column = 0; column < Math.Min(3, rows[rowIndex].Length); column++)
            {
                if (!TryParsePeriod(rows[rowIndex][column], out var number, out var start, out var end))
                    continue;
                periodRows[rowIndex] = (number, start, end);
                periodsByNumber[number] = (start, end);
                break;
            }
        }
        if (periodRows.Count == 0) return null;

        var parsedCells = new List<ExcelScheduleCell>();
        foreach (var (row, rowIndex) in rows.Select((row, index) => (row, index)))
        {
            if (rowIndex <= headerRow || !periodRows.ContainsKey(rowIndex)) continue;
            foreach (var (column, weekday) in dayColumns)
            {
                if (column >= row.Length || string.IsNullOrWhiteSpace(row[column])) continue;
                foreach (var parsed in ParseCourseCell(row[column]))
                    parsedCells.Add(new(weekday, rowIndex, parsed));
            }
        }
        if (parsedCells.Count == 0) return null;

        var sessionCounts = parsedCells
            .Select(cell =>
            {
                var start = periodRows[cell.RowIndex].Start;
                if (cell.Parsed.PeriodRange is { } range
                    && periodsByNumber.TryGetValue(range.First, out var requestedStart))
                {
                    start = requestedStart.Start;
                }
                return (cell.Parsed.Name, cell.Weekday, start);
            })
            .Distinct()
            .GroupBy(session => session.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        var courses = new List<CourseEntry>();
        foreach (var cell in parsedCells)
        {
            var startPeriod = periodRows[cell.RowIndex];
            foreach (var range in cell.Parsed.WeekRanges)
            {
                var courseStart = startPeriod.Start;
                var courseEnd = startPeriod.End;
                if (cell.Parsed.PeriodRange is { } requestedRange
                    && periodsByNumber.TryGetValue(requestedRange.First, out var requestedStart)
                    && periodsByNumber.TryGetValue(requestedRange.Last, out var requestedEnd))
                {
                    courseStart = requestedStart.Start;
                    courseEnd = requestedEnd.End;
                }
                else if (periodsByNumber.TryGetValue(
                    startPeriod.Number
                        + InferPeriodCount(
                            cell.Parsed.Credits,
                            courseStart,
                            cell.Parsed.Location,
                            sessionCounts.GetValueOrDefault(cell.Parsed.Name, 1))
                        - 1,
                    out var inferredEnd))
                {
                    courseEnd = inferredEnd.End;
                }

                courses.Add(new(
                    Guid.NewGuid(),
                    cell.Parsed.Name,
                    cell.Parsed.Teacher,
                    cell.Parsed.Location,
                    cell.Weekday,
                    courseStart,
                    courseEnd,
                    range.FirstWeek,
                    range.LastWeek,
                    range.Parity));
            }
        }
        if (courses.Count == 0) return null;

        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            semesterStartDate,
            MergeAdjacentRanges(courses),
            periodRows.Values
                .Select(period => new SchedulePeriodEntry(
                    period.Number,
                    period.Start,
                    period.End))
                .DistinctBy(period => period.Number)
                .OrderBy(period => period.Number)
                .ToArray());
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
            if (!TryParseCourseName(lines[index], out var name, out var credits)) continue;
            var offerings = new List<string>();
            var next = index + 1;
            while (next < lines.Length && !TryParseCourseName(lines[next], out _, out _))
            {
                if (WeekRegex.IsMatch(lines[next])) offerings.Add(lines[next]);
                next++;
            }
            foreach (var offering in offerings)
            {
                var ranges = ParseWeekRanges(offering);
                if (ranges.Count == 0) continue;
                var metadata = ParseOfferingMetadata(offering);
                courses.Add(new(
                    name,
                    metadata.Teacher,
                    metadata.Location,
                    ranges,
                    credits,
                    ParseCoursePeriodRange(offering)));
            }
            index = next - 1;
        }
        return courses;
    }

    private static bool TryParseCourseName(string line, out string name, out decimal credits)
    {
        var match = CourseNameRegex.Match(line);
        if (match.Success)
        {
            name = match.Groups["name"].Value.Trim();
            decimal.TryParse(
                match.Groups["credits"].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out credits);
            return true;
        }

        var creditIndex = line.IndexOf("学分", StringComparison.Ordinal);
        if (creditIndex <= 0)
        {
            name = string.Empty;
            credits = 0;
            return false;
        }
        name = line[..creditIndex].Trim();
        name = Regex.Replace(name, @"\[[^\]]+\]\s*$", string.Empty).Trim();
        var creditMatch = CreditRegex.Match(line);
        decimal.TryParse(
            creditMatch.Success ? creditMatch.Groups["credits"].Value : string.Empty,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out credits);
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
        var withoutPeriods = CoursePeriodRegex.Replace(withoutWeeks, " ").Trim();
        var metadata = ModeRegex.Replace(withoutPeriods, " ").Trim();
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

    private static CoursePeriodRange? ParseCoursePeriodRange(string text)
    {
        var match = CoursePeriodRegex.Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups["start"].Value, out var first)
            || first <= 0)
        {
            return null;
        }

        var last = match.Groups["end"].Success
            ? int.Parse(match.Groups["end"].Value, System.Globalization.CultureInfo.InvariantCulture)
            : first;
        return last >= first ? new(first, last) : null;
    }

    private static IReadOnlyDictionary<string, int> CountWeeklySessions(
        IReadOnlyList<WordGridCell> cells,
        IReadOnlyDictionary<int, DayOfWeek> dayColumns,
        IReadOnlyDictionary<int, (int Number, TimeOnly Start, TimeOnly End)> periodRows,
        IReadOnlyDictionary<int, (TimeOnly Start, TimeOnly End)> periodsByNumber,
        int headerRow)
    {
        var sessions = new HashSet<(string Name, DayOfWeek Weekday, TimeOnly Start)>();
        foreach (var cell in cells)
        {
            if (cell.StartRow <= headerRow || string.IsNullOrWhiteSpace(cell.Text)) continue;
            if (!dayColumns.TryGetValue(cell.StartColumn, out var weekday)) continue;
            if (!periodRows.TryGetValue(cell.StartRow, out var startPeriod)) continue;

            foreach (var parsed in ParseCourseCell(cell.Text))
            {
                var start = startPeriod.Start;
                if (parsed.PeriodRange is { } range
                    && periodsByNumber.TryGetValue(range.First, out var requestedStart))
                {
                    start = requestedStart.Start;
                }

                sessions.Add((parsed.Name, weekday, start));
            }
        }

        return sessions
            .GroupBy(session => session.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
    }

    private static int InferPeriodCount(
        decimal credits,
        TimeOnly start,
        string location,
        int weeklySessions)
    {
        if (credits <= 0) return 1;
        if (credits >= 4)
            return start < new TimeOnly(12, 0) || weeklySessions <= 1 ? 4 : 2;
        if (credits >= 3)
            return location.Contains("线上", StringComparison.Ordinal) ? 2 : 3;
        if (credits >= 2 && start >= new TimeOnly(18, 0)) return 3;
        return 2;
    }

    private static bool TryParsePeriod(
        string text,
        out int number,
        out TimeOnly start,
        out TimeOnly end)
    {
        var match = PeriodRegex.Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups["number"].Value, out number)
            || !TimeOnly.TryParseExact(match.Groups["start"].Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start)
            || !TimeOnly.TryParseExact(match.Groups["end"].Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out end))
        {
            number = 0;
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

    private sealed record HtmlGridCell(
        int StartRow,
        int EndRow,
        int StartColumn,
        int ColumnSpan,
        string HtmlBody)
    {
        public string Text => string.Join(Environment.NewLine, ExtractHtmlLines(HtmlBody));
    }

    private sealed record ParsedCourse(
        string Name,
        string Teacher,
        string Location,
        IReadOnlyList<WeekRange> WeekRanges,
        decimal Credits,
        CoursePeriodRange? PeriodRange);

    private sealed record ExcelScheduleCell(
        DayOfWeek Weekday,
        int RowIndex,
        ParsedCourse Parsed);

    private sealed record WeekRange(int FirstWeek, int LastWeek, WeekParity Parity);

    private sealed record CoursePeriodRange(int First, int Last);

    private static readonly IReadOnlyDictionary<int, (TimeOnly Start, TimeOnly End)> DefaultPeriods =
        new Dictionary<int, (TimeOnly Start, TimeOnly End)>
        {
            [1] = (new TimeOnly(8, 20), new TimeOnly(9, 0)),
            [2] = (new TimeOnly(9, 10), new TimeOnly(9, 50)),
            [3] = (new TimeOnly(10, 0), new TimeOnly(10, 40)),
            [4] = (new TimeOnly(10, 50), new TimeOnly(11, 30)),
            [5] = (new TimeOnly(11, 40), new TimeOnly(12, 20)),
            [6] = (new TimeOnly(14, 0), new TimeOnly(14, 40)),
            [7] = (new TimeOnly(14, 50), new TimeOnly(15, 30)),
            [8] = (new TimeOnly(15, 40), new TimeOnly(16, 20)),
            [9] = (new TimeOnly(16, 30), new TimeOnly(17, 10)),
            [10] = (new TimeOnly(17, 20), new TimeOnly(18, 0)),
            [11] = (new TimeOnly(19, 0), new TimeOnly(19, 40)),
            [12] = (new TimeOnly(19, 50), new TimeOnly(20, 30)),
            [13] = (new TimeOnly(20, 40), new TimeOnly(21, 20)),
            [14] = (new TimeOnly(21, 30), new TimeOnly(22, 20))
        };
}
