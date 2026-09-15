using System.Text.Json;
using Microsoft.Playwright;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class PlaywrightActionRecorder(EdgeSessionFactory sessions, LocatorResolver resolver) : IActionRecorder
{
    private TaskCompletionSource<ElementDescriptor>? _capture;

    public async Task<RecordedPortalFlow> RecordAsync(RecorderRequest request, IProgress<RecorderStep> progress, CancellationToken cancellationToken)
    {
        await using var session = await sessions.LaunchAsync(false, cancellationToken);
        var page = session.Context.Pages.FirstOrDefault() ?? await session.Context.NewPageAsync();
        await page.ExposeFunctionAsync("schoolNetCapture", new Action<JsonElement>(Capture));
        await page.AddInitScriptAsync(CaptureScript);
        await page.GotoAsync(request.PortalUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded }).WaitAsync(cancellationToken);

        var username = await CaptureAsync(page, progress, "username", "请在 Edge 中点击账号输入框。", cancellationToken);
        var password = await CaptureAsync(page, progress, "password", "请在 Edge 中点击密码输入框。", cancellationToken);
        var login = await CaptureAsync(page, progress, "login", "请在 Edge 中点击登录按钮（本次点击只记录，不会提交）。", cancellationToken);
        await resolver.Resolve(page, login).ClickAsync().WaitAsync(cancellationToken);
        var provider = await CaptureAsync(page, progress, "provider", $"请点击运营商“{request.ProviderName}”（本次点击只记录）。", cancellationToken);
        return new(username, password, login, new Dictionary<string, RecordedLocator>(StringComparer.Ordinal) { [request.ProviderName] = provider }, DateTimeOffset.UtcNow);
    }

    private async Task<RecordedLocator> CaptureAsync(IPage page, IProgress<RecorderStep> progress, string key, string message, CancellationToken cancellationToken)
    {
        progress.Report(new(key, message));
        _capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.EvaluateAsync("window.__schoolNetCaptureArmed = true");
        var descriptor = await _capture.Task.WaitAsync(cancellationToken);
        foreach (var candidate in LocatorCandidateFactory.CreateCandidates(descriptor))
            if (await resolver.IsUniqueAsync(page, candidate)) return candidate;
        throw new InvalidDataException("无法为所选元素生成唯一定位规则。");
    }

    private void Capture(JsonElement value)
    {
        var descriptor = JsonSerializer.Deserialize<ElementDescriptor>(value.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (descriptor is not null) _capture?.TrySetResult(descriptor);
    }

    private const string CaptureScript = """
        (() => {
          if (window.__schoolNetCaptureInstalled) return;
          window.__schoolNetCaptureInstalled = true;
          document.addEventListener('click', event => {
            if (!window.__schoolNetCaptureArmed) return;
            window.__schoolNetCaptureArmed = false;
            event.preventDefault(); event.stopImmediatePropagation();
            const e = event.target.closest('input,button,a,select,[role]') || event.target;
            const label = e.labels && e.labels.length ? Array.from(e.labels).map(x => x.innerText).join(' ') : null;
            const role = e.getAttribute('role') || ({BUTTON:'button',A:'link',INPUT:e.type==='checkbox'?'checkbox':'textbox',SELECT:'combobox'})[e.tagName] || null;
            let cssPath = e.id ? '#' + CSS.escape(e.id) : (e.getAttribute('name') ? `${e.tagName.toLowerCase()}[name="${CSS.escape(e.getAttribute('name'))}"]` : e.tagName.toLowerCase());
            window.schoolNetCapture({ tagName:e.tagName.toLowerCase(), role, accessibleName:e.getAttribute('aria-label') || (e.tagName==='BUTTON'||e.tagName==='A' ? e.innerText : null), label, placeholder:e.getAttribute('placeholder'), text:(e.tagName==='BUTTON'||e.tagName==='A') ? e.innerText : null, cssPath });
          }, true);
        })();
        """;
}
