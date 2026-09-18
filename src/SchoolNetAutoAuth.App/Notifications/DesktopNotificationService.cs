using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.App.Notifications;

public sealed class DesktopNotificationService : IDisposable
{
    private NotificationWindow? _current;

    public void Show(AuthenticationNotice notice)
    {
        var (title, message, duration) = notice.Kind switch
        {
            AuthenticationNoticeKind.Connected =>
                ("校园网连接成功", notice.Message, TimeSpan.FromSeconds(8)),
            _ when notice.ExternalAction == ExternalActionKind.PhoneVerification =>
                ("需要电话验证", BuildBlockedMessage(notice.Message), TimeSpan.FromSeconds(30)),
            _ =>
                ("在线设备达到上限", BuildBlockedMessage(notice.Message), TimeSpan.FromSeconds(30))
        };
        _current?.Close();
        _current = new(title, message, duration);
        _current.Closed += (_, _) => _current = null;
        _current.ShowInactive();
    }

    private static string BuildBlockedMessage(string safeExcerpt) =>
        $"{safeExcerpt}\n\n将在 5 分钟后自动重试。也可以点击主窗口的“立即认证”马上重试。";

    public void Dispose()
    {
        _current?.Close();
        _current = null;
    }
}
