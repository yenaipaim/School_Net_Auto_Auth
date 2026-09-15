using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public static class LocatorCandidateFactory
{
    public static IReadOnlyList<RecordedLocator> CreateCandidates(ElementDescriptor element)
    {
        var candidates = new List<RecordedLocator>();
        Add(element.Role is null ? null : new(LocatorStrategy.Role, Normalize(element.Role), Normalize(element.AccessibleName)), candidates);
        Add(element.Label is null ? null : new(LocatorStrategy.Label, Normalize(element.Label)), candidates);
        Add(element.Placeholder is null ? null : new(LocatorStrategy.Placeholder, Normalize(element.Placeholder)), candidates);
        Add(element.Text is null ? null : new(LocatorStrategy.Text, Normalize(element.Text)), candidates);
        Add(string.IsNullOrWhiteSpace(element.CssPath) ? null : new(LocatorStrategy.Css, element.CssPath.Trim()), candidates);
        return candidates.Distinct().ToArray();
    }

    private static string Normalize(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static void Add(RecordedLocator? locator, List<RecordedLocator> list)
    {
        if (locator is not null && !string.IsNullOrWhiteSpace(locator.Selector) &&
            (locator.Strategy != LocatorStrategy.Role || !string.IsNullOrWhiteSpace(locator.Name))) list.Add(locator);
    }
}
