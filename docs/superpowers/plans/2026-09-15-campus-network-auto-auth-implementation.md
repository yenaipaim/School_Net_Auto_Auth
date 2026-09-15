# 校园网自动认证 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一款 Windows 10/11 原生托盘程序，在连接指定 Wi-Fi 且无法访问互联网时，使用 Playwright 控制系统 Edge 完成可录制的校园网认证流程。

**Architecture:** 解决方案分为 Core、Infrastructure 和 WPF App 三层。Core 保存配置模型、接口和认证状态机；Infrastructure 实现 Windows Wi-Fi、联网探测、凭据管理器、Playwright、启动项与日志；App 负责托盘、设置向导、录制交互和依赖装配。所有外部依赖都通过接口进入核心流程，以便用 xUnit 和本地模拟门户完整测试。

**Tech Stack:** .NET 8、WPF、C#、Microsoft.Playwright for .NET、xUnit、ASP.NET Core 本地测试门户、System.Windows.Forms.NotifyIcon、Windows Credential Manager、Windows Native WLAN API、Inno Setup。

**Spec:** `docs/superpowers/specs/2026-09-15-campus-network-auto-auth-design.md`

## Global Constraints

- 目标平台仅为 Windows 10/11 x64。
- 应用名称固定为“校园网自动认证”，程序集根命名空间为 `SchoolNetAutoAuth`。
- 默认目标 SSID 为 `NSU-SDN`。
- 默认认证入口为 `http://2.2.2.2`。
- 默认联网检测地址为 `https://www.yuanshen.com`。
- 页面动作顺序固定为：打开入口、填写或复用凭据、点击登录、点击预选运营商、验证联网。
- 自动化必须使用系统安装的 Microsoft Edge `msedge` 通道和独立持久化用户目录，不接管日常 Edge 默认配置。
- 密码只允许进入 Edge 密码存储或 Windows Credential Manager；JSON 配置和日志不得包含秘密。
- Playwright 截图、视频和 Trace 默认关闭。
- 非目标 SSID 不允许执行门户自动化；同一时间只允许一个认证任务。
- 成功后关闭自动化 Edge；最终失败后保留 Edge 并通知用户。
- 首次安装默认启用开机自启，设置中允许随时关闭。
- 所有业务逻辑以异步 API 和 `CancellationToken` 支持取消。
- 当前机器在 2026-09-15 只有 .NET 8.0.30 运行时，没有 .NET SDK；执行 Task 1 前必须安装 .NET 8 SDK。

---

## Planned File Structure

```text
SchoolNetAutoAuth.sln
Directory.Build.props
src/
  SchoolNetAutoAuth.Core/
    SchoolNetAutoAuth.Core.csproj
    Configuration/AppSettings.cs
    Configuration/RecordedLocator.cs
    Configuration/RecordedPortalFlow.cs
    Authentication/AuthenticationState.cs
    Authentication/AuthenticationResult.cs
    Authentication/AuthenticationCoordinator.cs
    Authentication/BackgroundAuthenticationService.cs
    Contracts/IAuthenticationRunner.cs
    Contracts/IAppDataCleaner.cs
    Contracts/IConnectivityProbe.cs
    Contracts/ICredentialStore.cs
    Contracts/IDelay.cs
    Contracts/INetworkMonitor.cs
    Contracts/INotifier.cs
    Contracts/ISettingsStore.cs
    Contracts/IStartupManager.cs
    Diagnostics/IAppLogger.cs
  SchoolNetAutoAuth.Infrastructure/
    SchoolNetAutoAuth.Infrastructure.csproj
    Automation/EdgeSessionFactory.cs
    Automation/LocatorResolver.cs
    Automation/LocatorCandidateFactory.cs
    Automation/PlaywrightActionRecorder.cs
    Automation/PlaywrightAuthenticationRunner.cs
    Automation/FailedEdgeSessionKeeper.cs
    Configuration/JsonSettingsStore.cs
    Connectivity/HttpConnectivityProbe.cs
    Credentials/WindowsCredentialStore.cs
    Diagnostics/SafeFileLogger.cs
    Maintenance/AppDataCleaner.cs
    Network/NativeWifiClient.cs
    Network/WindowsWifiMonitor.cs
    Startup/RegistryStartupManager.cs
    Time/SystemDelay.cs
  SchoolNetAutoAuth.App/
    SchoolNetAutoAuth.App.csproj
    App.xaml
    App.xaml.cs
    AppCompositionRoot.cs
    Assets/App.ico
    Tray/TrayIconController.cs
    ViewModels/ObservableObject.cs
    ViewModels/RelayCommand.cs
    ViewModels/SettingsViewModel.cs
    ViewModels/SetupWizardViewModel.cs
    Views/SettingsWindow.xaml
    Views/SettingsWindow.xaml.cs
    Views/SetupWizardWindow.xaml
    Views/SetupWizardWindow.xaml.cs
tests/
  SchoolNetAutoAuth.Core.Tests/
    Configuration/AppSettingsTests.cs
    Authentication/AuthenticationCoordinatorTests.cs
  SchoolNetAutoAuth.Infrastructure.Tests/
    Configuration/JsonSettingsStoreTests.cs
    Connectivity/HttpConnectivityProbeTests.cs
    Credentials/WindowsCredentialStoreTests.cs
    Network/WindowsWifiMonitorTests.cs
    Automation/LocatorCandidateFactoryTests.cs
    Automation/PlaywrightAuthenticationRunnerTests.cs
  SchoolNetAutoAuth.App.Tests/
    ViewModels/SettingsViewModelTests.cs
  SchoolNetAutoAuth.TestPortal/
    SchoolNetAutoAuth.TestPortal.csproj
    Program.cs
    wwwroot/index.html
    wwwroot/providers.html
installer/
  SchoolNetAutoAuth.iss
scripts/
  publish.ps1
README.md
```

`Core` 不引用 WPF、Playwright 或 Windows API。`Infrastructure` 只通过 Core 接口暴露能力。`App` 是唯一创建窗口、托盘图标和具体实现实例的项目。

---

### Task 1: Bootstrap the Solution and Core Configuration Model

**Files:**
- Create: `SchoolNetAutoAuth.sln`
- Create: `Directory.Build.props`
- Create: `src/SchoolNetAutoAuth.Core/SchoolNetAutoAuth.Core.csproj`
- Create: `src/SchoolNetAutoAuth.Infrastructure/SchoolNetAutoAuth.Infrastructure.csproj`
- Create: `src/SchoolNetAutoAuth.App/SchoolNetAutoAuth.App.csproj`
- Create: `tests/SchoolNetAutoAuth.Core.Tests/SchoolNetAutoAuth.Core.Tests.csproj`
- Create: `tests/SchoolNetAutoAuth.Infrastructure.Tests/SchoolNetAutoAuth.Infrastructure.Tests.csproj`
- Create: `tests/SchoolNetAutoAuth.App.Tests/SchoolNetAutoAuth.App.Tests.csproj`
- Create: `src/SchoolNetAutoAuth.Core/Configuration/AppSettings.cs`
- Create: `src/SchoolNetAutoAuth.Core/Configuration/RecordedLocator.cs`
- Create: `src/SchoolNetAutoAuth.Core/Configuration/RecordedPortalFlow.cs`
- Test: `tests/SchoolNetAutoAuth.Core.Tests/Configuration/AppSettingsTests.cs`

