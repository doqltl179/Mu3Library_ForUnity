using System;
using Unity.Notifications;

namespace Mu3Library.Notifications
{
    public interface INotificationManagerEventBus
    {
        /// <summary>
        /// Raised when a notification arrives while the application runs.
        /// On Android it also fires when the app returns from the background; on iOS only
        /// while the app is in the foreground, which is what the underlying package delivers.
        /// </summary>
        event Action<Notification> OnNotificationReceived;
    }
}
