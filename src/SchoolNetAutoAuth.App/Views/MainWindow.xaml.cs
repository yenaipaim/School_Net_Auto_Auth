using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.ViewModels;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;
using Windows.Graphics;
using Windows.UI;

namespace SchoolNetAutoAuth.App.Views;

/// <summary>
/// 应用外壳：40px 自绘标题栏 + 可折叠侧栏（220 / 60）+ 透明内容区。
/// 右上角 138 DIP 留给系统原生标题按钮：本投影的 AppWindowTitleBar 没有 IsCaptionVisible，
/// 隐藏不了，所以只把它配色成主题色，不自己再画一套按钮，避免点击区被非客户区吞掉。
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double SidebarExpandedWidth = 220;
    private const double SidebarCollapsedWidth = 60;

    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private static readonly Dictionary<string, Type> PageTypes = new(StringComparer.Ordinal)
    {
        ["status"] = typeof(StatusPage),
        ["recording"] = typeof(RecordingPage),
        ["network"] = typeof(NetworkSettingsPage),
        ["advanced"] = typeof(AdvancedSettingsPage),
        ["general"] = typeof(GeneralSettingsPage),
        ["schedule"] = typeof(SchedulePage)
    };
    private readonly List<TextBlock> _sectionLabels = [];
    private readonly List<Grid> _navRows = [];
    private readonly List<RadioButton> _navItems = [];
    private readonly Dictionary<RadioButton, (FontIcon Icon, TextBlock Label)> _navVisuals = [];
    private SizeInt32? _sizeBeforeSchedule;
    private SchedulePage? _schedulePage;
    private bool _scheduleMode;
    private bool _sidebarCollapsed;

    public MainWindow(
        MainViewModel viewModel,
        WindowService windows,
        AppController controller,
        DialogService dialogs)
    {
        InitializeComponent();
        _controller = controller;
        _dialogs = dialogs;
        Root.DataContext = viewModel;
        windows.Attach(this);
        AppWindow.Title = "校园网自动认证";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "App.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
        AppWindow.Resize(new SizeInt32(1180, 760));
        VersionText.Text = "v" + (typeof(App).Assembly.GetName().Version ?? new Version(1, 0)).ToString(3);

        // 原生标题栏保留系统绘制：自绘 caption 在本环境会得到「透明 caption + 内容未上移」
        // 的双标题栏（壁纸从顶部透出），因此只接管 caption 的配色，不动布局。
        ConfigureCaptionButtons();

        // 侧栏元素按 Tag 归类，折叠时批量切换，避免依赖视觉树遍历。
        CollectNavElements(NavPanel);
        ApplyNavVisuals();


        ApplyAppearance(_controller.Settings);
        ThemeService.ThemeChanged += () => DispatcherQueue.TryEnqueue(ApplyThemeToVisualTree);
        Root.ActualThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyNavVisuals);
        _controller.SettingsChanged += (_, settings) => Root.DispatcherQueue.TryEnqueue(() =>
        {
            ApplyAppearance(settings);
            ApplyBackgroundImage(settings.BackgroundImagePath, settings.BackgroundImageOpacity);
        });
        _controller.SnapshotChanged += (_, snapshot) => Root.DispatcherQueue.TryEnqueue(() => ApplySnapshot(snapshot));

        ApplyBackgroundImage(_controller.Settings.BackgroundImagePath, _controller.Settings.BackgroundImageOpacity);
        // WinUI 3 无法在 XAML 里给 ToggleButton 系（RadioButton）赋 IsChecked，
        // 只能在这里设，否则 InitializeComponent 抛 XamlParseException。
        StatusNav.IsChecked = true;
        ContentFrame.Navigate(PageTypes["status"]);
        ApplySnapshot(_controller.Snapshot);

        // 首启提示与状态页横幅都通过 WindowService 请求导航，避免 ViewModel 直接摸视觉树。
        windows.NavigationRequested += tag => DispatcherQueue.TryEnqueue(() => NavigateFromRequest(tag));
    }

    private void NavigateFromRequest(string tag)
    {
        if (!PageTypes.ContainsKey(tag)) return;
        var item = _navItems.FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.Ordinal));
        if (item is not null && item.IsChecked != true) item.IsChecked = true;
        NavigateTo(tag);
    }

    // ===== 外观 =====

    private void CollectNavElements(DependencyObject root)
    {
        switch (root)
        {
            case TextBlock { Tag: "section" } label:
                _sectionLabels.Add(label);
                break;
            case Grid { Tag: "nav" } row:
                _navRows.Add(row);
                break;
            case RadioButton { GroupName: "AppNav" } item:
                _navItems.Add(item);
                if (item.Content is Grid itemRow)
                {
                    var icon = itemRow.Children.OfType<FontIcon>().FirstOrDefault();
                    var label = itemRow.Children.OfType<TextBlock>().FirstOrDefault(text => (string?)text.Tag == "label");
                    if (icon is not null && label is not null)
                    {
                        _navVisuals[item] = (icon, label);
                        item.PointerEntered += (_, _) => ApplyNavVisuals();
                        item.PointerExited += (_, _) => ApplyNavVisuals();
                        item.Checked += (_, _) => ApplyNavVisuals();
                        item.Unchecked += (_, _) => ApplyNavVisuals();
                    }
                }
                break;
        }

        if (root is Panel panel)
            foreach (var child in panel.Children) CollectNavElements(child);
        if (root is ContentControl { Content: DependencyObject content }) CollectNavElements(content);
    }

    private void ApplyAppearance(AppSettings settings)
    {
        ThemeService.Apply(settings.Theme, settings.AccentColor, settings.InterfaceFontFamily);
        ApplyThemeToVisualTree();
    }

    private void ApplyThemeToVisualTree()
    {
        Root.RequestedTheme = ThemeService.IsLight ? ElementTheme.Light : ElementTheme.Dark;
        // 图标显示「点击后进入的」主题：深色时给太阳，浅色时给月亮。
        ThemeGlyph.Glyph = ThemeService.IsLight ? "\uE708" : "\uE706";
        ToolTipService.SetToolTip(ThemeButton, ThemeService.IsLight ? "切换到深色主题" : "切换到浅色主题");

        ConfigureCaptionButtons();
    }



    /// <summary>让系统标题按钮与标题栏底色跟随主题，视觉上与正文连成一体。</summary>
    private void ConfigureCaptionButtons()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        var bar = AppWindow.TitleBar;
        var card = Resolve("AppCardBrush");
        bar.BackgroundColor = card;
        bar.InactiveBackgroundColor = card;
        bar.ButtonBackgroundColor = card;
        bar.ButtonInactiveBackgroundColor = card;
        bar.ButtonForegroundColor = Resolve("AppMutedFgBrush");
        bar.ButtonInactiveForegroundColor = Resolve("AppPlaceholderBrush");
        bar.ButtonHoverBackgroundColor = Resolve("AppSurfaceHoverBrush");
        bar.ButtonHoverForegroundColor = Resolve("AppFgBrush");
        bar.ButtonPressedBackgroundColor = Resolve("AppPanelHoverBrush");
        bar.ButtonPressedForegroundColor = Resolve("AppMutedFgBrush");
        bar.ForegroundColor = Resolve("AppFgBrush");
        bar.InactiveForegroundColor = Resolve("AppMutedFgBrush");
    }

    private static Color Resolve(string key) =>
        Application.Current.Resources.TryGetValue(key, out object? value) && value is SolidColorBrush brush
            ? brush.Color
            : Colors.Transparent;

    // ===== 背景图 =====

    private void ApplyBackgroundImage(string? path, double opacity)
    {
        BackgroundImage.Source = null;
        var effectiveOpacity = Math.Clamp(opacity, 0, 1);
        BackgroundImage.Opacity = effectiveOpacity;
        var hasImage = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        // 有背景图时：卡片/面板留出透光率，同时给图片压一层底色保证正文可读。
        ThemeService.SetSurfaceTranslucency(hasImage);
        BackgroundScrim.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        BackgroundScrim.Opacity = hasImage ? ScrimOpacityFor(effectiveOpacity) : 0;
        if (!hasImage) return;
        try
        {
            BackgroundImage.Source = new BitmapImage(new Uri(path!, UriKind.Absolute));
        }
        catch
        {
            BackgroundImage.Source = null;
            ThemeService.SetSurfaceTranslucency(false);
            BackgroundScrim.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>背景图越"实"，压色越轻，把观感决定权交回给用户的透明度滑块。</summary>
    private static double ScrimOpacityFor(double imageOpacity)
    {
        // 浅色主题下白底深字最怕亮背景图，需要压得更狠才读得清。
        var (basis, slope, floor) = ThemeService.IsLight ? (0.97, 0.27, 0.68) : (0.87, 0.33, 0.40);
        return Math.Clamp(basis - slope * imageOpacity, floor, basis);
    }

    // ===== 状态指示 =====

    private void ApplySnapshot(RuntimeSnapshot snapshot)
    {
        var text = StatusViewModel.FormatAuthenticationState(snapshot.AuthenticationState);
        TitleStatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(snapshot.AuthenticationState switch
        {
            AuthenticationState.Online => Resolve("AppSuccessBrush"),
            AuthenticationState.Authenticating or AuthenticationState.CheckingConnectivity => Resolve("AppInfoBrush"),
            AuthenticationState.RetryDelay or AuthenticationState.WaitingForCredentials => Resolve("AppWarningBrush"),
            AuthenticationState.ExternalActionCooldown => Resolve("AppDestructiveBrush"),
            _ => Resolve("AppMutedFgBrush")
        });
    }

    // ===== 标题栏交互 =====

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        _ = AuthenticateAsync();
    }

    private async Task AuthenticateAsync()
    {
        try { await _controller.AuthenticateNowAsync(); }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("认证失败", ex.Message); }
        finally { RefreshButton.IsEnabled = true; }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var next = ThemeService.IsLight ? AppSettings.DarkTheme : AppSettings.LightTheme;
        _ = SaveAsync(_controller.Settings with { Theme = next });
    }





    private async Task SaveAsync(AppSettings settings)
    {
        try { await _controller.SaveSettingsAsync(settings); }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存失败", ex.Message);
        }
    }

    // ===== 侧栏 =====

    private void Collapse_Click(object sender, RoutedEventArgs e) => SetSidebarCollapsed(!_sidebarCollapsed);

    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;
        SidebarColumn.Width = new GridLength(collapsed ? SidebarCollapsedWidth : SidebarExpandedWidth);
        CollapseGlyph.Glyph = collapsed ? "\uE76C" : "\uE76B";
        ToolTipService.SetToolTip(CollapseButton, collapsed ? "展开侧边栏" : "收起侧边栏");
        SidebarTitle.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ThemeButton.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;

        foreach (var label in _sectionLabels)
            label.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        foreach (var row in _navRows)
        {
            // 折叠时让行按内容自适应，并清掉列间距：
            // 标签隐藏后 * 列宽度为 0，但 ColumnSpacing=10 仍会占位，把图标顶向左侧。
            row.Width = collapsed ? double.NaN : 196;
            row.ColumnSpacing = collapsed ? 0 : 10;
            // Grid 有 Auto + * 两列，即使标签隐藏，* 列仍会吃掉剩余宽度，
            // 只设置按钮的 ContentAlignment 只会让"已经撑满的 Grid"居中，图标依旧贴左。
            row.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            foreach (var child in row.Children)
                if (child is TextBlock { Tag: "label" } text)
                    text.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        }
        foreach (var item in _navItems)
        {
            // 按钮始终铺满侧栏，悬停底色才不会只覆盖图标那一小块。
            item.HorizontalAlignment = HorizontalAlignment.Stretch;
            item.Padding = new Thickness(collapsed ? 0 : 14, 0, 0, 0);
            item.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        }
        ApplyNavVisuals();
    }


    /// <summary>
    /// 导航项配色：未选中 muted、悬停 foreground、选中 foreground + 品牌色图标。
    /// FontIcon.Foreground 不是继承属性，模板拿不到内容树的图标，所以只能在代码里刷。
    /// </summary>
    private void ApplyNavVisuals()
    {
        foreach (var (item, visual) in _navVisuals)
        {
            var selected = item.IsChecked == true;
            var hovered = item.IsPointerOver;
            item.Background = new SolidColorBrush(selected
                ? Resolve("AppAccentBrush")
                : hovered ? Resolve("AppPanelHoverBrush")
                : Colors.Transparent);
            var text = selected || hovered ? "AppFgBrush" : "AppMutedFgBrush";
            visual.Label.Foreground = new SolidColorBrush(Resolve(text));
            visual.Icon.Foreground = new SolidColorBrush(selected ? Resolve("AppBrandBrush") : Resolve(text));
        }
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        if (!PageTypes.TryGetValue(tag, out var pageType)) return;
        var enteringSchedule = string.Equals(tag, "schedule", StringComparison.Ordinal);
        if (enteringSchedule && !_scheduleMode) EnterScheduleMode();
        else if (!enteringSchedule && _scheduleMode) LeaveScheduleMode();

        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
        if (enteringSchedule) AttachSchedulePage();
    }

    private void EnterScheduleMode()
    {
        _scheduleMode = true;
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            _sizeBeforeSchedule = AppWindow.Size;
    }

    private void LeaveScheduleMode()
    {
        DetachSchedulePage();
        _scheduleMode = false;
        if (_sizeBeforeSchedule is { } size
            && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            ResizeToWorkArea(size);
        }
        _sizeBeforeSchedule = null;
    }

    private void AttachSchedulePage()
    {
        DetachSchedulePage();
        _schedulePage = ContentFrame.Content as SchedulePage;
        if (_schedulePage is null) return;
        ResizeToWorkArea(_schedulePage.PreferredWindowSize);
    }

    private void DetachSchedulePage() => _schedulePage = null;

    private void ResizeToWorkArea(SizeInt32 requestedSize)
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is null) return;
        var workArea = displayArea.WorkArea;
        var minimumWidth = Math.Min(720, workArea.Width);
        var minimumHeight = Math.Min(560, workArea.Height);
        AppWindow.Resize(new SizeInt32(
            Math.Clamp(requestedSize.Width, minimumWidth, workArea.Width),
            Math.Clamp(requestedSize.Height, minimumHeight, workArea.Height)));
    }
}
