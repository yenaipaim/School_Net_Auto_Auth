namespace SchoolNetAutoAuth.Infrastructure.Automation;

public static class RecordedClickFilter
{
    public static bool ShouldReplay(ElementDescriptor element)
        => !string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(element.TagName, "textarea", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(element.Role, "textbox", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(element.Role, "password", StringComparison.OrdinalIgnoreCase);
}