**Interfaces:**
- Produces: `AppSettings.CreateDefault()`, `AppSettings.Validate()`, `ValidationResult`.
- Produces: the project references used by every later task.

- [ ] **Step 1: Install and verify the .NET 8 SDK**

Run in an approved elevated/network-enabled terminal:

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e --source winget
dotnet --version
```

Expected: `dotnet --version` prints `8.0.x` and `dotnet --list-sdks` contains an 8.0 SDK.

- [ ] **Step 2: Initialize Git if the workspace is not already a repository**

```powershell
git rev-parse --is-inside-work-tree
git init
```

Only run `git init` when the first command reports that the directory is not a repository. Do not overwrite an existing repository.

- [ ] **Step 3: Scaffold the solution and projects**

```powershell
dotnet new sln -n SchoolNetAutoAuth
dotnet new classlib -n SchoolNetAutoAuth.Core -o src/SchoolNetAutoAuth.Core -f net8.0
dotnet new classlib -n SchoolNetAutoAuth.Infrastructure -o src/SchoolNetAutoAuth.Infrastructure -f net8.0
dotnet new wpf -n SchoolNetAutoAuth.App -o src/SchoolNetAutoAuth.App -f net8.0-windows
dotnet new xunit -n SchoolNetAutoAuth.Core.Tests -o tests/SchoolNetAutoAuth.Core.Tests -f net8.0
dotnet new xunit -n SchoolNetAutoAuth.Infrastructure.Tests -o tests/SchoolNetAutoAuth.Infrastructure.Tests -f net8.0
dotnet new xunit -n SchoolNetAutoAuth.App.Tests -o tests/SchoolNetAutoAuth.App.Tests -f net8.0
dotnet sln add src/SchoolNetAutoAuth.Core/SchoolNetAutoAuth.Core.csproj src/SchoolNetAutoAuth.Infrastructure/SchoolNetAutoAuth.Infrastructure.csproj src/SchoolNetAutoAuth.App/SchoolNetAutoAuth.App.csproj tests/SchoolNetAutoAuth.Core.Tests/SchoolNetAutoAuth.Core.Tests.csproj tests/SchoolNetAutoAuth.Infrastructure.Tests/SchoolNetAutoAuth.Infrastructure.Tests.csproj tests/SchoolNetAutoAuth.App.Tests/SchoolNetAutoAuth.App.Tests.csproj
dotnet add src/SchoolNetAutoAuth.Infrastructure reference src/SchoolNetAutoAuth.Core
dotnet add src/SchoolNetAutoAuth.App reference src/SchoolNetAutoAuth.Core src/SchoolNetAutoAuth.Infrastructure
dotnet add tests/SchoolNetAutoAuth.Core.Tests reference src/SchoolNetAutoAuth.Core
dotnet add tests/SchoolNetAutoAuth.Infrastructure.Tests reference src/SchoolNetAutoAuth.Core src/SchoolNetAutoAuth.Infrastructure
dotnet add tests/SchoolNetAutoAuth.App.Tests reference src/SchoolNetAutoAuth.App
dotnet add src/SchoolNetAutoAuth.Infrastructure package Microsoft.Playwright
```

Set `UseWPF` and `UseWindowsForms` to `true` in the App project. Set App, App.Tests, Infrastructure, and Infrastructure.Tests targets to `net8.0-windows`; Core and Core.Tests remain `net8.0`.
Delete template files `Class1.cs` and `UnitTest1.cs` after the project references are established.

- [ ] **Step 4: Add shared compiler rules**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Write failing default-settings tests**

```csharp
[Fact]
public void CreateDefault_UsesApprovedPortalValues()
{
    var settings = AppSettings.CreateDefault();

    Assert.Equal("NSU-SDN", settings.TargetSsid);
    Assert.Equal(new Uri("http://2.2.2.2"), settings.PortalUri);
    Assert.Equal(new Uri("https://www.yuanshen.com"), settings.ProbeUri);
    Assert.True(settings.StartWithWindows);
}

[Fact]
public void Validate_RejectsNonPositiveRetryValues()
{
    var settings = AppSettings.CreateDefault() with
    {
        RetryInterval = TimeSpan.Zero,
        MaximumAttempts = 0
    };

    Assert.False(settings.Validate().IsValid);
}
```

- [ ] **Step 6: Run tests and verify the expected failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Core.Tests --filter AppSettingsTests`

Expected: FAIL because `AppSettings` does not exist.

- [ ] **Step 7: Implement the minimal immutable settings model**

```csharp
public sealed record AppSettings(
    int SchemaVersion,
    string TargetSsid,
    Uri PortalUri,
    Uri ProbeUri,
    string? SelectedProvider,
    TimeSpan NetworkCheckInterval,
    TimeSpan ProbeTimeout,
    TimeSpan AuthenticationTimeout,
    TimeSpan RetryInterval,
    int MaximumAttempts,
    bool StartWithWindows,
    RecordedPortalFlow? RecordedFlow)
{
    public static AppSettings CreateDefault() => new(
        1,
        "NSU-SDN",
        new Uri("http://2.2.2.2"),
        new Uri("https://www.yuanshen.com"),
        null,
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(45),
        TimeSpan.FromSeconds(10),
        3,
        true,
        null);

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(TargetSsid)) errors.Add("目标 Wi-Fi 不能为空。");
        if (!PortalUri.IsAbsoluteUri) errors.Add("认证地址必须是完整地址。");
        if (ProbeUri.Scheme != Uri.UriSchemeHttps) errors.Add("联网检测地址必须使用 HTTPS。");
        if (RetryInterval <= TimeSpan.Zero) errors.Add("重试间隔必须大于零。");
        if (MaximumAttempts < 1) errors.Add("最大重试次数至少为 1。");
        return new ValidationResult(errors);
    }
}

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
```

Add the locator models used by `AppSettings` from the first commit:

```csharp
public enum LocatorStrategy { Role, Label, Placeholder, Text, Css }

public sealed record RecordedLocator(
    LocatorStrategy Strategy,
    string Selector,
    string? Name = null,
    bool Exact = true);

public sealed record RecordedPortalFlow(
    RecordedLocator Username,
    RecordedLocator Password,
    RecordedLocator Login,
    IReadOnlyDictionary<string, RecordedLocator> Providers,
    DateTimeOffset RecordedAtUtc);
```

- [ ] **Step 8: Run all tests and commit**

