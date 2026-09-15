using Microsoft.Playwright;
using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class LocatorResolver
{
    public ILocator Resolve(IPage page, RecordedLocator locator) => locator.Strategy switch
    {
        LocatorStrategy.Role => page.GetByRole(ParseRole(locator.Selector), new() { Name = locator.Name, Exact = locator.Exact }),
        LocatorStrategy.Label => page.GetByLabel(locator.Selector, new() { Exact = locator.Exact }),
        LocatorStrategy.Placeholder => page.GetByPlaceholder(locator.Selector, new() { Exact = locator.Exact }),
        LocatorStrategy.Text => page.GetByText(locator.Selector, new() { Exact = locator.Exact }),
        LocatorStrategy.Css => page.Locator(locator.Selector),
        _ => throw new ArgumentOutOfRangeException(nameof(locator))
    };

    public async Task<bool> IsUniqueAsync(IPage page, RecordedLocator locator) => await Resolve(page, locator).CountAsync() == 1;

    private static AriaRole ParseRole(string value) => Enum.TryParse<AriaRole>(value, true, out var role)
        ? role
        : throw new InvalidDataException($"Unknown ARIA role: {value}");
}
