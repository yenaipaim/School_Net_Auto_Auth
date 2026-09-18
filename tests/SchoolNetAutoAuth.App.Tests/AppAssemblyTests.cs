namespace SchoolNetAutoAuth.App.Tests;

public sealed class AppAssemblyTests
{
    [Fact]
    public void AppType_LoadsFromWinUiAssembly()
    {
        Assert.Equal("SchoolNetAutoAuth.App", typeof(App).Namespace);
    }

    [Fact]
    public void AppAssembly_DoesNotReferenceWpf()
    {
        var references = typeof(App).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "PresentationFramework");
    }

    [Fact]
    public void TrayAssembly_UsesWindowsFormsNotifyIcon()
    {
        var references = typeof(SchoolNetAutoAuth.Tray.TrayIconService).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => reference.Name == "System.Windows.Forms");
    }
}
