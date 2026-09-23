using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SchoolNetAutoAuth.App.ViewModels;
using Windows.Graphics;
using Windows.UI;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class SchedulePage : Page
{
    private const double DayWidth = 96;
    private static readonly SolidColorBrush HourLineBrush = new(Color.FromArgb(255, 215, 224, 232));
    private static readonly SolidColorBrush HalfHourLineBrush = new(Color.FromArgb(255, 235, 240, 244));
    private static readonly SolidColorBrush CourseBackgroundBrush = new(Color.FromArgb(255, 232, 242, 252));
    private static readonly SolidColorBrush CourseBorderBrush = new(Color.FromArgb(255, 169, 200, 231));
    private static readonly SolidColorBrush CourseTimeBrush = new(Color.FromArgb(255, 49, 94, 134));
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
            var height = Math.Clamp(630 + maximumCourses * 24, 680, 780);
            var width = _viewModel.ShowWeekends ? 1160 : 980;
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
        var height = Math.Max(1, _viewModel.TimelineHeight);
        TimeAxisCanvas.Height = height;
        ScheduleCanvas.Height = height;
        ScheduleCanvas.Width = Math.Max(1, _viewModel.WeekDays.Count * DayWidth);

        foreach (var slot in _viewModel.TimeSlots)
        {
            var label = new TextBlock
            {
                Text = slot.TimeText,
                FontSize = 10,
                Foreground = SecondaryTextBrush
            };
            Canvas.SetTop(label, slot.LabelTop);
            TimeAxisCanvas.Children.Add(label);
        }

        for (var dayIndex = 0; dayIndex < _viewModel.WeekDays.Count; dayIndex++)
        {
            var day = _viewModel.WeekDays[dayIndex];
            var dayCanvas = new Canvas
            {
                Width = DayWidth,
                Height = height
            };
            foreach (var slot in day.TimeSlots)
            {
                var line = new Border
                {
                    Width = DayWidth,
                    Height = 1,
                    Background = slot.IsHour ? HourLineBrush : HalfHourLineBrush
                };
                Canvas.SetTop(line, slot.LineTop);
                dayCanvas.Children.Add(line);
            }

            var divider = new Border
            {
                Width = 1,
                Height = height,
                Background = HourLineBrush
            };
            Canvas.SetLeft(divider, DayWidth - 1);
            dayCanvas.Children.Add(divider);

            foreach (var course in day.Courses)
            {
                var card = CreateCourseCard(course);
                Canvas.SetLeft(card, 8);
                Canvas.SetTop(card, course.Top);
                dayCanvas.Children.Add(card);
            }

            Canvas.SetLeft(dayCanvas, dayIndex * DayWidth);
            ScheduleCanvas.Children.Add(dayCanvas);
        }
    }

    private static Border CreateCourseCard(ScheduleOccurrenceViewModel course)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock
        {
            Text = course.TimeText,
            FontSize = 11,
            Foreground = CourseTimeBrush
        });
        content.Children.Add(new TextBlock
        {
            Text = course.Name,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            MaxLines = 4
        });
        if (!string.IsNullOrWhiteSpace(course.Location))
        {
            content.Children.Add(new TextBlock
            {
                Text = course.Location,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = SecondaryTextBrush
            });
        }

        var card = new Border
        {
            Width = 80,
            Height = course.Height,
            Padding = new Thickness(6),
            Background = course.IsCurrent ? CurrentCourseBackgroundBrush : CourseBackgroundBrush,
            BorderBrush = course.IsCurrent ? CurrentCourseBorderBrush : CourseBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = content
        };
        ToolTipService.SetToolTip(card, $"{course.TimeText} {course.Name} {course.Location}".Trim());
        return card;
    }
}
