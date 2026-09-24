namespace SchoolNetAutoAuth.Core.Schedules;

public enum WeekParity
{
    All = 0,
    Odd = 1,
    Even = 2
}

public sealed record CourseEntry(
    Guid Id,
    string Name,
    string Teacher,
    string Location,
    DayOfWeek Weekday,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int FirstWeek,
    int LastWeek,
    WeekParity Parity)
{
    public bool OccursInWeek(int week) =>
        week >= FirstWeek
        && week <= LastWeek
        && Parity switch
        {
            WeekParity.Odd => week % 2 == 1,
            WeekParity.Even => week % 2 == 0,
            _ => true
        };
}

public sealed record SchedulePeriodEntry(
    int Number,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record CourseSchedule(
    int SchemaVersion,
    DateOnly SemesterStartDate,
    IReadOnlyList<CourseEntry> Courses,
    IReadOnlyList<SchedulePeriodEntry>? Periods = null)
{
    public const int CurrentSchemaVersion = 2;

    public static CourseSchedule CreateDefault()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new(CurrentSchemaVersion, StartOfWeek(today), []);
    }

    public int GetWeekNumber(DateOnly date)
    {
        var semesterMonday = StartOfWeek(SemesterStartDate);
        var dateMonday = StartOfWeek(date);
        return ((dateMonday.DayNumber - semesterMonday.DayNumber) / 7) + 1;
    }

    public IReadOnlyList<CourseEntry> GetCoursesOn(DateOnly date)
    {
        var week = GetWeekNumber(date);
        if (week < 1) return [];

        return Courses
            .Where(course => course.Weekday == date.DayOfWeek && course.OccursInWeek(week))
            .OrderBy(course => course.StartTime)
            .ToArray();
    }

    public IReadOnlyList<SchedulePeriodEntry> GetTimelinePeriods()
    {
        var explicitPeriods = Periods?
            .Where(period => period.Number > 0 && period.StartTime < period.EndTime)
            .OrderBy(period => period.Number)
            .ToArray() ?? [];
        if (explicitPeriods.Length > 0) return explicitPeriods;

        return Courses
            .Select(course => (course.StartTime, course.EndTime))
            .Distinct()
            .OrderBy(period => period.StartTime)
            .ThenBy(period => period.EndTime)
            .Select((period, index) => new SchedulePeriodEntry(
                index + 1,
                period.StartTime,
                period.EndTime))
            .ToArray();
    }

    public ScheduleValidationResult Validate()
    {
        var errors = new List<string>();
        if (SemesterStartDate == default) errors.Add("学期开始日期无效。");
        if (Courses is null)
        {
            errors.Add("课程列表无效。");
            return new(errors);
        }

        var duplicateIds = Courses
            .GroupBy(course => course.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0) errors.Add("课程标识重复。");

        if (Periods is not null)
        {
            var duplicatePeriods = Periods
                .GroupBy(period => period.Number)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePeriods.Length > 0) errors.Add("节次编号重复。");

            foreach (var period in Periods)
            {
                if (period.Number <= 0) errors.Add("节次编号必须大于 0。");
                if (period.StartTime >= period.EndTime)
                    errors.Add($"第 {period.Number} 节的结束时间必须晚于开始时间。");
            }
        }

        foreach (var course in Courses)
        {
            if (course.Id == Guid.Empty) errors.Add("课程标识不能为空。");
            if (string.IsNullOrWhiteSpace(course.Name)) errors.Add("课程名称不能为空。");
            if (!Enum.IsDefined(course.Weekday)) errors.Add($"课程“{course.Name}”的星期无效。");
            if (course.StartTime >= course.EndTime) errors.Add($"课程“{course.Name}”的结束时间必须晚于开始时间。");
            if (course.FirstWeek < 1 || course.LastWeek < course.FirstWeek) errors.Add($"课程“{course.Name}”的周次范围无效。");
            if (!Enum.IsDefined(course.Parity)) errors.Add($"课程“{course.Name}”的单双周设置无效。");
        }

        return new(errors);
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}

public sealed record ScheduleValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
