namespace SchoolNetAutoAuth.App.Notifications;

public readonly record struct DesktopRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public readonly record struct DesktopSize(double Width, double Height);
public readonly record struct DesktopPoint(double X, double Y);

public static class NotificationPlacement
{
    public static DesktopPoint Calculate(
        DesktopRect workArea,
        DesktopSize windowSize,
        double margin) =>
        new(
            workArea.Right - windowSize.Width - margin,
            workArea.Bottom - windowSize.Height - margin);
}
