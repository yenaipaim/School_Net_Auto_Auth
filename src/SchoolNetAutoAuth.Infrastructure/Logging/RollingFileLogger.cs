using System.Text;

namespace SchoolNetAutoAuth.Infrastructure.Logging;

public sealed class RollingFileLogger(string path, TimeProvider? timeProvider = null)
{
    private const int MaximumBytes = 1024 * 1024;
    private const int RetainedBytes = 768 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false);
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Write(AppLogEntry entry)
    {
        try
        {
            lock (_sync)
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var line = Format(entry);
                var lineBytes = Utf8.GetBytes(line);
                var currentLength = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (currentLength + lineBytes.Length > MaximumBytes)
                    TrimAndAppend(lineBytes);
                else
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                        stream.Write(lineBytes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Logging must never interrupt authentication.
        }
    }

    private string Format(AppLogEntry entry)
    {
        var builder = new StringBuilder(160);
        builder.Append(_timeProvider.GetUtcNow().ToString("O"));
        builder.Append(" event=").Append(entry.Event);
        if (entry.State is not null) builder.Append(" state=").Append(entry.State);
        if (entry.Outcome is not null) builder.Append(" outcome=").Append(entry.Outcome);
        if (entry.ExternalAction is not null) builder.Append(" externalAction=").Append(entry.ExternalAction);
        if (entry.Attempt is not null) builder.Append(" attempt=").Append(entry.Attempt.Value);
        if (entry.Url is not null) builder.Append(" url=").Append(Sanitize(entry.Url.ToString()));
        if (!string.IsNullOrWhiteSpace(entry.Message)) builder.Append(" message=").Append(Sanitize(entry.Message));
        if (!string.IsNullOrWhiteSpace(entry.TechnicalDetail)) builder.Append(" detail=").Append(Sanitize(entry.TechnicalDetail));
        return builder.AppendLine().ToString();
    }

    private static string Sanitize(string value)
    {
        var clean = value.Replace("\r", " ").Replace("\n", " ");
        if (Uri.TryCreate(clean, UriKind.Absolute, out var uri))
            return new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.ToString();
        return clean.Length > 500 ? clean[..500] : clean;
    }

    private void TrimAndAppend(byte[] lineBytes)
    {
        var existing = File.Exists(path) ? File.ReadAllBytes(path) : [];
        var start = Math.Max(0, existing.Length - RetainedBytes);
        while (start < existing.Length && existing[start] != (byte)'\n') start++;
        if (start < existing.Length) start++;

        var temp = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough))
            {
                stream.Write(existing, start, existing.Length - start);
                stream.Write(lineBytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
