using SchoolNetAutoAuth.Installer;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class InstallPathValidationTests
{
    [Theory]
    [InlineData("", "请输入安装路径")]
    [InlineData("   ", "请输入安装路径")]
    [InlineData(@"C:\Windows\Temp\SchoolNetAutoAuth", "不能安装到系统目录")]
    [InlineData(@"\\server\share\SchoolNetAutoAuth", "不支持网络位置或移动硬盘")]
    public void Check_RejectsObviousBadTargets(string path, string expectedErrorStart)
    {
        var result = PathValidation.Check(path);

        Assert.False(result.IsValid);
        Assert.StartsWith(expectedErrorStart, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_RejectsDriveRoot()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;
        var result = PathValidation.Check(root);

        Assert.False(result.IsValid);
        Assert.Equal("请选择至少一层文件夹", result.Error);
    }

    [Fact]
    public void Check_RejectsInvalidCharactersAndOverlongPaths()
    {
        var invalid = PathValidation.Check(Path.Combine(Path.GetTempPath(), "bad?name"));
        Assert.False(invalid.IsValid);
        Assert.Equal("路径包含不允许的字符", invalid.Error);

        var longPath = Path.Combine(Path.GetTempPath(), new string('a', 260));
        Assert.False(PathValidation.Check(longPath).IsValid);
    }

    [Fact]
    public void Check_AcceptsWritableLocalDirectory()
    {
        var target = Path.Combine(Path.GetTempPath(), "SchoolNetAutoAuthInstallFixture", Guid.NewGuid().ToString("N"));

        var result = PathValidation.Check(target);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(Path.GetFullPath(target), result.FullPath);
        Assert.False(Directory.Exists(target));
    }
}