```powershell
dotnet test SchoolNetAutoAuth.sln
git add SchoolNetAutoAuth.sln Directory.Build.props src tests
git commit -m "build: bootstrap campus network app"
```

Expected: all tests pass and package lock files are committed.

---

### Task 2: Persist and Validate Settings Safely

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Contracts/ISettingsStore.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Configuration/JsonSettingsStore.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Configuration/JsonSettingsStoreTests.cs`

**Interfaces:**
- Produces: `Task<AppSettings> ISettingsStore.LoadAsync(CancellationToken)`.
- Produces: `Task SaveAsync(AppSettings settings, CancellationToken)`.
- Consumes: `AppSettings.Validate()` from Task 1.

- [ ] **Step 1: Write failing persistence tests**

Test these exact cases using a unique temporary directory per test:

```csharp
[Fact]
public async Task SaveThenLoad_RoundTripsValidatedSettings()
{
    var store = CreateStore();
    var expected = AppSettings.CreateDefault() with { TargetSsid = "Campus-Test" };

    await store.SaveAsync(expected, CancellationToken.None);
    var actual = await store.LoadAsync(CancellationToken.None);

    Assert.Equal(expected, actual);
}

[Fact]
public async Task Load_WhenJsonIsCorrupt_BacksUpFileAndReturnsDefaults()
{
    var store = CreateStoreWithRawJson("{broken");

    var actual = await store.LoadAsync(CancellationToken.None);

    Assert.Equal(AppSettings.CreateDefault(), actual);
    Assert.Single(Directory.GetFiles(TestDirectory, "settings.corrupt-*.json"));
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter JsonSettingsStoreTests`

Expected: FAIL because `ISettingsStore` and `JsonSettingsStore` do not exist.

- [ ] **Step 3: Implement atomic JSON persistence**

Implement `JsonSettingsStore` with these rules:

```csharp
public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
{
    var validation = settings.Validate();
    if (!validation.IsValid)
        throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));

    Directory.CreateDirectory(_directory);
    var temporaryPath = _settingsPath + ".tmp";
    await using (var stream = File.Create(temporaryPath))
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
    File.Move(temporaryPath, _settingsPath, overwrite: true);
}
```

On missing file, return defaults. On invalid JSON or invalid settings, rename the original to `settings.corrupt-yyyyMMddHHmmss.json`, then return defaults. Never serialize credential values.

- [ ] **Step 4: Run focused and full tests**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter JsonSettingsStoreTests
dotnet test SchoolNetAutoAuth.sln
```

- [ ] **Step 5: Commit**

```powershell
git add src/SchoolNetAutoAuth.Core/Contracts src/SchoolNetAutoAuth.Infrastructure/Configuration tests/SchoolNetAutoAuth.Infrastructure.Tests/Configuration
git commit -m "feat: persist validated app settings"
```

---

### Task 3: Detect the Current Wi-Fi with the Windows WLAN API

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Contracts/INetworkMonitor.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Network/NativeWifiClient.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Network/WindowsWifiMonitor.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Network/WindowsWifiMonitorTests.cs`

**Interfaces:**
- Produces: `NetworkSnapshot` record with `string? Ssid` and `bool IsConnected`.
- Produces: `Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken)` and `event EventHandler? NetworkChanged`.

- [ ] **Step 1: Define the contract and write failing adapter tests**

```csharp
public sealed record NetworkSnapshot(string? Ssid, bool IsConnected);

