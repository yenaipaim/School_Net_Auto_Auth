namespace SchoolNetAutoAuth.App.Tests;

public sealed class AppAssemblyTests
{
    [Fact]
    public void AppType_LoadsFromWpfAssembly()
    {
        Assert.Equal("SchoolNetAutoAuth.App", typeof(App).Namespace);
    }

    [Fact]
    public void MainWindow_CanBeConstructedWithoutMissingResources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new Views.MainWindow(background: true);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
