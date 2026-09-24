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
        Assert.Equal(2, schedule.GetTimelinePeriods().Count);
        Assert.Equal(new TimeOnly(8, 20), schedule.GetTimelinePeriods()[0].StartTime);
        Assert.Equal(new TimeOnly(9, 50), schedule.GetTimelinePeriods()[1].EndTime);

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

    [Fact]
    public void Import_UsesExplicitCoursePeriodRangeWithoutVerticalMerge()
    {
        var rows = new[]
        {
            Row(Cell("节次/星期"), Cell("星期一"), Cell("星期二"), Cell("星期三"), Cell("星期四"), Cell("星期五")),
            Row(
                Cell("第1节(08:20-09:00)"),
                Cell("数据结构[3200610002] 4.0学分\n1-16周[讲授] 樊龙 A7301 第1节-第3节"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第2节(09:10-09:50)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第3节(10:00-10:40)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty))
        };
        using var document = CreateDocx(new XElement(WordNamespace + "document",
            new XElement(WordNamespace + "body",
                new XElement(WordNamespace + "tbl", rows))));

        var schedule = _importer.Import(document, new DateOnly(2026, 9, 7));

        var course = Assert.Single(schedule.Courses);
        Assert.Equal("数据结构", course.Name);
        Assert.Equal(new TimeOnly(8, 20), course.StartTime);
        Assert.Equal(new TimeOnly(10, 40), course.EndTime);
        Assert.Equal("A7301", course.Location);
    }

    [Fact]
    public void Import_InfersMorningFourCreditBlockWhenWordLosesMergedRows()
    {
        var rows = new[]
        {
            Row(Cell("节次/星期"), Cell("星期一"), Cell("星期二"), Cell("星期三"), Cell("星期四"), Cell("星期五")),
            Row(
                Cell("第1节(08:20-09:00)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第2节(09:10-09:50)"),
                Cell("大语言模型技术与应用[3200610640] 4.0学分\n1-4周[讲授] 谢心 A3208"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第3节(10:00-10:40)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第4节(10:50-11:30)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第5节(11:40-12:20)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty))
        };
        using var document = CreateDocx(new XElement(WordNamespace + "document",
            new XElement(WordNamespace + "body",
                new XElement(WordNamespace + "tbl", rows))));

        var schedule = _importer.Import(document, new DateOnly(2026, 9, 7));

        var course = Assert.Single(schedule.Courses);
        Assert.Equal(new TimeOnly(9, 10), course.StartTime);
        Assert.Equal(new TimeOnly(12, 20), course.EndTime);
    }

    [Fact]
    public void Import_InfersWholeAfternoonBlockForSingleWeeklyFourCreditCourse()
    {
        var rows = new[]
        {
            Row(Cell("节次/星期"), Cell("星期一"), Cell("星期二"), Cell("星期三"), Cell("星期四"), Cell("星期五")),
            Row(
                Cell("第6节(14:00-14:40)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell("计算机网络[2200610240] 4.0学分\n1-16周[讲授] 刘茹文 F3216"),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第7节(14:50-15:30)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第8节(15:40-16:20)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty)),
            Row(
                Cell("第9节(16:30-17:10)"),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty),
                Cell(string.Empty))
        };
        using var document = CreateDocx(new XElement(WordNamespace + "document",
            new XElement(WordNamespace + "body",
                new XElement(WordNamespace + "tbl", rows))));

        var schedule = _importer.Import(document, new DateOnly(2026, 9, 7));

        var course = Assert.Single(schedule.Courses);
        Assert.Equal(new TimeOnly(14, 0), course.StartTime);
        Assert.Equal(new TimeOnly(17, 10), course.EndTime);
    }

    [Fact]
    public void ImportExcel_ReadsLegacyXlsAndExplicitPeriodRange()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Configuration",
            "Fixtures",
            "schedule-smoke.xls");
        using var document = File.OpenRead(path);

        var schedule = _importer.ImportExcel(document, new DateOnly(2026, 9, 15));

        var course = Assert.Single(schedule.Courses);
        Assert.Equal("数据结构", course.Name);
        Assert.Equal(DayOfWeek.Monday, course.Weekday);
        Assert.Equal(new TimeOnly(8, 20), course.StartTime);
        Assert.Equal(new TimeOnly(10, 40), course.EndTime);
        Assert.Equal(1, course.FirstWeek);
        Assert.Equal(16, course.LastWeek);
        Assert.NotEmpty(schedule.GetTimelinePeriods());
    }

    [Fact]
    public void ImportExcel_ReadsGb2312HtmlTableDisguisedAsXls()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string html = """
            <!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
            <html>
            <head><meta http-equiv="Content-Type" content="text/html; charset=GBK" /></head>
            <body>
            <table id="mytable">
              <tr>
                <td colspan="2"></td>
                <td>星期一</td>
                <td>星期二</td>
              </tr>
              <tr>
                <td rowspan="2">上午</td>
                <td>一</td>
                <td>
                  <div class="div1">
                    <div style="padding-bottom:5px;clear:both;">
                      <font style="font-weight: bolder">大学英语（3）</font><br>
                      廖勇<br>1-16[1-2]<br>C208
                    </div>
                  </div>
                </td>
                <td></td>
              </tr>
              <tr>
                <td>二</td>
                <td></td>
                <td>
                  <div class="div1">
                    <div style="padding-bottom:5px;clear:both;">
                      <font style="font-weight: bolder">烹饪职业教育学</font><br>
                      刘浩林<br>9-16[3-4]<br>B311
                    </div>
                    <div style="padding-bottom:5px;clear:both;">
                      <font style="font-weight: bolder">烹饪职业教育学</font><br>
                      丁晓<br>1-8[3-4]<br>B311
                    </div>
                  </div>
                </td>
              </tr>
            </table>
            </body>
            </html>
            """;
        using var document = new MemoryStream(Encoding.GetEncoding("GBK").GetBytes(html));

        var schedule = _importer.ImportExcel(document, new DateOnly(2026, 9, 7));

        Assert.Equal(3, schedule.Courses.Count);
        var english = Assert.Single(schedule.Courses, course => course.Name == "大学英语（3）");
        Assert.Equal("廖勇", english.Teacher);
        Assert.Equal("C208", english.Location);
        Assert.Equal(DayOfWeek.Monday, english.Weekday);
        Assert.Equal(new TimeOnly(8, 20), english.StartTime);
        Assert.Equal(new TimeOnly(9, 50), english.EndTime);

        var firstTeacher = Assert.Single(schedule.Courses, course =>
            course.Name == "烹饪职业教育学" && course.Teacher == "刘浩林");
        Assert.Equal(DayOfWeek.Tuesday, firstTeacher.Weekday);
        Assert.Equal(new TimeOnly(10, 0), firstTeacher.StartTime);
        Assert.Equal(new TimeOnly(11, 30), firstTeacher.EndTime);
        Assert.Equal(9, firstTeacher.FirstWeek);
        Assert.Equal(16, firstTeacher.LastWeek);
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
