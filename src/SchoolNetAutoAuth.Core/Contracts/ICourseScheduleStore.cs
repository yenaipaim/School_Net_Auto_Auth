using SchoolNetAutoAuth.Core.Schedules;

namespace SchoolNetAutoAuth.Core.Contracts;

public interface ICourseScheduleStore
{
    Task<CourseSchedule> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CourseSchedule schedule, CancellationToken cancellationToken);
}