public interface INetworkMonitor
{
    event EventHandler? NetworkChanged;
    Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
```

Test the public monitor against a fake `INativeWifiClient`:

```csharp
[Theory]
[InlineData("NSU-SDN", true)]
[InlineData(null, false)]
public async Task GetSnapshot_MapsNativeConnection(string? ssid, bool connected)
{
    var monitor = new WindowsWifiMonitor(new FakeNativeWifiClient(ssid, connected));
    Assert.Equal(new NetworkSnapshot(ssid, connected), await monitor.GetSnapshotAsync(default));
}
```

- [ ] **Step 2: Run the test and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter WindowsWifiMonitorTests`

Expected: FAIL because the monitor is missing.

- [ ] **Step 3: Implement Native WLAN interop**

Use `wlanapi.dll` P/Invoke for `WlanOpenHandle`, `WlanEnumInterfaces`, `WlanQueryInterface` with `wlan_intf_opcode_current_connection`, `WlanRegisterNotification`, `WlanFreeMemory`, and `WlanCloseHandle`.

Convert `DOT11_SSID.ucSSID[0..uSSIDLength]` with UTF-8, never by reading localized `netsh` output. Wrap the native handle in `SafeHandle`. Raise `NetworkChanged` for ACM connection complete, disconnected, and interface arrival/removal notifications.

- [ ] **Step 4: Add a Windows-only smoke test**

```csharp
[Fact]
public async Task NativeClient_ReturnsWithoutThrowingOnWindows()
{
    if (!OperatingSystem.IsWindows()) return;
    using var client = new NativeWifiClient();
    var snapshot = await client.GetCurrentConnectionAsync(default);
    Assert.True(snapshot is not null);
}
```

The smoke test may return a disconnected snapshot; it must not require a specific SSID.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter "WindowsWifiMonitorTests|NativeClient"
dotnet test SchoolNetAutoAuth.sln
git add src/SchoolNetAutoAuth.Core/Contracts/INetworkMonitor.cs src/SchoolNetAutoAuth.Infrastructure/Network tests/SchoolNetAutoAuth.Infrastructure.Tests/Network
git commit -m "feat: detect current wifi network"
```

---

### Task 4: Implement the HTTPS Connectivity Probe

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IConnectivityProbe.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Connectivity/HttpConnectivityProbe.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Connectivity/HttpConnectivityProbeTests.cs`

**Interfaces:**
- Produces: `Task<ConnectivityResult> CheckAsync(Uri probeUri, TimeSpan timeout, CancellationToken)`.
- Produces: `ConnectivityResult(bool IsOnline, string Reason)`.

- [ ] **Step 1: Write failing tests using a stub HttpMessageHandler**

Cover response, DNS/network exception, timeout, and status 511:

```csharp
[Theory]
[InlineData(200, true)]
[InlineData(403, true)]
[InlineData(511, false)]
public async Task Check_ClassifiesHttpResponses(int statusCode, bool expectedOnline)
{
    var probe = CreateProbeReturning((HttpStatusCode)statusCode);
    var result = await probe.CheckAsync(new Uri("https://www.yuanshen.com"), TimeSpan.FromSeconds(1), default);
    Assert.Equal(expectedOnline, result.IsOnline);
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter HttpConnectivityProbeTests`

- [ ] **Step 3: Implement the probe**

Create an `HttpClient` with `AllowAutoRedirect = false`. Send `GET` with `HttpCompletionOption.ResponseHeadersRead`. Any received status except `511 NetworkAuthenticationRequired` proves external HTTPS reachability; `HttpRequestException`, TLS failure, DNS failure, or timeout returns `IsOnline = false` with a non-secret reason code.

Reject non-HTTPS probe URIs before sending.

- [ ] **Step 4: Run tests and commit**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter HttpConnectivityProbeTests
dotnet test SchoolNetAutoAuth.sln
git add src/SchoolNetAutoAuth.Core/Contracts/IConnectivityProbe.cs src/SchoolNetAutoAuth.Infrastructure/Connectivity tests/SchoolNetAutoAuth.Infrastructure.Tests/Connectivity
git commit -m "feat: add external connectivity probe"
```

---

### Task 5: Store Credentials in Windows Credential Manager

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Contracts/ICredentialStore.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Credentials/WindowsCredentialStore.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Credentials/WindowsCredentialStoreTests.cs`

**Interfaces:**
- Produces: `PortalCredential(string Username, string Password)`.
- Produces: `ReadAsync`, `WriteAsync`, and `DeleteAsync` keyed by constant target `SchoolNetAutoAuth/Portal`.

- [ ] **Step 1: Write failing credential lifecycle tests**

Use a per-test target name such as `SchoolNetAutoAuth.Tests/{Guid.NewGuid():N}` and delete it in `finally`:

```csharp
[Fact]
public async Task WriteReadDelete_RoundTripsCredential()
{
    var target = $"SchoolNetAutoAuth.Tests/{Guid.NewGuid():N}";
    var store = new WindowsCredentialStore(target);
    try
    {
        await store.WriteAsync(new PortalCredential("student", "secret"), default);
        Assert.Equal(new PortalCredential("student", "secret"), await store.ReadAsync(default));
        await store.DeleteAsync(default);
        Assert.Null(await store.ReadAsync(default));
    }
    finally { await store.DeleteAsync(default); }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter WindowsCredentialStoreTests`

- [ ] **Step 3: Implement `CredWriteW`, `CredReadW`, `CredDeleteW`, and `CredFree` interop**

Use `CRED_TYPE_GENERIC` and `CRED_PERSIST_LOCAL_MACHINE`. Marshal password bytes as UTF-16 and zero temporary unmanaged buffers in `finally`. Map Windows error 1168 to “credential not found”; throw `Win32Exception` for other error codes.

Do not implement a plaintext fallback.

- [ ] **Step 4: Run the lifecycle test twice and inspect the config directory**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter WindowsCredentialStoreTests
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter WindowsCredentialStoreTests
rg -n "student|secret" "$env:LOCALAPPDATA\SchoolNetAutoAuth" -g "*.json" -g "*.log"
```

Expected: both test runs pass; `rg` returns no secret matches or reports that the app directory does not yet exist.

- [ ] **Step 5: Commit**

```powershell
git add src/SchoolNetAutoAuth.Core/Contracts/ICredentialStore.cs src/SchoolNetAutoAuth.Infrastructure/Credentials tests/SchoolNetAutoAuth.Infrastructure.Tests/Credentials
git commit -m "feat: secure portal credentials on Windows"
```

---

### Task 6: Build the Authentication Coordinator and Retry State Machine

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Authentication/AuthenticationState.cs`
- Create: `src/SchoolNetAutoAuth.Core/Authentication/AuthenticationResult.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IAuthenticationRunner.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IDelay.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/INotifier.cs`
- Create: `src/SchoolNetAutoAuth.Core/Authentication/AuthenticationCoordinator.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Time/SystemDelay.cs`
- Test: `tests/SchoolNetAutoAuth.Core.Tests/Authentication/AuthenticationCoordinatorTests.cs`

**Interfaces:**
- Produces: `Task EvaluateAsync(CancellationToken)` guarded against concurrent runs.
- Produces: `AuthenticationState State` and `event EventHandler<AuthenticationState>? StateChanged`.
- Produces: `Task<AuthenticationResult> IAuthenticationRunner.AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken)` where `AuthenticationAttempt` contains the one-based attempt number, maximum attempts, and `KeepBrowserOpenOnFailure`.
- Consumes: settings, network monitor, connectivity probe, runner, delay, notifier.

- [ ] **Step 1: Define outcomes and write failing state tests**

```csharp
public enum AuthenticationState
{
    WaitingForTargetWifi,
    CheckingConnectivity,
    Online,
    Authenticating,
    WaitingForCredentials,
    RetryDelay,
    ActionRequired
}

public enum AuthenticationOutcome
{
    Succeeded,
    Failed,
    CredentialsRequired,
    RecordingRequired,
    Cancelled
}

public sealed record AuthenticationResult(AuthenticationOutcome Outcome, string ReasonCode);

public sealed record AuthenticationAttempt(
    int Number,
    int Maximum,
    bool KeepBrowserOpenOnFailure);
```

Write tests proving:

- SSID mismatch never calls the probe or runner.
- An online probe stops before browser automation.
- Three configured attempts call the runner exactly three times.
- Success on attempt two stops retries and sets `Online`.
- `CredentialsRequired` sets `WaitingForCredentials` without retry.
- A concurrent second `EvaluateAsync` call does not start another runner.
- Cancellation caused by SSID change does not send a failure notification.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Core.Tests --filter AuthenticationCoordinatorTests`

- [ ] **Step 3: Implement the coordinator**

Use a `SemaphoreSlim(1, 1)` with non-blocking acquisition. The algorithm must re-read settings, SSID, and connectivity before every retry. Use `IDelay.DelayAsync(settings.RetryInterval, token)` rather than `Task.Delay` directly.

The coordinator does not create browser objects and never reads credentials. For each call it passes `KeepBrowserOpenOnFailure = attemptNumber == settings.MaximumAttempts`; non-final failures therefore close their Edge session, while the last failure remains visible. The coordinator interprets `AuthenticationResult`, emits state changes, and sends only the final failure notification.

- [ ] **Step 4: Run focused tests, then mutation checks by changing one expectation**

Run the focused suite and confirm pass. Temporarily change the expected attempt count from 3 to 2 and confirm a failure, then restore it and rerun:

```powershell
dotnet test tests/SchoolNetAutoAuth.Core.Tests --filter AuthenticationCoordinatorTests
dotnet test SchoolNetAutoAuth.sln
```

- [ ] **Step 5: Commit**

```powershell
git add src/SchoolNetAutoAuth.Core/Authentication src/SchoolNetAutoAuth.Core/Contracts src/SchoolNetAutoAuth.Infrastructure/Time tests/SchoolNetAutoAuth.Core.Tests/Authentication
git commit -m "feat: coordinate wifi authentication retries"
```

---

### Task 7: Model and Resolve Recorded Playwright Locators

**Files:**
- Modify: `src/SchoolNetAutoAuth.Core/Configuration/RecordedLocator.cs`
- Modify: `src/SchoolNetAutoAuth.Core/Configuration/RecordedPortalFlow.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/LocatorCandidateFactory.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/LocatorResolver.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/LocatorCandidateFactoryTests.cs`

**Interfaces:**
- Produces: `RecordedLocator`, `RecordedPortalFlow`, `ElementDescriptor`.
- Produces: `IReadOnlyList<RecordedLocator> CreateCandidates(ElementDescriptor descriptor)`.
- Produces: `ILocator Resolve(IPage page, RecordedLocator locator)`.

- [ ] **Step 1: Write failing candidate-priority tests**

```csharp
[Fact]
public void CreateCandidates_PrefersRoleAndAccessibleName()
{
    var descriptor = new ElementDescriptor(
        TagName: "button", Role: "button", AccessibleName: "登录",
        Label: null, Placeholder: null, Text: "登录", CssPath: "#login");

    var candidates = LocatorCandidateFactory.CreateCandidates(descriptor);

    Assert.Equal(LocatorStrategy.Role, candidates[0].Strategy);
    Assert.Equal("button", candidates[0].Selector);
    Assert.Equal("登录", candidates[0].Name);
}
```

Also test label before placeholder, text before CSS, whitespace normalization, empty candidate removal, and dictionary serialization of provider names.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter LocatorCandidateFactoryTests`

- [ ] **Step 3: Implement candidate generation and locator resolution**

Normalize candidate values by trimming and collapsing whitespace, remove duplicates, and return candidates in the tested priority order. `LocatorResolver` maps the existing `RecordedLocator` strategies to `GetByRole`, `GetByLabel`, `GetByPlaceholder`, `GetByText`, or `Locator`; parse stored role names into Playwright `AriaRole` values and reject unknown role strings. Do not use XPath.

- [ ] **Step 4: Add a resolver uniqueness helper**

Implement:

```csharp
public async Task<bool> IsUniqueAsync(IPage page, RecordedLocator locator)
    => await Resolve(page, locator).CountAsync() == 1;
```

The recorder in Task 8 may only save a candidate that passes this check.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter LocatorCandidateFactoryTests
dotnet test SchoolNetAutoAuth.sln
git add src/SchoolNetAutoAuth.Core/Configuration src/SchoolNetAutoAuth.Infrastructure/Automation tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation
git commit -m "feat: model reliable portal locators"
```

---

### Task 8: Implement the Guided Action Recorder

**Files:**
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/EdgeSessionFactory.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/PlaywrightActionRecorder.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IActionRecorder.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/PlaywrightActionRecorderTests.cs`

**Interfaces:**
- Produces: `Task<RecordedPortalFlow> RecordAsync(RecorderRequest request, IProgress<RecorderStep>, CancellationToken)`.
- Produces: `Task<bool> ValidateAsync(RecordedPortalFlow flow, Uri portalUri, CancellationToken)`.
- Produces: `Task<EdgeSession> IEdgeSessionFactory.LaunchAsync(bool headless, CancellationToken)`; `EdgeSession` owns both `IPlaywright` and `IBrowserContext` and implements `IAsyncDisposable`.
- Consumes: `LocatorCandidateFactory`, `LocatorResolver`, and a persistent Edge session.

- [ ] **Step 1: Write failing tests against a local HTML fixture**

Create a temporary two-page portal with labeled username/password inputs, a “登录” button, and provider buttons. Tests must prove the recorder:

- records username and password without reading their values;
- records login, then uses the captured locator to advance to provider selection;
- records multiple named providers without activating them during capture;
- chooses the first unique candidate;
- rejects ambiguous elements and reports the current recorder step;
- validates every saved locator on a new browser page.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter PlaywrightActionRecorderTests`

Expected: FAIL because the recorder and Edge session factory are missing.

- [ ] **Step 3: Implement the persistent Edge session factory**

```csharp
public async Task<EdgeSession> LaunchAsync(bool headless, CancellationToken cancellationToken)
{
    var playwright = await Playwright.CreateAsync();
    try
    {
        var context = await playwright.Chromium.LaunchPersistentContextAsync(
            _userDataDirectory,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Channel = "msedge",
                Headless = headless,
                Args = ["--no-first-run"],
                ViewportSize = null
            });
        return new EdgeSession(playwright, context);
    }
    catch
    {
        playwright.Dispose();
        throw;
    }
}
```

Register cancellation to close the context while a launch or navigation is active. `EdgeSession.DisposeAsync` closes the context before disposing `IPlaywright`, so the driver process is always released.

- [ ] **Step 4: Implement guided element capture**

Expose a binding named `schoolNetCapture` and install a capture script in every document. While capture is armed, the script prevents the selected click, extracts only structural metadata, and sends this shape to .NET:

```javascript
{
  tagName: element.tagName.toLowerCase(),
  role: element.getAttribute('role'),
  accessibleName: element.getAttribute('aria-label') || element.innerText,
  label: associatedLabelText,
  placeholder: element.getAttribute('placeholder'),
  text: element.innerText,
  cssPath: stableCssPath
}
```

Never include `value`, `textContent` from password inputs, cookies, headers, or full HTML. For each wizard step, generate candidates, select the first unique candidate, visually outline the matched element, and return the locator to the WPF view model.

- [ ] **Step 5: Run recorder tests headed and headless**

```powershell
dotnet build src/SchoolNetAutoAuth.Infrastructure
pwsh src/SchoolNetAutoAuth.Infrastructure/bin/Debug/net8.0-windows/playwright.ps1 install chromium
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter PlaywrightActionRecorderTests
dotnet test SchoolNetAutoAuth.sln
```

The CI path may use Playwright Chromium; the Windows manual path must use installed Edge.

- [ ] **Step 6: Commit**

```powershell
git add src/SchoolNetAutoAuth.Core/Contracts/IActionRecorder.cs src/SchoolNetAutoAuth.Infrastructure/Automation tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation
git commit -m "feat: record portal actions with Playwright"
```

---

### Task 9: Automate Edge Authentication with Credential Fallback

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Diagnostics/IAppLogger.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/PlaywrightAuthenticationRunner.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/FailedEdgeSessionKeeper.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/PlaywrightAuthenticationRunnerTests.cs`

