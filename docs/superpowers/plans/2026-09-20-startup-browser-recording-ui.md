# 启动、浏览器、录制配置与按钮改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 WinUI 3 应用更早启动、后台无头认证并可人工恢复，同时为录制页增加安全的配置导入导出，并统一按钮图标视觉。

**Architecture:** 保留 Core、Infrastructure 和现有 MVVM 分层。把启动注册、Edge 会话模式、录制配置文件格式和文件选择器分别封装为独立边界，再由 `AppController`、`RecordingViewModel` 和 XAML 接入。

**Tech Stack:** .NET 8 Windows、WinUI 3、Windows App SDK、Playwright、CommunityToolkit.Mvvm、Windows Task Scheduler、Lucide SVG icons via better-icons。

**Spec:** `docs/superpowers/specs/2026-09-20-startup-browser-recording-ui-design.md`

## Global Constraints

- 不保证绝对早于所有第三方软件，只优化当前用户登录触发路径。
- 录制配置不包含账号、密码、SSID、认证地址、重试参数。
- 录制使用可见 Edge，后台认证使用 Headless Edge，需要人工处理时才打开可见 Edge。
- 保留现有 WinUI 3、MVVM、Playwright 和托盘实现。
- 图标作为本地资源随应用发布，不依赖运行时网络。

---

### Task 1: 启动任务管理与安装/卸载接入

**Files:**
- Create: `src/SchoolNetAutoAuth.Infrastructure/Startup/StartupTaskManager.cs`
- Modify: `src/SchoolNetAutoAuth.Infrastructure/Startup/RegistryStartupManager.cs`
- Modify: `src/SchoolNetAutoAuth.App/App.xaml.cs`
- Modify: `src/SchoolNetAutoAuth.App/Services/AppController.cs`
- Modify: `src/SchoolNetAutoAuth.Installer/Program.cs`
- Modify: `src/SchoolNetAutoAuth.App/Uninstallation/UninstallService.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Startup/StartupTaskManagerTests.cs`

**Interfaces:**
- Produces `StartupTaskManager.SetEnabled(bool enabled, string executablePath)`, `Remove()`, and `BuildRegistrationCommand(string executablePath)`.
- Existing callers stop writing startup registry values directly.

- [ ] **Step 1: Write failing tests** for task name, `--background` command, current-user logon trigger, cleanup command, and fallback decision.
- [ ] **Step 2: Run focused tests** with `dotnet test tests/SchoolNetAutoAuth.Infrastructure.Tests/SchoolNetAutoAuth.Infrastructure.Tests.csproj --filter StartupTaskManager`.
- [ ] **Step 3: Implement** Task Scheduler registration through `schtasks.exe`/PowerShell-compatible current-user commands, with a small injectable command runner; on failure call existing registry fallback.
- [ ] **Step 4: Route** `AppController.SaveSettingsAsync`, installer registration, and uninstall cleanup through the manager; ensure successful task registration removes the old `Run` value.
- [ ] **Step 5: Run focused infrastructure and app tests**, then commit `feat: register startup with logon task`.

### Task 2: Edge 会话模式与人工恢复

**Files:**
- Create: `src/SchoolNetAutoAuth.Infrastructure/Automation/EdgeSessionMode.cs`
- Modify: `src/SchoolNetAutoAuth.Infrastructure/Automation/EdgeSessionFactory.cs`
- Modify: `src/SchoolNetAutoAuth.Infrastructure/Automation/PlaywrightActionRecorder.cs`
- Modify: `src/SchoolNetAutoAuth.Infrastructure/Automation/PlaywrightAuthenticationRunner.cs`
- Modify: `src/SchoolNetAutoAuth.Infrastructure/Automation/FailedEdgeSessionKeeper.cs`
- Modify: `src/SchoolNetAutoAuth.App/Services/AppController.cs`
- Modify: `src/SchoolNetAutoAuth.App/App.xaml.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/EdgeSessionFactoryTests.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Automation/AuthenticationRecoveryTests.cs`

**Interfaces:**
- `EdgeSessionFactory.LaunchAsync(EdgeSessionMode mode, CancellationToken token)`.
- `EdgeSessionMode` values: `Recording`, `BackgroundAuthentication`, `InteractiveRecovery`.
- Recovery result carries the target URI and user-action reason without credentials.

- [ ] **Step 1: Add tests** asserting recorder launches visible mode, authentication launches headless mode, and only recoverable failures request interactive mode.
- [ ] **Step 2: Run tests** and confirm failure against the current boolean API.
- [ ] **Step 3: Replace** the boolean with the enum and map modes to Playwright `Headless`; keep the same persistent profile path.
- [ ] **Step 4: Implement** recovery orchestration: close failed headless session, launch visible session, navigate to saved page, publish notice, retain session until connectivity returns or app exits.
- [ ] **Step 5: Preserve** retry behavior for ordinary timeouts and make recovery startup failures non-fatal.
- [ ] **Step 6: Run automation tests and commit** `feat: add headless authentication recovery`.

