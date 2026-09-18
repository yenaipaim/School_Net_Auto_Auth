using SchoolNetAutoAuth.App.Notifications;
using SchoolNetAutoAuth.App.Views;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class NotificationAndAboutTests
{
    [Fact]
    public void Calculate_PlacementUsesWorkAreaBottomRightMargin()
    {
        var result = NotificationPlacement.Calculate(new DesktopRect(100, 50, 1600, 900), new DesktopSize(360, 180), 16);

        Assert.Equal(new DesktopPoint(1324, 754), result);
    }

    [Fact]
    public void AboutInfo_ContainsRequestedAuthorAndRepository()
    {
        Assert.Equal("yenaipaim", AboutInfo.Author);
        Assert.Equal(new Uri("https://github.com/yenaipaim/School_Net_Auto_Auth"), AboutInfo.RepositoryUri);
    }
}
