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
            _ =>
                ("认证需要手动处理", BuildBlockedMessage(notice.Message), TimeSpan.FromSeconds(30))
        };
        _current?.Close();
        _current = new(title, message, duration);
        _current.Closed += (_, _) => _current = null;
        _current.ShowInactive();
    }

    private static string BuildBlockedMessage(string safeExcerpt) =>
        $"{safeExcerpt}\n\n已关闭后台浏览器并打开认证地址，请手动完成修复。";

    public void Dispose()
    {
        _current?.Close();
        _current = null;
    }
}
