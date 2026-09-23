using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using SchoolNetAutoAuth.Core.Schedules;
using SchoolNetAutoAuth.Infrastructure.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Configuration;

public sealed class WordCourseScheduleImporterTests
{
    private static readonly XNamespace WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly WordCourseScheduleImporter _importer = new();

    [Fact]
    public void Import_ParsesMergedCoursesWeekRangesParityAndMetadata()
    {
        using var document = CreateDocument();

        var schedule = _importer.Import(document, new DateOnly(2026, 9, 7));

        Assert.Equal(new DateOnly(2026, 9, 7), schedule.SemesterStartDate);
        Assert.Equal(4, schedule.Courses.Count);

        var first = Assert.Single(schedule.Courses, course =>
            course.Name == "大语言模型技术与应用" && course.FirstWeek == 1);
        Assert.Equal("谢心", first.Teacher);
        Assert.Equal("A3208", first.Location);
        Assert.Equal(DayOfWeek.Monday, first.Weekday);
        Assert.Equal(new TimeOnly(8, 20), first.StartTime);
        Assert.Equal(new TimeOnly(9, 50), first.EndTime);
        Assert.Equal(4, first.LastWeek);
        Assert.Equal(WeekParity.All, first.Parity);

        var later = Assert.Single(schedule.Courses, course =>
            course.Name == "大语言模型技术与应用" && course.FirstWeek == 6);
        Assert.Equal("A3208", later.Location);
        Assert.Equal(8, later.LastWeek);

        var locationChange = Assert.Single(schedule.Courses, course =>
            course.Name == "大语言模型技术与应用" && course.FirstWeek == 9);
        Assert.Equal("A3210", locationChange.Location);

        var oddWeekCourse = Assert.Single(schedule.Courses, course => course.Name == "高等数学");
        Assert.Equal("张老师", oddWeekCourse.Teacher);
        Assert.Equal("B101", oddWeekCourse.Location);
        Assert.Equal(DayOfWeek.Tuesday, oddWeekCourse.Weekday);
        Assert.Equal(WeekParity.Odd, oddWeekCourse.Parity);
        Assert.Equal(1, oddWeekCourse.FirstWeek);
        Assert.Equal(15, oddWeekCourse.LastWeek);
    }

    [Fact]
    public void Import_RejectsDocumentWithoutScheduleTable()
    {
        using var document = CreateDocx(new XElement(WordNamespace + "document",
            new XElement(WordNamespace + "body",
                new XElement(WordNamespace + "p",
                    new XElement(WordNamespace + "r",
                        new XElement(WordNamespace + "t", "没有课程表"))))));

        var exception = Assert.Throws<InvalidDataException>(() =>
            _importer.Import(document, new DateOnly(2026, 9, 7)));

        Assert.Contains("没有课程表", exception.Message);
    }

    private static MemoryStream CreateDocument()
    {
        var rows = new[]
        {
            Row(Cell("节次/星期"), Cell("星期一"), Cell("星期二"), Cell("星期三"), Cell("星期四"), Cell("星期五")),
            Row(
                Cell("第1节(08:20-09:00)"),
                Cell("大语言模型技术与应用[3200610640] 4.0学分\n1-4周,6-8周[讲授] 谢心 A3208\n9周[讲授] 谢心 A3210\n选课人数: 32\n人工智能25401", restart: true),
                Cell("高等数学[3200610001] 3.0学分\n1-15周(单)[讲授] 张老师 B101", restart: true),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第2节(09:10-09:50)"),
                Cell(string.Empty, continuation: true),
                Cell(string.Empty, continuation: true),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty))
        };
        var table = new XElement(WordNamespace + "tbl", rows);
        return CreateDocx(new XElement(WordNamespace + "document",
            new XElement(WordNamespace + "body", table)));
    }

    private static XElement Row(params XElement[] cells) => new(WordNamespace + "tr", cells);

    private static XElement Cell(
        string text,
        bool restart = false,
        bool continuation = false)
    {
        var properties = new XElement(WordNamespace + "tcPr");
        if (restart)
            properties.Add(new XElement(WordNamespace + "vMerge", new XAttribute(WordNamespace + "val", "restart")));
        else if (continuation)
            properties.Add(new XElement(WordNamespace + "vMerge"));

        var content = new List<object>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0) content.Add(new XElement(WordNamespace + "br"));
            content.Add(new XElement(WordNamespace + "t", lines[index]));
        }
        return new XElement(WordNamespace + "tc",
            properties,
            new XElement(WordNamespace + "p", new XElement(WordNamespace + "r", content)));
    }

    private static MemoryStream CreateDocx(XElement documentXml)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            documentXml.Save(writer);
        }
        stream.Position = 0;
        return stream;
    }
}
