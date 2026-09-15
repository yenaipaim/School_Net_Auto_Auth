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
        if (settings.RecordedFlow is null || string.IsNullOrWhiteSpace(settings.SelectedProvider) || !settings.RecordedFlow.Providers.TryGetValue(settings.SelectedProvider, out var provider))
            return new(AuthenticationOutcome.RecordingRequired, "recording_missing");

        EdgeSession? session = null;
        try
        {
            session = await sessions.LaunchAsync(false, cancellationToken);
            var page = session.Context.Pages.FirstOrDefault() ?? await session.Context.NewPageAsync();
            var timeout = (float)settings.AuthenticationTimeout.TotalMilliseconds;
            await page.GotoAsync(settings.PortalUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout }).WaitAsync(cancellationToken);
            var username = resolver.Resolve(page, settings.RecordedFlow.Username);
            var password = resolver.Resolve(page, settings.RecordedFlow.Password);
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

            await resolver.Resolve(page, settings.RecordedFlow.Login).ClickAsync(new() { Timeout = timeout }).WaitAsync(cancellationToken);
            var providerLocator = resolver.Resolve(page, provider);
            await providerLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout }).WaitAsync(cancellationToken);
            await providerLocator.ClickAsync().WaitAsync(cancellationToken);

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
}
