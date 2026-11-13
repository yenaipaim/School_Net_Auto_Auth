using System.IO;

namespace SchoolNetAutoAuth.Installer;

internal sealed record InstallPathCheck(
    bool IsValid,
    string Error,
    string Warning,
    string FullPath,
    long AvailableFreeBytes)
{
    internal static InstallPathCheck Invalid(string error) => new(false, error, string.Empty, string.Empty, 0);
}

/// <summary>安装路径校验：全部在用户点「安装」之前完成，避免装到一半才失败。</summary>
internal static class PathValidation
{
    internal const long RequiredBytes = 320L * 1024 * 1024;

    internal static InstallPathCheck Check(string? rawPath)
    {
        var text = (rawPath ?? string.Empty).Trim().Trim('"');
        if (text.Length == 0) return InstallPathCheck.Invalid("请输入安装路径");
        if (text.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0)
            return InstallPathCheck.Invalid("路径包含不允许的字符");
        if (text.IndexOf(':') > 1)
            return InstallPathCheck.Invalid("路径包含不允许的字符");

        var expanded = Environment.ExpandEnvironmentVariables(text);
        if (!Path.IsPathFullyQualified(expanded))
            return InstallPathCheck.Invalid("请输入完整的安装路径，例如 D:\\Apps\\SchoolNetAutoAuth");
        if (expanded.StartsWith(@"\\", StringComparison.Ordinal) || expanded.StartsWith("//", StringComparison.Ordinal))
            return InstallPathCheck.Invalid("不支持网络位置或移动硬盘");

        string full;
        try { full = Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InstallPathCheck.Invalid("路径格式无效");
        }

        if (full.Length > 240) return InstallPathCheck.Invalid("路径过长，请换一个更短的目录");

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root)
            || full.TrimEnd(Path.DirectorySeparatorChar)
                .Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return InstallPathCheck.Invalid("请选择至少一层文件夹");
        }

        DriveInfo drive;
        try { drive = new DriveInfo(root); }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return InstallPathCheck.Invalid("磁盘不可用，请换一个位置");
        }

        if (!drive.IsReady) return InstallPathCheck.Invalid("磁盘不可用，请换一个位置");
        if (drive.DriveType != DriveType.Fixed)
            return InstallPathCheck.Invalid("不支持网络位置或移动硬盘");

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (IsUnder(full, windows) || IsUnder(full, Environment.SystemDirectory))
            return InstallPathCheck.Invalid("不能安装到系统目录");

        var (writable, writeError) = ProbeWritable(full);
        if (!writable)
            return InstallPathCheck.Invalid(writeError);

        var available = SafeAvailable(drive);
        var warning = string.Empty;
        if (Directory.Exists(full) && !File.Exists(Path.Combine(full, "SchoolNetAutoAuth.App.exe")))
            warning = "该文件夹已存在，继续会覆盖同名文件";
        if (available > 0 && available < RequiredBytes)
            warning = string.IsNullOrEmpty(warning)
                ? "磁盘可用空间不足 320 MB"
                : warning + "；且磁盘可用空间不足 320 MB";

        return new InstallPathCheck(true, string.Empty, warning, full, available);
    }

    /// <summary>在最近一级已存在的父目录里试写探测文件，不提前创建目标目录。</summary>
    private static (bool Writable, string Error) ProbeWritable(string fullPath)
    {
        var existing = fullPath;
        while (!Directory.Exists(existing))
        {
            var parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent)) return (false, "无法访问该位置");
            existing = parent;
        }

        var probe = Path.Combine(existing, $".snaa-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                // 能创建并删除探测文件，即可写。
            }
            return (true, string.Empty);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "这个位置需要管理员权限，请换一个目录");
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            return (false, "无法写入该位置，请换一个目录");
        }
    }

    private static long SafeAvailable(DriveInfo drive)
    {
        try { return drive.AvailableFreeSpace; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static bool IsUnder(string path, string? parent) =>
        !string.IsNullOrEmpty(parent)
        && (path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));

    internal static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024
            ? $"{bytes / 1024d / 1024 / 1024:0.#} GB"
            : $"{Math.Max(0, bytes) / 1024d / 1024:0} MB";
}
