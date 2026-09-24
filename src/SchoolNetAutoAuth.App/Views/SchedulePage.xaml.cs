using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SchoolNetAutoAuth.App.ViewModels;
using Windows.Graphics;
using Windows.UI;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class SchedulePage : Page
{
    private const double MinimumDayWidth = 118;
    private const double TimeAxisWidth = 78;
    private static readonly SolidColorBrush HourLineBrush = new(Color.FromArgb(255, 215, 224, 232));
    private static readonly SolidColorBrush CourseBackgroundBrush = new(Color.FromArgb(255, 232, 242, 252));
    private static readonly SolidColorBrush CourseBorderBrush = new(Color.FromArgb(255, 169, 200, 231));
    private static readonly SolidColorBrush SecondaryTextBrush = new(Color.FromArgb(255, 102, 120, 138));
    private static readonly SolidColorBrush CurrentCourseBackgroundBrush = new(Color.FromArgb(255, 255, 244, 214));
    private static readonly SolidColorBrush CurrentCourseBorderBrush = new(Color.FromArgb(255, 224, 180, 76));
    private readonly ScheduleViewModel _viewModel;
    private bool _renderQueued;
    private bool _subscribed;

    public SchedulePage()
    {
        InitializeComponent();
        _viewModel = App.GetService<ScheduleViewModel>();
        DataContext = _viewModel;
        SemesterStartList.AddHandler(
            PointerWheelChangedEvent,
            new PointerEventHandler(SemesterStartList_PointerWheelChanged),
            handledEventsToo: true);
        Loaded += SchedulePage_Loaded;
        Unloaded += SchedulePage_Unloaded;
    }

    public SizeInt32 PreferredWindowSize
    {
        get
        {
            var maximumCourses = _viewModel.WeekDays.Count == 0
                ? 0
                : _viewModel.WeekDays.Max(day => day.Courses.Count);
            var height = Math.Clamp(820 + maximumCourses * 18, 860, 940);
            var width = _viewModel.ShowWeekends ? 1320 : 1120;
            return new(width, height);
        }
    }

    private void SchedulePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            _viewModel.WeekDays.CollectionChanged += ViewModel_WeekDaysChanged;
            _subscribed = true;
        }
        RenderTimeline();
    }

    private void SchedulePage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed) return;
        _viewModel.WeekDays.CollectionChanged -= ViewModel_WeekDaysChanged;
        _subscribed = false;
    }

    private void ViewModel_WeekDaysChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        QueueRenderTimeline();
    }

    private void ScheduleScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueRenderTimeline();
    }

    private void QueueRenderTimeline()
    {
        if (_renderQueued) return;
        _renderQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _renderQueued = false;
            RenderTimeline();
        });
    }

    private void RenderTimeline()
    {
        TimeAxisCanvas.Children.Clear();
        ScheduleCanvas.Children.Clear();
        WeekHeaderGrid.Children.Clear();
        WeekHeaderGrid.ColumnDefinitions.Clear();
        var height = Math.Max(1, _viewModel.TimelineHeight);
        var dayCount = Math.Max(1, _viewModel.WeekDays.Count);
        var availableWidth = Math.Max(0, ScheduleScrollViewer.ViewportWidth - TimeAxisWidth);
        var dayWidth = Math.Max(MinimumDayWidth, availableWidth / dayCount);
        TimeAxisCanvas.Height = height;
        ScheduleCanvas.Height = height;
        ScheduleCanvas.Width = dayCount * dayWidth;

        foreach (var period in _viewModel.Periods)
        {
            var axisCell = CreatePeriodAxisCell(period);
            Canvas.SetTop(axisCell, period.Top);
            TimeAxisCanvas.Children.Add(axisCell);
        }

        for (var dayIndex = 0; dayIndex < _viewModel.WeekDays.Count; dayIndex++)
        {
            var day = _viewModel.WeekDays[dayIndex];
            WeekHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(dayWidth)
            });
            var header = new Border
            {
                Width = dayWidth,
                Padding = new Thickness(6),
                BorderBrush = HourLineBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = day.DayName,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text = day.DateText,
                            FontSize = 11,
                            Foreground = SecondaryTextBrush
                        }
                    }
                }
            };
            Grid.SetColumn(header, dayIndex);
            WeekHeaderGrid.Children.Add(header);

            var dayCanvas = new Canvas
            {
                Width = dayWidth,
                Height = height
            };
            foreach (var period in day.Periods)
            {
                var line = new Border
                {
                    Width = dayWidth,
                    Height = 1,
                    Background = HourLineBrush
                };
                Canvas.SetTop(line, period.Top + period.Height - 1);
                dayCanvas.Children.Add(line);
            }

            var divider = new Border
            {
                Width = 1,
                Height = height,
                Background = HourLineBrush
            };
            Canvas.SetLeft(divider, dayWidth - 1);
            dayCanvas.Children.Add(divider);

            foreach (var course in day.Courses)
            {
                var card = CreateCourseCard(course, dayWidth);
                Canvas.SetLeft(card, 2);
                Canvas.SetTop(card, course.Top);
                dayCanvas.Children.Add(card);
            }

            Canvas.SetLeft(dayCanvas, dayIndex * dayWidth);
            ScheduleCanvas.Children.Add(dayCanvas);
        }
    }

    private void SemesterStartList_ItemClick(object sender, ItemClickEventArgs e)
    {
        SemesterStartButton.Flyout?.Hide();
    }

    private void SemesterStartList_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var scrollViewer = FindDescendant<ScrollViewer>(SemesterStartList);
        if (scrollViewer is null) return;

        var delta = e.GetCurrentPoint(SemesterStartList).Properties.MouseWheelDelta;
        if (delta == 0) return;

        var offset = Math.Clamp(
            scrollViewer.VerticalOffset - delta / 120d * 56,
            0,
            scrollViewer.ScrollableHeight);
        if (!scrollViewer.ChangeView(null, offset, null, disableAnimation: true))
            MoveSemesterStartSelection(delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void MoveSemesterStartSelection(int offset)
    {
        var itemCount = SemesterStartList.Items.Count;
        if (itemCount == 0) return;

        var currentIndex = SemesterStartList.SelectedIndex;
        var nextIndex = currentIndex < 0
            ? (offset > 0 ? 0 : itemCount - 1)
            : Math.Clamp(currentIndex + offset * 3, 0, itemCount - 1);
        SemesterStartList.SelectedIndex = nextIndex;
        SemesterStartList.ScrollIntoView(SemesterStartList.Items[nextIndex]);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;

            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private static Border CreatePeriodAxisCell(SchedulePeriodViewModel period)
    {
        var content = new Grid
        {
            Height = period.Height,
            Padding = new Thickness(4, 4, 4, 3)
        };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(new TextBlock
        {
            Text = period.PeriodText,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var time = new TextBlock
        {
            Text = period.TimeText,
            FontSize = 9,
            Foreground = SecondaryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(time, 1);
        content.Children.Add(time);

        return new Border
        {
            Width = TimeAxisWidth,
            Height = period.Height,
            BorderBrush = HourLineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = content
        };
    }

    private static Border CreateCourseCard(ScheduleOccurrenceViewModel course, double dayWidth)
    {
        var content = new StackPanel { Spacing = 1 };
        content.Children.Add(new TextBlock
        {
            Text = course.Name,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 11,
            LineHeight = 15,
            MaxLines = course.Height >= 74 ? 2 : 1
        });
        if (course.Height >= 34 && !string.IsNullOrWhiteSpace(course.DetailText))
        {
            content.Children.Add(new TextBlock
            {
                Text = course.DetailText,
                FontSize = 10,
                LineHeight = 14,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = SecondaryTextBrush,
                MaxLines = course.Height >= 74 ? 3 : 1
            });
        }

        var card = new Border
        {
            Width = Math.Max(44, dayWidth - 4),
            Height = course.Height,
            Padding = new Thickness(6, 2, 4, 2),
            Background = course.IsCurrent ? CurrentCourseBackgroundBrush : CourseBackgroundBrush,
            BorderBrush = course.IsCurrent ? CurrentCourseBorderBrush : CourseBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = content
        };
        ToolTipService.SetToolTip(
            card,
            $"{course.Name}{Environment.NewLine}{course.TimeText}{Environment.NewLine}{course.DetailText}".Trim());
        return card;
    }
}
