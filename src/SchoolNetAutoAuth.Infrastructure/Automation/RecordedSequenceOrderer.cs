using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public static class RecordedSequenceOrderer
{
    public static IReadOnlyList<RecordedClickStep> Order(IEnumerable<RecordedClickStep> clicks)
        => clicks.OrderBy(step => step.Order).ToArray();
}
