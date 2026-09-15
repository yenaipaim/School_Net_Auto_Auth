namespace SchoolNetAutoAuth.App.Tests;

public sealed class AppAssemblyTests
{
    [Fact]
    public void AppType_LoadsFromWpfAssembly()
    {
        Assert.Equal("SchoolNetAutoAuth.App", typeof(App).Namespace);
    }
}
