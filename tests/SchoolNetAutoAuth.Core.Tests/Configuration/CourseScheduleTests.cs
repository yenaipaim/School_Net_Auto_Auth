using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.Core.Tests.Configuration;

public sealed class CourseScheduleTests
{
    [Fact]
    public void GetWeekNumber_UsesMondayBasedSemester()
    {
        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            new DateOnly(2026, 9, 7),
            []);

        Assert.Equal(1, schedule.GetWeekNumber(new DateOnly(2026, 9, 7)));
        Assert.Equal(2, schedule.GetWeekNumber(new DateOnly(2026, 9, 14)));
        Assert.Equal(0, schedule.GetWeekNumber(new DateOnly(2026, 9, 6)));
    }

    [Fact]
    public void GetCoursesOn_AppliesWeekdayWeekRangeAndParity()
    {
        var course = new CourseEntry(
            Guid.NewGuid(),
            "高等数学",
            "张老师",
            "A101",
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(9, 40),
            1,
            16,
            WeekParity.Odd);
        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            new DateOnly(2026, 9, 7),
            [course]);

        Assert.Single(schedule.GetCoursesOn(new DateOnly(2026, 9, 7)));
        Assert.Empty(schedule.GetCoursesOn(new DateOnly(2026, 9, 14)));
        Assert.Empty(schedule.GetCoursesOn(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void Validate_RejectsInvalidTimeAndWeekRange()
    {
        var course = new CourseEntry(
            Guid.NewGuid(),
            "课程",
            string.Empty,
            string.Empty,
            DayOfWeek.Monday,
            new TimeOnly(10, 0),
            new TimeOnly(9, 0),
            8,
            2,
            WeekParity.All);
        var schedule = new CourseSchedule(
            CourseSchedule.CurrentSchemaVersion,
            new DateOnly(2026, 9, 7),
            [course]);

        var result = schedule.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("结束时间", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("周次范围", StringComparison.Ordinal));
    }
}