**Interfaces:**
- Implements: `Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken)`.
- Consumes: `EdgeSessionFactory`, `LocatorResolver`, `ICredentialStore`, `IConnectivityProbe`, `IAppLogger`.

Define `IAppLogger` here as `void Log(AppEventId eventId, IReadOnlyDictionary<string,string>? fields = null)`. Add the fixed `AppEventId` enum values used by the runner; Task 10 provides the file implementation.

`FailedEdgeSessionKeeper` owns at most one preserved `EdgeSession`. `ReplaceAsync` disposes the previously preserved session before storing a new one, `CloseAsync` disposes the current session, and application shutdown always calls `DisposeAsync`.

- [ ] **Step 1: Write failing integration tests against the local portal**

Cover these exact paths:

1. Username and password are already populated: do not call the credential store or overwrite inputs.
2. Inputs remain empty after the autofill wait: read Credential Manager and fill both fields.
3. No browser values and no stored credential: return `CredentialsRequired` before clicking.
4. Login locator is missing or ambiguous: return `RecordingRequired`.
5. Selected provider is absent from the recorded mapping: return `RecordingRequired`.
6. Click login, wait for provider page, click the configured provider, then return success only after the connectivity probe reports online.
7. Cancellation closes the session without converting cancellation into a failure.
8. A non-final failed attempt disposes its Edge session; a final failed attempt with `KeepBrowserOpenOnFailure = true` transfers ownership to a failure-session keeper; success disposes it.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter PlaywrightAuthenticationRunnerTests`

- [ ] **Step 3: Implement the authentication sequence**

Use locator actions with explicit timeouts; do not use arbitrary sleeps except the configurable short autofill grace period. Determine whether both input values are non-empty with `InputValueAsync`. Fill only when either required field is empty.

```csharp
var username = _resolver.Resolve(page, flow.Username);
var password = _resolver.Resolve(page, flow.Password);
await username.WaitForAsync(new() { State = WaitForSelectorState.Visible });
await password.WaitForAsync(new() { State = WaitForSelectorState.Visible });
await _delay.DelayAsync(_autofillGracePeriod, cancellationToken);

