using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class CourseScheduleServiceTests
{
    [Fact]
    public async Task SaveAsync_PersistsAndRaisesChanged()
    {
        var store = new MemoryStore();
        var service = new CourseScheduleService(store);
        await service.InitializeAsync();
        var changed = 0;
        service.ScheduleChanged += (_, _) => changed++;
        var schedule = CourseSchedule.CreateDefault() with { SemesterStartDate = new DateOnly(2026, 9, 7) };

        await service.SaveAsync(schedule);

        Assert.Same(schedule, store.Saved);
        Assert.Same(schedule, service.Schedule);
        Assert.Equal(1, changed);
    }

    private sealed class MemoryStore : ICourseScheduleStore
    {
        public CourseSchedule? Saved { get; private set; }

        public Task<CourseSchedule> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CourseSchedule.CreateDefault());

        public Task SaveAsync(CourseSchedule schedule, CancellationToken cancellationToken)
        {
            Saved = schedule;
            return Task.CompletedTask;
        }
    }
}
