using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MuseumAdmin.Services
{
    public enum NotificationType
    {
        Success,
        Info,
        Warning,
        Error
    }

    public class ToastNotification
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; } = DateTime.Now;
    }

    public class NotificationService
    {
        public event Action? OnNotificationsChanged;
        private readonly object _syncLock = new();
        private readonly List<ToastNotification> _notifications = new();
        private readonly Dictionary<Guid, CancellationTokenSource> _dismissalTokens = new();
        public IReadOnlyList<ToastNotification> Notifications
        {
            get
            {
                lock (_syncLock)
                {
                    return _notifications.ToArray();
                }
            }
        }

        public void Notify(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
        {
            var notification = new ToastNotification
            {
                Title = title,
                Message = message,
                Type = type
            };

            CancellationTokenSource? dismissalToken = null;

            lock (_syncLock)
            {
                _notifications.Add(notification);

                if (durationMs > 0)
                {
                    dismissalToken = new CancellationTokenSource();
                    _dismissalTokens[notification.Id] = dismissalToken;
                }
            }

            NotifyStateChanged();

            if (dismissalToken is not null)
            {
                _ = DismissAfterDelayAsync(notification.Id, durationMs, dismissalToken.Token);
            }
        }

        public void Success(string title, string message) => Notify(title, message, NotificationType.Success);
        public void Error(string title, string message) => Notify(title, message, NotificationType.Error, 8000);
        public void Warning(string title, string message) => Notify(title, message, NotificationType.Warning);
        public void Info(string title, string message) => Notify(title, message, NotificationType.Info);

        public void Remove(Guid id)
        {
            CancellationTokenSource? dismissalToken = null;
            var removed = false;

            lock (_syncLock)
            {
                var notification = _notifications.Find(n => n.Id == id);
                if (notification is null)
                {
                    return;
                }

                _notifications.Remove(notification);
                removed = true;

                if (_dismissalTokens.Remove(id, out var cts))
                {
                    dismissalToken = cts;
                }
            }

            dismissalToken?.Dispose();

            if (removed)
            {
                NotifyStateChanged();
            }
        }

        private async Task DismissAfterDelayAsync(Guid notificationId, int durationMs, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(durationMs, cancellationToken);
                Remove(notificationId);
            }
            catch (TaskCanceledException)
            {
                // Notification was closed manually or already removed.
            }
        }

        private void NotifyStateChanged() => OnNotificationsChanged?.Invoke();
    }
}
