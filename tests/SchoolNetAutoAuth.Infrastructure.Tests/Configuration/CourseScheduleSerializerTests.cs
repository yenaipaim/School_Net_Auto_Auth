using SchoolNetAutoAuth.Core.Schedules;
using SchoolNetAutoAuth.Infrastructure.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Configuration;

public sealed class CourseScheduleSerializerTests
{
    private readonly CourseScheduleSerializer _serializer = new();

    [Fact]
    public void Json_RoundTripsSchedule()
    {
        var schedule = CreateSchedule();

        var result = _serializer.DeserializeJson(_serializer.SerializeJson(schedule));

        Assert.Equal(schedule.SemesterStartDate, result.SemesterStartDate);
        var course = Assert.Single(result.Courses);
        Assert.Equal("高等数学", course.Name);
        Assert.Equal(WeekParity.Odd, course.Parity);
        Assert.Collection(
            result.GetTimelinePeriods(),
            period => Assert.Equal(new TimeOnly(8, 0), period.StartTime),
            period => Assert.Equal(new TimeOnly(9, 40), period.EndTime));
    }

    [Fact]
    public void Csv_RoundTripsCourses()
    {
        var schedule = CreateSchedule();
        var start = schedule.SemesterStartDate;

        var result = _serializer.DeserializeCsv(_serializer.SerializeCsv(schedule), start);

        var course = Assert.Single(result.Courses);
        Assert.Equal("高等数学", course.Name);
        Assert.Equal("张老师", course.Teacher);
        Assert.Equal(DayOfWeek.Monday, course.Weekday);
        Assert.Equal(new TimeOnly(8, 0), course.StartTime);
        Assert.Equal(WeekParity.Odd, course.Parity);
    }

    [Fact]
    public void Ics_RoundTripsWeeklyCourse()
    {
        var schedule = CreateSchedule();

        var result = _serializer.DeserializeIcs(_serializer.SerializeIcs(schedule));

        var course = Assert.Single(result.Courses);
        Assert.Equal("高等数学", course.Name);
        Assert.Equal("张老师", course.Teacher);
        Assert.Equal("A101", course.Location);
        Assert.Equal(DayOfWeek.Monday, course.Weekday);
        Assert.Equal(new TimeOnly(8, 0), course.StartTime);
        Assert.Equal(new TimeOnly(9, 40), course.EndTime);
    }

    private static CourseSchedule CreateSchedule()
    {
        var course = new CourseEntry(
            Guid.Parse("d7b8a2b9-2b3d-4a49-9d58-ce49c976c53a"),
            "高等数学",
            "张老师",
            "A101",
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(9, 40),
            1,
            16,
            WeekParity.Odd);
        return new(
            CourseSchedule.CurrentSchemaVersion,
            new DateOnly(2026, 9, 7),
            [course],
            [
                new SchedulePeriodEntry(1, new TimeOnly(8, 0), new TimeOnly(8, 45)),
                new SchedulePeriodEntry(2, new TimeOnly(8, 55), new TimeOnly(9, 40))
            ]);
    }
}
