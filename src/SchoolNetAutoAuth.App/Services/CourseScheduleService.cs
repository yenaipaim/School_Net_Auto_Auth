using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.App.Services;

public sealed class CourseScheduleService(ICourseScheduleStore store)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public CourseSchedule Schedule { get; private set; } = CourseSchedule.CreateDefault();
    public event EventHandler<CourseSchedule>? ScheduleChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            Schedule = await store.LoadAsync(cancellationToken);
            _initialized = true;
            ScheduleChanged?.Invoke(this, Schedule);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CourseSchedule schedule, CancellationToken cancellationToken = default)
    {
        var validation = schedule.Validate();
        if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await store.SaveAsync(schedule, cancellationToken);
            Schedule = schedule;
            ScheduleChanged?.Invoke(this, schedule);
        }
        finally
        {
            _gate.Release();
        }
    }
}
