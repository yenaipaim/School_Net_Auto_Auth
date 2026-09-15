namespace SchoolNetAutoAuth.Core.Configuration;

public enum LocatorStrategy { Role, Label, Placeholder, Text, Css }

public sealed record RecordedLocator(LocatorStrategy Strategy, string Selector, string? Name = null, bool Exact = true);