if (string.IsNullOrWhiteSpace(await username.InputValueAsync()) ||
    string.IsNullOrWhiteSpace(await password.InputValueAsync()))
{
    var credential = await _credentialStore.ReadAsync(cancellationToken);
    if (credential is null)
        return new(AuthenticationOutcome.CredentialsRequired, "credentials_missing");
    await username.FillAsync(credential.Username);
    await password.FillAsync(credential.Password);
}
```

Use the exact action order from the spec. Redact exception messages before logging by logging only known reason codes and exception types.

When a failed result occurs, dispose the session unless `attempt.KeepBrowserOpenOnFailure` is true. For the final failure, call `FailedEdgeSessionKeeper.ReplaceAsync(session)` and clear the runner's local ownership so its `finally` block does not close the visible browser.

- [ ] **Step 4: Verify all paths**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter PlaywrightAuthenticationRunnerTests
dotnet test SchoolNetAutoAuth.sln
```

- [ ] **Step 5: Commit**

```powershell
git add src/SchoolNetAutoAuth.Core/Diagnostics src/SchoolNetAutoAuth.Infrastructure/Automation/PlaywrightAuthenticationRunner.cs src/SchoolNetAutoAuth.Infrastructure/Automation/FailedEdgeSessionKeeper.cs tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/PlaywrightAuthenticationRunnerTests.cs
git commit -m "feat: automate campus portal authentication"
```

---

### Task 10: Add Safe Diagnostics, Startup Registration, and Notifications

**Files:**
- Create: `src/SchoolNetAutoAuth.Infrastructure/Diagnostics/SafeFileLogger.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IAppDataCleaner.cs`
- Create: `src/SchoolNetAutoAuth.Core/Contracts/IStartupManager.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Maintenance/AppDataCleaner.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Startup/RegistryStartupManager.cs`
- Create: `src/SchoolNetAutoAuth.App/Tray/TrayIconController.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Diagnostics/SafeFileLoggerTests.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Maintenance/AppDataCleanerTests.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Startup/RegistryStartupManagerTests.cs`

**Interfaces:**
- Produces: whitelist-based `Log(AppEventId eventId, IReadOnlyDictionary<string,string>? fields)`.
- Produces: `Task IAppDataCleaner.ClearAsync(CancellationToken)` for deleting only app-owned data and its credential entry after UI confirmation.
- Produces: `IsEnabled`, `SetEnabledAsync(bool, CancellationToken)` for the current-user startup entry.
- Implements: `INotifier` using `NotifyIcon.ShowBalloonTip`.

- [ ] **Step 1: Write failing logger tests**

Verify one log line contains timestamp, event ID, and approved reason code, while keys named `username`, `password`, `cookie`, `authorization`, `formValue`, and `html` are omitted. Verify rolling keeps at most five files of 1 MiB each.

- [ ] **Step 2: Implement the safe logger**

Accept only the field keys `state`, `ssid`, `attempt`, `reasonCode`, `exceptionType`, and `durationMs`. Replace control characters and cap every value at 256 characters. Never accept arbitrary message templates from browser content.

- [ ] **Step 3: Write and implement startup tests**

Use a test registry value name and clean it in `finally`. The production value is:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
CampusNetworkAutoAuth = "<installed exe>" --background
```

Quote the executable path and reject paths that do not exist. `SetEnabledAsync(false)` removes only this value.

- [ ] **Step 4: Implement tray notifications and menu commands**

Create stable menu items for “立即认证”, “打开设置”, “重新录制”, “查看日志”, and “退出”. Map coordinator states to concise Chinese tooltip text. Normal `WaitingForTargetWifi` and `Online` transitions do not show balloons; `WaitingForCredentials`, `ActionRequired`, and final failure do.

- [ ] **Step 5: Test and implement app-data clearing**

Create a temporary root containing `settings.json`, `Logs`, and `EdgeProfile`. Prove the cleaner refuses deletion targets outside its configured root, closes any preserved failure session, deletes the Windows credential through `ICredentialStore`, and removes only those three app-owned locations.

`ClearAsync` resolves every full path and verifies it begins with the configured `%LOCALAPPDATA%\SchoolNetAutoAuth` root plus a directory separator. It never deletes the installation directory or a path supplied by the caller.

- [ ] **Step 6: Run tests and commit**

```powershell
dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter "SafeFileLoggerTests|RegistryStartupManagerTests|AppDataCleanerTests"
dotnet test SchoolNetAutoAuth.sln
git add src/SchoolNetAutoAuth.Core/Diagnostics src/SchoolNetAutoAuth.Core/Contracts/IStartupManager.cs src/SchoolNetAutoAuth.Core/Contracts/IAppDataCleaner.cs src/SchoolNetAutoAuth.Infrastructure/Diagnostics src/SchoolNetAutoAuth.Infrastructure/Startup src/SchoolNetAutoAuth.Infrastructure/Maintenance src/SchoolNetAutoAuth.App/Tray tests
git commit -m "feat: add startup notifications and safe diagnostics"
```

---

### Task 11: Build the WPF Settings and First-Run Wizard

**Files:**
- Create: `src/SchoolNetAutoAuth.App/ViewModels/ObservableObject.cs`
- Create: `src/SchoolNetAutoAuth.App/ViewModels/RelayCommand.cs`
- Create: `src/SchoolNetAutoAuth.App/ViewModels/SettingsViewModel.cs`
- Create: `src/SchoolNetAutoAuth.App/ViewModels/SetupWizardViewModel.cs`
- Create: `src/SchoolNetAutoAuth.App/Views/SettingsWindow.xaml`
- Create: `src/SchoolNetAutoAuth.App/Views/SettingsWindow.xaml.cs`
- Create: `src/SchoolNetAutoAuth.App/Views/SetupWizardWindow.xaml`
- Create: `src/SchoolNetAutoAuth.App/Views/SetupWizardWindow.xaml.cs`
- Test: `tests/SchoolNetAutoAuth.App.Tests/ViewModels/SettingsViewModelTests.cs`

**Interfaces:**
- Produces: save/delete credential, test locator, test authentication, record flow, toggle startup, and manual authentication commands.
- Consumes: settings store, credential store, action recorder, startup manager, coordinator.

- [ ] **Step 1: Move view models into a testable target and write failing command tests**

The view models must not reference `Window`, `MessageBox`, or static services. Test:

- invalid URI or retry values disable Save and expose Chinese validation messages;
- target SSID, portal URI, HTTPS probe URI, retry interval, maximum attempts, and timeouts round-trip through the view model;
- saving writes settings before changing the startup registration;
- blank password leaves the existing credential unchanged;
- “删除凭据” calls `DeleteAsync` only after a confirmation callback returns true;
- recording updates `RecordedFlow` but saves only after validation passes;
- “清除应用数据” requires explicit confirmation, calls `IAppDataCleaner.ClearAsync`, and returns the UI to the first-run wizard;
- `IsBusy` disables conflicting commands.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.App.Tests --filter SettingsViewModelTests`

