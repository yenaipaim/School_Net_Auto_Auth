using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Schedules;
using SchoolNetAutoAuth.Infrastructure.Configuration;
using Windows.Storage;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class ScheduleViewModel : ViewModelBase
{
    private readonly CourseScheduleService _scheduleService;
    private readonly DialogService _dialogs;
    private readonly FilePickerService _pickers;
    private readonly CourseScheduleSerializer _serializer;
    private readonly WordCourseScheduleImporter _wordImporter;
    private Guid? _editingCourseId;
    private bool _initialized;

    [ObservableProperty] public partial DateTimeOffset SemesterStartDate { get; set; }
    [ObservableProperty] public partial DateTimeOffset SelectedDate { get; set; }
    [ObservableProperty] public partial CourseEntry? SelectedCourse { get; set; }
    [ObservableProperty] public partial string CourseName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Teacher { get; set; } = string.Empty;
    [ObservableProperty] public partial string Location { get; set; } = string.Empty;
    [ObservableProperty] public partial int WeekdayIndex { get; set; }
    [ObservableProperty] public partial int ParityIndex { get; set; }
    [ObservableProperty] public partial TimeSpan StartTime { get; set; }
    [ObservableProperty] public partial TimeSpan EndTime { get; set; }
    [ObservableProperty] public partial double FirstWeek { get; set; } = 1;
    [ObservableProperty] public partial double LastWeek { get; set; } = 16;
    [ObservableProperty] public partial string StatusText { get; set; } = "课程表已就绪";
    [ObservableProperty] public partial string NextCourseText { get; set; } = "暂无后续课程";
    [ObservableProperty] public partial string SelectedDayText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ShowWeekends { get; set; } = true;
    [ObservableProperty] public partial double TimelineHeight { get; set; }

    public ScheduleViewModel(
        CourseScheduleService scheduleService,
        DialogService dialogs,
        FilePickerService pickers,
        CourseScheduleSerializer serializer,
        WordCourseScheduleImporter wordImporter)
    {
        _scheduleService = scheduleService;
        _dialogs = dialogs;
        _pickers = pickers;
        _serializer = serializer;
        _wordImporter = wordImporter;
        _scheduleService.ScheduleChanged += ScheduleService_ScheduleChanged;
        LoadSchedule(scheduleService.Schedule);
    }

    public ObservableCollection<CourseEntry> Courses { get; } = [];
    public ObservableCollection<ScheduleDayViewModel> WeekDays { get; } = [];
    public ObservableCollection<ScheduleOccurrenceViewModel> SelectedDayCourses { get; } = [];
    public ObservableCollection<ScheduleTimeSlotViewModel> TimeSlots { get; } = [];
    public ObservableCollection<SemesterStartOptionViewModel> SemesterStartOptions { get; } = [];

    [RelayCommand]
    private void NewCourse()
    {
        SelectedCourse = null;
        _editingCourseId = null;
        CourseName = string.Empty;
        Teacher = string.Empty;
        Location = string.Empty;
        WeekdayIndex = 0;
        ParityIndex = 0;
        StartTime = new TimeSpan(8, 0, 0);
        EndTime = new TimeSpan(9, 40, 0);
        FirstWeek = 1;
        LastWeek = 16;
        StatusText = "正在新增课程";
    }

    [RelayCommand]
    private async Task SaveCourseAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CourseName))
                throw new InvalidDataException("请输入课程名称。");
            if (StartTime >= EndTime)
                throw new InvalidDataException("结束时间必须晚于开始时间。");

            var firstWeek = Math.Max(1, (int)Math.Round(FirstWeek));
            var lastWeek = Math.Max(firstWeek, (int)Math.Round(LastWeek));
            if (lastWeek > 52) throw new InvalidDataException("周次不能超过 52。");

            var course = new CourseEntry(
                _editingCourseId ?? Guid.NewGuid(),
                CourseName.Trim(),
                Teacher.Trim(),
                Location.Trim(),
                (DayOfWeek)WeekdayIndex,
                TimeOnly.FromTimeSpan(StartTime),
                TimeOnly.FromTimeSpan(EndTime),
                firstWeek,
                lastWeek,
                (WeekParity)ParityIndex);

            var courses = _scheduleService.Schedule.Courses.ToList();
            var index = courses.FindIndex(item => item.Id == course.Id);
            if (index >= 0) courses[index] = course;
            else courses.Add(course);
            await PersistAsync(_scheduleService.Schedule with { Courses = courses });
            _editingCourseId = course.Id;
            StatusText = index >= 0 ? "课程已更新" : "课程已添加";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存课程失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteCourseAsync()
    {
        if (SelectedCourse is null) return;
        if (!await _dialogs.ConfirmAsync("删除课程", $"确认删除“{SelectedCourse.Name}”？", "删除")) return;

        var courses = _scheduleService.Schedule.Courses
            .Where(course => course.Id != SelectedCourse.Id)
            .ToArray();
        await PersistAsync(_scheduleService.Schedule with { Courses = courses });
        NewCourse();
        StatusText = "课程已删除";
    }

    [RelayCommand]
    private void SelectToday()
    {
        SelectedDate = DateTimeOffset.Now;
        StatusText = "已定位到今天";
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var file = await _pickers.PickOpenFileAsync("课程表文件", ".json", ".csv", ".ics", ".docx");
        if (file is null) return;

        try
        {
            CourseSchedule imported;
            if (string.Equals(file.FileType, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                await using var stream = await file.OpenStreamForReadAsync();
                imported = _wordImporter.Import(stream, DateOnly.FromDateTime(SemesterStartDate.Date));
            }
            else
            {
                var content = await FileIO.ReadTextAsync(file);
                imported = file.FileType.ToLowerInvariant() switch
                {
                    ".csv" => _serializer.DeserializeCsv(content, DateOnly.FromDateTime(SemesterStartDate.Date)),
                    ".ics" => _serializer.DeserializeIcs(content),
                    _ => _serializer.DeserializeJson(content)
                };
            }
            if (!await _dialogs.ConfirmAsync("导入课程表", "导入会替换当前课程表。继续吗？", "导入")) return;
            await PersistAsync(imported);
            StatusText = $"已导入 {imported.Courses.Count} 门课程";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("导入失败", ex.Message);
        }
    }

    [RelayCommand]
    private Task ExportJsonAsync() => ExportAsync(".json", "JSON 课程表", _serializer.SerializeJson);

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync(".csv", "CSV 课程表", _serializer.SerializeCsv);

    [RelayCommand]
    private Task ExportIcsAsync() => ExportAsync(".ics", "ICS 日历", _serializer.SerializeIcs);

    partial void OnSelectedCourseChanged(CourseEntry? value)
    {
        if (value is null) return;
        _editingCourseId = value.Id;
        CourseName = value.Name;
        Teacher = value.Teacher;
        Location = value.Location;
        WeekdayIndex = (int)value.Weekday;
        ParityIndex = (int)value.Parity;
        StartTime = value.StartTime.ToTimeSpan();
        EndTime = value.EndTime.ToTimeSpan();
        FirstWeek = value.FirstWeek;
        LastWeek = value.LastWeek;
    }

    partial void OnSelectedDateChanged(DateTimeOffset value)
    {
        if (!_initialized) return;
        RebuildViews();
    }

    partial void OnSemesterStartDateChanged(DateTimeOffset value)
    {
        if (!_initialized) return;
        _ = SaveSemesterStartAsync(DateOnly.FromDateTime(value.Date));
    }

    partial void OnShowWeekendsChanged(bool value)
    {
        if (!_initialized) return;
        RebuildViews();
    }

    private async Task SaveSemesterStartAsync(DateOnly date)
    {
        try
        {
            await PersistAsync(_scheduleService.Schedule with { SemesterStartDate = date });
            StatusText = "学期开始日期已保存";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存失败", ex.Message);
        }
    }

    private async Task ExportAsync(
        string extension,
        string description,
        Func<CourseSchedule, string> serialize)
    {
        var file = await _pickers.PickSaveFileAsync($"课程表{extension}", description, extension);
        if (file is null) return;
        await FileIO.WriteTextAsync(file, serialize(_scheduleService.Schedule));
        StatusText = $"课程表已导出为 {extension}";
    }

    private async Task PersistAsync(CourseSchedule schedule)
    {
        await _scheduleService.SaveAsync(schedule);
    }

    private void ScheduleService_ScheduleChanged(object? sender, CourseSchedule schedule) =>
        Dispatch(() => LoadSchedule(schedule));

    private void LoadSchedule(CourseSchedule schedule)
    {
        var selectedDate = SelectedDate == default ? DateTimeOffset.Now : SelectedDate;
        var editingCourseId = _editingCourseId;
        _initialized = false;
        SemesterStartDate = new DateTimeOffset(schedule.SemesterStartDate.ToDateTime(TimeOnly.MinValue));
        RebuildSemesterStartOptions(schedule.SemesterStartDate);
        SelectedDate = selectedDate;
        Courses.Clear();
        foreach (var course in schedule.Courses.OrderBy(course => course.Weekday).ThenBy(course => course.StartTime))
            Courses.Add(course);
        if (editingCourseId is not null)
            SelectedCourse = Courses.FirstOrDefault(course => course.Id == editingCourseId);
        _initialized = true;
        RebuildViews();
    }

    private void RebuildViews()
    {
        var schedule = _scheduleService.Schedule;
        var selectedDate = DateOnly.FromDateTime(SelectedDate.Date);
        SelectedDayText = $"{FormatWeekday(selectedDate.DayOfWeek)} {selectedDate:MM月dd日} · 第 {schedule.GetWeekNumber(selectedDate)} 周";
        SelectedDayCourses.Clear();
        foreach (var course in schedule.GetCoursesOn(selectedDate))
            SelectedDayCourses.Add(CreateOccurrence(course, selectedDate));

        var weekStart = CourseSchedule.StartOfWeek(selectedDate);
        var dayCount = ShowWeekends ? 7 : 5;
        var dates = Enumerable.Range(0, dayCount)
            .Select(offset => weekStart.AddDays(offset))
            .ToArray();
        var visibleCourses = dates
            .SelectMany(date => schedule.GetCoursesOn(date))
            .ToArray();
        var firstMinute = visibleCourses.Length == 0
            ? 8 * 60
            : (int)Math.Floor(visibleCourses.Min(course => course.StartTime.ToTimeSpan().TotalMinutes) / 30) * 30;
        var lastMinute = visibleCourses.Length == 0
            ? 20 * 60
            : (int)Math.Ceiling(visibleCourses.Max(course => course.EndTime.ToTimeSpan().TotalMinutes) / 30) * 30;
        firstMinute = Math.Clamp(firstMinute, 0, 23 * 60);
        lastMinute = Math.Clamp(lastMinute, firstMinute + 60, 24 * 60);
        const double hourHeight = 52;
        TimelineHeight = (lastMinute - firstMinute) / 60d * hourHeight + 12;
        TimeSlots.Clear();
        for (var minute = firstMinute; minute <= lastMinute; minute += 30)
        {
            var lineTop = (minute - firstMinute) / 60d * hourHeight;
            TimeSlots.Add(new(
                $"{minute / 60:00}:{minute % 60:00}",
                lineTop,
                Math.Max(0, lineTop - 8),
                minute % 60 == 0));
        }

        WeekDays.Clear();
        foreach (var date in dates)
        {
            var items = schedule.GetCoursesOn(date)
                .Select(course => CreateOccurrence(course, date, firstMinute, hourHeight))
                .ToArray();
            WeekDays.Add(new(
                FormatWeekday(date.DayOfWeek),
                date.ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture),
                TimeSlots.ToArray(),
                items,
                TimelineHeight));
        }
        NextCourseText = BuildNextCourseText(schedule, DateTime.Now);
    }

    private void RebuildSemesterStartOptions(DateOnly selectedDate)
    {
        SemesterStartOptions.Clear();
        var firstDate = selectedDate.AddYears(-1);
        var lastDate = selectedDate.AddYears(1);
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            SemesterStartOptions.Add(new(
                new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)),
                $"{date:yyyy-MM-dd} {FormatWeekday(date.DayOfWeek)}"));
        }
    }

    private static ScheduleOccurrenceViewModel CreateOccurrence(
        CourseEntry course,
        DateOnly date,
        int firstMinute = 8 * 60,
        double hourHeight = 52)
    {
        var now = DateTime.Now;
        var isToday = DateOnly.FromDateTime(now) == date;
        var isCurrent = isToday
            && course.StartTime <= TimeOnly.FromDateTime(now)
            && course.EndTime > TimeOnly.FromDateTime(now);
        var startMinute = course.StartTime.ToTimeSpan().TotalMinutes;
        var duration = course.EndTime.ToTimeSpan().TotalMinutes - startMinute;
        return new(
            course.Name,
            $"{course.StartTime:HH:mm}-{course.EndTime:HH:mm}",
            string.IsNullOrWhiteSpace(course.Location) ? string.Empty : course.Location,
            string.IsNullOrWhiteSpace(course.Teacher) ? string.Empty : course.Teacher,
            isCurrent,
            (startMinute - firstMinute) / 60d * hourHeight + 2,
            Math.Max(34, duration / 60d * hourHeight - 4));
    }

    private static string BuildNextCourseText(CourseSchedule schedule, DateTime now)
    {
        var currentDate = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = currentDate.AddDays(offset);
            var course = schedule.GetCoursesOn(date)
                .FirstOrDefault(item => offset > 0 || item.StartTime > currentTime);
            if (course is null) continue;

            var location = string.IsNullOrWhiteSpace(course.Location) ? string.Empty : $" · {course.Location}";
            return $"{FormatWeekday(date.DayOfWeek)} {date:MM/dd} {course.StartTime:HH:mm} {course.Name}{location}";
        }
        return "暂无后续课程";
    }

    private static string FormatWeekday(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日"
    };
}

public sealed record ScheduleDayViewModel(
    string DayName,
    string DateText,
    IReadOnlyList<ScheduleTimeSlotViewModel> TimeSlots,
    IReadOnlyList<ScheduleOccurrenceViewModel> Courses,
    double TimelineHeight);

public sealed record ScheduleTimeSlotViewModel(
    string TimeText,
    double LineTop,
    double LabelTop,
    bool IsHour);

public sealed record SemesterStartOptionViewModel(
    DateTimeOffset Date,
    string Text);

public sealed record ScheduleOccurrenceViewModel(
    string Name,
    string TimeText,
    string Location,
    string Teacher,
    bool IsCurrent,
    double Top,
    double Height);
