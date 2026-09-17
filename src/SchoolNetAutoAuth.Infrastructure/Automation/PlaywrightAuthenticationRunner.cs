using Microsoft.Playwright;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class PlaywrightAuthenticationRunner(
    EdgeSessionFactory sessions,
    LocatorResolver resolver,
    ICredentialStore credentials,
    IConnectivityProbe probe,
    FailedEdgeSessionKeeper failedSessions) : IAuthenticationRunner
{
    public async Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken cancellationToken)
    {
        if (settings.RecordedSequence is null)
            return new(AuthenticationOutcome.RecordingRequired, "recording_missing");

        EdgeSession? session = null;
        try
        {
            session = await sessions.LaunchAsync(false, cancellationToken);
            var page = session.Context.Pages.FirstOrDefault() ?? await session.Context.NewPageAsync();
            var pagesByKey = new Dictionary<string, IPage>(StringComparer.Ordinal)
            {
                [settings.RecordedSequence.Credentials.PageKey] = page
            };
            var timeout = (float)settings.AuthenticationTimeout.TotalMilliseconds;
            await page.GotoAsync(settings.PortalUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout }).WaitAsync(cancellationToken);
            var username = resolver.Resolve(page, settings.RecordedSequence.Credentials.Username);
            var password = resolver.Resolve(page, settings.RecordedSequence.Credentials.Password);
            await username.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout }).WaitAsync(cancellationToken);
            await password.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout }).WaitAsync(cancellationToken);
            await Task.Delay(1500, cancellationToken);

            if (string.IsNullOrWhiteSpace(await username.InputValueAsync()) || string.IsNullOrWhiteSpace(await password.InputValueAsync()))
            {
                var credential = await credentials.ReadAsync(cancellationToken);
                if (credential is null) return new(AuthenticationOutcome.CredentialsRequired, "credentials_missing");
                await username.FillAsync(credential.Username).WaitAsync(cancellationToken);
                await password.FillAsync(credential.Password).WaitAsync(cancellationToken);
            }

            foreach (var step in RecordedSequenceOrderer.Order(settings.RecordedSequence.Clicks))
            {
                var targetPage = await ResolvePageAsync(session.Context, pagesByKey, step, settings.AuthenticationTimeout, cancellationToken);
                if (targetPage is null)
                    return new(AuthenticationOutcome.RecordingRequired, "recorded_page_unavailable");
                var locator = resolver.Resolve(targetPage, step.Locator);
                if (await locator.CountAsync() != 1)
                    return new(AuthenticationOutcome.RecordingRequired, "recorded_element_unavailable");
                await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout }).WaitAsync(cancellationToken);
                await locator.ClickAsync().WaitAsync(cancellationToken);
                await Task.Delay(250, cancellationToken);
            }

            var deadline = DateTime.UtcNow + settings.AuthenticationTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if ((await probe.CheckAsync(settings.ProbeUri, settings.ProbeTimeout, cancellationToken)).IsOnline)
                    return AuthenticationResult.Success();
                await Task.Delay(1000, cancellationToken);
            }
            if (attempt.KeepBrowserOpenOnFailure)
            {
                await failedSessions.ReplaceAsync(session);
                session = null;
            }
            return new(AuthenticationOutcome.Failed, "connectivity_not_restored");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(AuthenticationOutcome.Cancelled, "cancelled"); }
        catch (PlaywrightException)
        {
            if (session is not null && attempt.KeepBrowserOpenOnFailure) { await failedSessions.ReplaceAsync(session); session = null; }
            return new(AuthenticationOutcome.RecordingRequired, "portal_element_failed");
        }
        catch
        {
            if (session is not null && attempt.KeepBrowserOpenOnFailure) { await failedSessions.ReplaceAsync(session); session = null; }
            return new(AuthenticationOutcome.Failed, "browser_failed");
        }
        finally { if (session is not null) await session.DisposeAsync(); }
    }

    private static async Task<IPage?> ResolvePageAsync(
        IBrowserContext context,
        IDictionary<string, IPage> pagesByKey,
        RecordedClickStep step,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (pagesByKey.TryGetValue(step.PageKey, out var mapped) && !mapped.IsClosed && RecordedPageMatcher.Matches(mapped.Url, step.UrlPattern))
                return mapped;

            var candidate = context.Pages.FirstOrDefault(p => !p.IsClosed && RecordedPageMatcher.Matches(p.Url, step.UrlPattern));
            if (candidate is not null)
            {
                pagesByKey[step.PageKey] = candidate;
                return candidate;
            }
            await Task.Delay(100, cancellationToken);
        }
        return null;
    }
}