- [ ] **Step 3: Implement the compact settings window**

Create four tabs: 常规、认证、重试、诊断. Use labels and native controls, with fixed minimum window size `760x520`, keyboard focus order, and no nested card panels. 常规 includes target SSID, HTTPS connectivity probe URI, network check interval, and startup toggle. 重试 includes retry interval, maximum attempts, probe timeout, and authentication timeout.

Authentication fields include portal URI, provider combo box populated from recorded providers, username, masked password, “保存凭据”, “删除凭据”, “重新录制”, “测试定位”, and “测试认证”. Never read the saved password back into the UI; display “已安全保存” instead.

The diagnostics tab includes “清除应用数据”. Keep it visually separated from ordinary commands, show an explicit confirmation describing settings, logs, Edge profile, and credential deletion, then close the settings window and open the first-run wizard after success.

- [ ] **Step 4: Implement the wizard**

Wizard steps are: network settings, credential choice, capture username, capture password, capture login, capture one or more providers, choose default provider, test authentication, finish. The Back button is disabled once a Playwright navigation is in progress; Cancel closes the browser session and leaves the previous valid configuration untouched.

- [ ] **Step 5: Add accessibility and layout checks**

Set `AutomationProperties.Name` on icon-only controls, ensure Chinese text wraps at 125% and 150% Windows scaling, and keep all action buttons at a stable minimum width. Verify the longest validation message does not overlap controls at `760x520`.

- [ ] **Step 6: Run tests and launch the UI manually**

```powershell
dotnet test SchoolNetAutoAuth.sln
dotnet run --project src/SchoolNetAutoAuth.App
```

Verify close hides to tray, the tray reopens settings, and Exit ends the process.

- [ ] **Step 7: Commit**

```powershell
git add src/SchoolNetAutoAuth.App tests/SchoolNetAutoAuth.App.Tests/ViewModels
git commit -m "feat: add settings and setup wizard"
```

---

### Task 12: Compose the Application and Monitor Network Changes

**Files:**
- Modify: `src/SchoolNetAutoAuth.App/App.xaml`
- Modify: `src/SchoolNetAutoAuth.App/App.xaml.cs`
- Create: `src/SchoolNetAutoAuth.App/AppCompositionRoot.cs`
- Create: `src/SchoolNetAutoAuth.Core/Authentication/BackgroundAuthenticationService.cs`
- Test: `tests/SchoolNetAutoAuth.Core.Tests/Authentication/BackgroundAuthenticationServiceTests.cs`

**Interfaces:**
- Produces: application startup, single-instance forwarding, background polling, network-event debouncing, and clean shutdown.
- Consumes: all interfaces from Tasks 2-10.

- [ ] **Step 1: Write failing background-service tests**

Use fakes and a manual clock to prove:

- startup performs one evaluation after a short network-settle delay;
- network change bursts within two seconds trigger one evaluation;
- periodic polling triggers evaluation at the configured interval;
- changing away from the target SSID cancels the active evaluation;
- StopAsync waits for current work and disposes subscriptions;
- manual authentication shares the same concurrency gate as automatic authentication.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Core.Tests --filter BackgroundAuthenticationServiceTests`

- [ ] **Step 3: Implement application composition**

`AppCompositionRoot` creates paths under `%LOCALAPPDATA%\SchoolNetAutoAuth`:

```text
settings.json
Logs\
EdgeProfile\
```

Create one instance of each service, wire coordinator state changes to the tray controller and diagnostics view model, and register disposal in reverse construction order.

- [ ] **Step 4: Implement single-instance behavior**

Use a named mutex `Local\SchoolNetAutoAuth.Singleton`. If another instance exists, send a named-pipe message `show-settings` and exit. The first instance listens on `SchoolNetAutoAuth.Commands` and activates its settings window on the WPF dispatcher.

- [ ] **Step 5: Implement background startup and shutdown**

`--background` starts hidden. A normal launch opens settings unless first-run configuration is incomplete, in which case it opens the setup wizard. App shutdown cancels the root token, waits for the background service, disposes Playwright sessions, then releases tray and mutex resources.

- [ ] **Step 6: Run all automated and manual smoke tests**

```powershell
dotnet test SchoolNetAutoAuth.sln
dotnet run --project src/SchoolNetAutoAuth.App -- --background
```

Launch a second instance and verify it opens the first instance's settings window without creating a second tray icon.

- [ ] **Step 7: Commit**

```powershell
git add src/SchoolNetAutoAuth.App src/SchoolNetAutoAuth.Core/Authentication/BackgroundAuthenticationService.cs tests/SchoolNetAutoAuth.Core.Tests/Authentication
git commit -m "feat: compose background authentication app"
```

---

### Task 13: Add a Deterministic Test Portal and End-to-End Tests

**Files:**
- Create: `tests/SchoolNetAutoAuth.TestPortal/SchoolNetAutoAuth.TestPortal.csproj`
- Create: `tests/SchoolNetAutoAuth.TestPortal/Program.cs`
- Create: `tests/SchoolNetAutoAuth.TestPortal/wwwroot/index.html`
- Create: `tests/SchoolNetAutoAuth.TestPortal/wwwroot/providers.html`
- Modify: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/PlaywrightAuthenticationRunnerTests.cs`
- Modify: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/PlaywrightActionRecorderTests.cs`

**Interfaces:**
- Produces: deterministic local routes `/`, `/providers`, `/success`, `/slow`, and `/changed`.
- Consumes: recorder and authentication runner from Tasks 8-9.

- [ ] **Step 1: Scaffold the local ASP.NET Core portal**

```powershell
dotnet new web -n SchoolNetAutoAuth.TestPortal -o tests/SchoolNetAutoAuth.TestPortal -f net8.0
dotnet sln add tests/SchoolNetAutoAuth.TestPortal/SchoolNetAutoAuth.TestPortal.csproj
dotnet add tests/SchoolNetAutoAuth.Infrastructure.Tests reference tests/SchoolNetAutoAuth.TestPortal
```

Expose a public `TestPortalHost.StartAsync(int port = 0, CancellationToken cancellationToken = default)` helper that returns the selected base URI and an async-disposable host handle. The login page uses labeled inputs and a submit button. Valid test credentials are `test-user` / `test-password`. The provider page contains 中国移动、中国联通、中国电信、校园网 buttons. `/success` returns a fixed online marker. `/changed` intentionally renames the login control for locator-failure testing.

- [ ] **Step 2: Write an end-to-end failing test**

Start the portal on an ephemeral localhost port, record a flow, save a test credential, run the authentication runner with 中国移动 selected, and assert the portal received actions in order:

```text
credentials_submitted
provider_selected:中国移动
```

Assert no screenshot, video, trace, password, or cookie file is written outside the test Edge profile.

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests --filter EndToEnd`

