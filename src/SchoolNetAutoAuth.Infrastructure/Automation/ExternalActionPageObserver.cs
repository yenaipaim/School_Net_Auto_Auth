using System.Collections.Concurrent;
using Microsoft.Playwright;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public interface IExternalActionObserver
{
    Task<ExternalActionDetection?> InspectAsync(CancellationToken cancellationToken);
}

public sealed class ExternalActionPageObserver : IExternalActionObserver
{
    private const int MaximumBodyCharacters = 16 * 1024;
    private const string ModalSelector =
        "dialog:visible, [role=\"dialog\"]:visible, .modal:visible, .popup:visible, " +
        ".layui-layer:visible, .el-message-box:visible, .ant-modal:visible";

    private readonly IBrowserContext _context;
    private readonly PortalCredential? _credential;
    private readonly ConcurrentQueue<string> _dialogMessages = new();
    private readonly HashSet<IPage> _attachedPages = [];
    private readonly object _sync = new();

    public ExternalActionPageObserver(IBrowserContext context, PortalCredential? credential)
    {
        _context = context;
        _credential = credential;
        _context.Page += (_, page) => _ = AttachAsync(page);
    }

    public Task AttachAsync(IPage page)
    {
        lock (_sync)
        {
            if (!_attachedPages.Add(page)) return Task.CompletedTask;
        }
        page.Dialog += async (_, dialog) =>
        {
            _dialogMessages.Enqueue(dialog.Message);
            try { await dialog.DismissAsync(); }
            catch (PlaywrightException) { }
        };
        return Task.CompletedTask;
    }

    public async Task<ExternalActionDetection?> InspectAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>(_dialogMessages.ToArray());
        foreach (var page in _context.Pages.Where(page => !page.IsClosed))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AttachAsync(page);
            try
            {
                var modalTexts = await page.Locator(ModalSelector).AllInnerTextsAsync().WaitAsync(cancellationToken);
                candidates.AddRange(modalTexts);
                var body = await page.Locator("body").InnerTextAsync(new() { Timeout = 1000 }).WaitAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(body))
                    candidates.Add(body[..Math.Min(MaximumBodyCharacters, body.Length)]);
            }
            catch (PlaywrightException) { }
        }
        return ExternalActionClassifier.Classify(candidates, _credential);
    }
}
