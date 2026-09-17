using System.Text.Json;
using Microsoft.Playwright;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class PlaywrightActionRecorder(EdgeSessionFactory sessions, LocatorResolver resolver) : IActionRecorder
{
    private readonly object _sync = new();
    private readonly List<RecordedClickStep> _clicks = [];
    private TaskCompletionSource<ElementDescriptor>? _singleCapture;
    private RecordedCredentialLocators? _credentials;
    private int _pageCounter;
    private volatile bool _sequenceActive;
    private readonly TaskCompletionSource _recordingFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<RecordedClickSequence> RecordAsync(RecorderRequest request, IProgress<RecorderStep> progress, CancellationToken cancellationToken)
    {
        await using var session = await sessions.LaunchAsync(false, cancellationToken);
        var page = session.Context.Pages.FirstOrDefault() ?? await session.Context.NewPageAsync();
        var pageKey = $"page-{Interlocked.Increment(ref _pageCounter)}";
        await AttachPageAsync(page, pageKey);
        session.Context.Page += (_, newPage) => _ = AttachPageSafelyAsync(newPage, $"page-{Interlocked.Increment(ref _pageCounter)}");
        await page.GotoAsync(request.PortalUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded }).WaitAsync(cancellationToken);

        var username = await CaptureCredentialAsync(page, progress, "username", "请在 Edge 中点击账号输入框。", cancellationToken);
        var password = await CaptureCredentialAsync(page, progress, "password", "请在 Edge 中点击密码输入框。", cancellationToken);
        _credentials = new(username, password, pageKey, NormalizeUrl(page.Url));

        progress.Report(new("click-sequence", "请按正常认证流程点击页面控件；完成后回到软件点击“结束录制”。"));
        _sequenceActive = true;
        foreach (var activePage in session.Context.Pages)
            await ArmSequenceAsync(activePage);
        try
        {
            var stopTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completed = await Task.WhenAny(stopTask, _recordingFailure.Task);
            if (completed == _recordingFailure.Task) await _recordingFailure.Task;
            await stopTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && _credentials is not null)
        {
            lock (_sync)
                return new RecordedClickSequence(1, DateTimeOffset.UtcNow, _credentials, _clicks.OrderBy(x => x.Order).ToArray());
        }
        throw new OperationCanceledException(cancellationToken);
    }

    private async Task<RecordedLocator> CaptureCredentialAsync(IPage page, IProgress<RecorderStep> progress, string key, string message, CancellationToken cancellationToken)
    {
        progress.Report(new(key, message));
        _singleCapture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.EvaluateAsync("window.__schoolNetCaptureMode = 'single'; window.__schoolNetCaptureArmed = true;");
        var descriptor = await _singleCapture.Task.WaitAsync(cancellationToken);
        foreach (var candidate in LocatorCandidateFactory.CreateCandidates(descriptor))
            if (await resolver.IsUniqueAsync(page, candidate)) return candidate;
        throw new InvalidDataException("无法为输入框生成唯一定位规则。");
    }

    private async Task AttachPageAsync(IPage page, string pageKey)
    {
        await page.ExposeFunctionAsync("schoolNetCapture", new Func<JsonElement, Task>(value => CaptureAsync(page, pageKey, value)));
        var script = CaptureScript.Replace("__PAGE_KEY__", JsonSerializer.Serialize(pageKey));
        await page.AddInitScriptAsync(script);
        try { await page.EvaluateAsync(script); } catch (PlaywrightException) { }
        page.DOMContentLoaded += async (_, _) =>
        {
            if (_sequenceActive) await ArmSequenceAsync(page);
        };
        if (_sequenceActive) await ArmSequenceAsync(page);
    }

    private async Task AttachPageSafelyAsync(IPage page, string pageKey)
    {
        try { await AttachPageAsync(page, pageKey); }
        catch (Exception ex) { _recordingFailure.TrySetException(ex); }
    }

    private async Task CaptureAsync(IPage page, string pageKey, JsonElement value)
    {
        try
        {
            var descriptor = JsonSerializer.Deserialize<ElementDescriptor>(value.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (descriptor is null) return;
            descriptor = descriptor with { PageKey = pageKey, Url = page.Url };
            if (string.Equals(descriptor.TagName, "input", StringComparison.OrdinalIgnoreCase) || string.Equals(descriptor.Role, "textbox", StringComparison.OrdinalIgnoreCase))
            {
                _singleCapture?.TrySetResult(descriptor);
                return;
            }
            if (!RecordedClickFilter.ShouldReplay(descriptor)) return;
            RecordedLocator? selected = null;
            foreach (var candidate in LocatorCandidateFactory.CreateCandidates(descriptor))
            {
                if (await resolver.IsUniqueAsync(page, candidate)) { selected = candidate; break; }
            }
            if (selected is null) throw new InvalidDataException("所点击元素无法生成唯一定位规则，请使用带有明确文字或标签的控件。");
            lock (_sync) _clicks.Add(new RecordedClickStep(_clicks.Count + 1, pageKey, NormalizeUrl(page.Url), selected, Describe(descriptor)));
        }
        catch (Exception ex) { _recordingFailure.TrySetException(ex); throw; }
    }

    private static async Task ArmSequenceAsync(IPage page)
    {
        try { await page.EvaluateAsync("window.__schoolNetCaptureMode = 'sequence'; window.__schoolNetCaptureArmed = true;"); }
        catch (PlaywrightException) { }
    }

    private static string Describe(ElementDescriptor descriptor)
    {
        var value = string.IsNullOrWhiteSpace(descriptor.AccessibleName) ? descriptor.TagName : descriptor.AccessibleName;
        value = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return value[..Math.Min(80, value.Length)];
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        return new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.ToString().TrimEnd('/');
    }

    private const string CaptureScript = """
        (() => {
          if (window.__schoolNetCaptureInstalled) return;
          window.__schoolNetCaptureInstalled = true;
          window.__schoolNetCaptureArmed = false;
          window.__schoolNetCaptureMode = 'single';
          document.addEventListener('click', async event => {
            if (!window.__schoolNetCaptureArmed) return;
            const e = event.target.closest('input,button,a,select,textarea,[role]') || event.target;
            const label = e.labels && e.labels.length ? Array.from(e.labels).map(x => x.innerText).join(' ') : null;
            const role = e.getAttribute('role') || ({BUTTON:'button',A:'link',INPUT:e.type==='checkbox'?'checkbox':'textbox',SELECT:'combobox'})[e.tagName] || null;
            const cssPath = e.id ? '#' + CSS.escape(e.id) : (e.getAttribute('name') ? `${e.tagName.toLowerCase()}[name="${CSS.escape(e.getAttribute('name'))}"]` : e.tagName.toLowerCase());
            event.preventDefault(); event.stopImmediatePropagation();
            await window.schoolNetCapture({ tagName:e.tagName.toLowerCase(), role, accessibleName:e.getAttribute('aria-label') || (e.tagName==='BUTTON'||e.tagName==='A' ? e.innerText : null), label, placeholder:e.getAttribute('placeholder'), text:(e.tagName==='BUTTON'||e.tagName==='A' ? e.innerText : null), cssPath, pageKey:__PAGE_KEY__, url:location.href });
            if (window.__schoolNetCaptureMode === 'single') { window.__schoolNetCaptureArmed = false; return; }
            window.__schoolNetCaptureArmed = false;
            e.click();
            setTimeout(() => { window.__schoolNetCaptureArmed = true; }, 0);
          }, true);
        })();
        """;
}