- [ ] **Step 4: Wire the deterministic test dependencies**

In the end-to-end fixture, use a temporary Edge profile, the test Credential Manager target, and a `PortalStateConnectivityProbe` that returns offline until the portal records `provider_selected:<name>`, then returns online. Construct `AppSettings` with the local portal URI, the recorded flow, and the selected provider. Pass `AuthenticationAttempt(1, 1, true)` and assert the runner still disposes its session after success.

Do not change production defaults or relax HTTPS validation in `HttpConnectivityProbe`; the state-aware fake exists only in the integration test project.

- [ ] **Step 5: Run the complete suite repeatedly**

```powershell
dotnet test SchoolNetAutoAuth.sln
dotnet test SchoolNetAutoAuth.sln
```

Expected: both runs pass with no browser process left after the test command exits.

- [ ] **Step 6: Commit**

```powershell
git add tests/SchoolNetAutoAuth.TestPortal tests/SchoolNetAutoAuth.Infrastructure.Tests src
git commit -m "test: cover the recorded authentication workflow"
```

---

### Task 14: Publish the Windows App and Build the Installer

**Files:**
- Create: `src/SchoolNetAutoAuth.App/Assets/App.ico`
- Modify: `src/SchoolNetAutoAuth.App/SchoolNetAutoAuth.App.csproj`
- Create: `scripts/publish.ps1`
- Create: `installer/SchoolNetAutoAuth.iss`
- Create: `README.md`

**Interfaces:**
- Produces: `artifacts/publish/win-x64/SchoolNetAutoAuth.App.exe`.
- Produces: `artifacts/installer/校园网自动认证-Setup.exe`.

- [ ] **Step 1: Configure self-contained publishing**

Set these App project properties:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
  <ApplicationIcon>Assets\App.ico</ApplicationIcon>
  <AssemblyName>SchoolNetAutoAuth.App</AssemblyName>
</PropertyGroup>
```

Keep a folder publish because Playwright's driver assets must remain beside the executable.

- [ ] **Step 2: Write the publish script**

`scripts/publish.ps1` must delete only the repository's `artifacts/publish/win-x64` directory after resolving and verifying that path is inside the repository, then run:

```powershell
dotnet restore SchoolNetAutoAuth.sln --locked-mode
dotnet test SchoolNetAutoAuth.sln -c Release --no-restore
dotnet publish src/SchoolNetAutoAuth.App -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

After publishing, verify the executable, Playwright driver files, and application icon exist.

- [ ] **Step 3: Install Inno Setup and create the installer script**

If `ISCC.exe` is unavailable, run in an approved network-enabled terminal:

```powershell
winget install --id JRSoftware.InnoSetup -e --source winget
```

The Inno script installs into `{localappdata}\Programs\SchoolNetAutoAuth`, creates a Start Menu shortcut, preserves `%LOCALAPPDATA%\SchoolNetAutoAuth` on upgrade, removes the Run value on uninstall, and writes the installer to `artifacts/installer` as `校园网自动认证-Setup.exe`.

Do not register a Windows service. Do not request administrator privileges; set `PrivilegesRequired=lowest`.

- [ ] **Step 4: Document installation and privacy behavior**

README sections must include: supported Windows versions, Edge requirement, first-run recording, credential priority, target SSID behavior, retry settings, log location, clearing app data, uninstall behavior, and the fact that an unsigned installer may trigger Microsoft Defender SmartScreen until a code-signing certificate is supplied.

- [ ] **Step 5: Build and inspect artifacts**

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/SchoolNetAutoAuth.iss
Get-FileHash artifacts/installer/校园网自动认证-Setup.exe -Algorithm SHA256
```

Expected: test command passes, publish succeeds, installer exists, and a SHA-256 hash is printed.

- [ ] **Step 6: Perform Windows acceptance testing**

On Windows 10 and Windows 11, verify:

1. Install and launch without a separate .NET or Node installation.
2. First-run wizard records the local test portal successfully.
3. Reboot or sign out/in and confirm exactly one tray instance starts.
4. Non-target Wi-Fi never opens Edge.
5. Target Wi-Fi plus successful probe never opens Edge.
6. Target Wi-Fi plus failed probe opens the dedicated Edge profile.
7. Browser-saved values take priority over Credential Manager values.
8. Missing credentials opens settings without clicking login.
9. Successful authentication closes Edge.
10. Final failure leaves Edge open and shows a notification.
11. Changing SSID during authentication cancels the run.
12. Uninstall removes program files and startup registration but asks before deleting user data.

Record the tested OS build, Edge version, installer SHA-256, and results in the release notes.

- [ ] **Step 7: Commit the release tooling**

```powershell
git add src/SchoolNetAutoAuth.App installer scripts README.md
git commit -m "build: package campus network auto auth"
```

---

## Final Verification Gate

Before reporting completion, run every command below from a clean checkout or clean worktree:

```powershell
dotnet restore SchoolNetAutoAuth.sln --locked-mode
dotnet build SchoolNetAutoAuth.sln -c Release --no-restore
dotnet test SchoolNetAutoAuth.sln -c Release --no-build
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/SchoolNetAutoAuth.iss
Get-FileHash artifacts/installer/校园网自动认证-Setup.exe -Algorithm SHA256
git status --short
```

Then manually inspect that no secret appears in tracked files or generated logs:

```powershell
rg -n -i "password\s*[:=]|authorization|cookie\s*[:=]|test-password" src installer README.md "$env:LOCALAPPDATA\SchoolNetAutoAuth\Logs"
```

The only permitted password occurrence is the fixed test credential inside the test portal and its tests. Any occurrence under `src`, installer files, README, or runtime logs must be removed before release.