### Task 3: 录制配置导入导出

**Files:**
- Create: `src/SchoolNetAutoAuth.Core/Configuration/RecordingConfigurationFile.cs`
- Create: `src/SchoolNetAutoAuth.Infrastructure/Configuration/RecordingConfigurationSerializer.cs`
- Create: `src/SchoolNetAutoAuth.App/Services/FilePickerService.cs`
- Modify: `src/SchoolNetAutoAuth.App/ViewModels/RecordingViewModel.cs`
- Modify: `src/SchoolNetAutoAuth.App/Services/AppController.cs`
- Modify: `src/SchoolNetAutoAuth.App/Views/RecordingPage.xaml`
- Test: `tests/SchoolNetAutoAuth.Core.Tests/Configuration/RecordingConfigurationFileTests.cs`
- Test: `tests/SchoolNetAutoAuth.Infrastructure.Tests/Configuration/RecordingConfigurationSerializerTests.cs`
- Test: `tests/SchoolNetAutoAuth.App.Tests/RecordingViewModelTests.cs`

**Interfaces:**
- `RecordingConfigurationFile(string Format, int Version, DateTimeOffset ExportedAtUtc, RecordedClickSequence RecordedSequence)`.
- Serializer methods: `Serialize(RecordedClickSequence)`, `Deserialize(string)`, `Validate(RecordingConfigurationFile)`.
- Picker methods: `PickOpenFileAsync()`, `PickSaveFileAsync()`, both returning `StorageFile?`.

- [ ] **Step 1: Add failing round-trip, version rejection, malformed JSON, and sensitive-field exclusion tests.**
- [ ] **Step 2: Implement versioned JSON serialization and strict validation without touching `AppSettings`.**
- [ ] **Step 3: Add WinUI picker service with HWND initialization and cancellation-safe null results.**
- [ ] **Step 4: Add `ImportCommand` and `ExportCommand` to `RecordingViewModel`; ask overwrite confirmation before saving imported data.**
- [ ] **Step 5: Add icon+text buttons to the recording toolbar and refresh steps only after successful persistence.**
- [ ] **Step 6: Run focused tests and commit** `feat: import and export recording configurations`.

### Task 4: 本地 Lucide 图标与按钮样式

**Files:**
- Create: `src/SchoolNetAutoAuth.App/Assets/Icons/*.svg` for play, square, trash-2, eraser, rotate-ccw, file-input, file-output, info
- Modify: `src/SchoolNetAutoAuth.App/App.xaml`
- Modify: `src/SchoolNetAutoAuth.App/Views/MainWindow.xaml`
- Modify: `src/SchoolNetAutoAuth.App/Views/RecordingPage.xaml`
- Test: `tests/SchoolNetAutoAuth.App.Tests/IconResourceTests.cs`

**Interfaces:**
- Resource keys: `IconPlay`, `IconSquare`, `IconTrash`, `IconEraser`, `IconRefresh`, `IconImport`, `IconExport`, `IconInfo`.
- Styles: `AppPrimaryButtonStyle`, `AppButtonStyle`, `AppDangerButtonStyle`, `AppIconButtonStyle`.

- [ ] **Step 1: Retrieve Lucide SVGs using `better-icons get` and add them as build content.**
- [ ] **Step 2: Add shared icon resources and four button styles with fixed 32px icon-button dimensions and 36px text-button height.**
- [ ] **Step 3: Replace mixed `FontIcon`/`SymbolIcon` instances in the main window and recording page.**
- [ ] **Step 4: Make the About control a compact 32x32 icon-only button with tooltip and keyboard focus visuals.**
- [ ] **Step 5: Make the recording toolbar wrap safely at narrow widths.**
- [ ] **Step 6: Run XAML/resource tests and commit** `feat: unify WinUI button icon styling`.

### Task 5: 集成验证与发布回归

**Files:**
- Modify: `scripts/publish.ps1` only if new assets or startup registration require packaging changes
- Modify: `README.md` to document startup task fallback and recording config format

- [ ] **Step 1: Run full tests** with `dotnet test SchoolNetAutoAuth.sln --configuration Release`.
- [ ] **Step 2: Build** with `dotnet build SchoolNetAutoAuth.sln --configuration Release --no-restore`.
- [ ] **Step 3: Run** `powershell -ExecutionPolicy Bypass -File scripts/publish.ps1`.
- [ ] **Step 4: Manually verify** user-login startup, hidden background authentication, interactive recovery notification, recording import/export, and narrow-window button layout.
- [ ] **Step 5: Commit documentation/package changes** as `docs: document startup and recording configuration`.
