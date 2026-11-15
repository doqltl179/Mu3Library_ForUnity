using System;
using Unity.Notifications;

namespace Mu3Library.Notifications
{
    /// <summary>
    /// Local notifications on Android and iOS, wrapped over the Mobile Notifications package's
    /// unified <see cref="NotificationCenter"/>. The manager must be initialized before anything
    /// else; every call before that reports and answers a safe default. In the editor the
    /// underlying package delivers nothing, so calls are harmless no-ops there.
    /// </summary>
    public partial interface INotificationManager
    {
        public bool IsInitialized { get; }

        /// <summary>
        /// Initializes the notification center with the default presentation options and an
        /// automatically created Android channel. iOS ignores the channel values.
        /// </summary>
        public void Initialize(string androidChannelId, string androidChannelName, string androidChannelDescription);

        /// <summary>
        /// Initializes the notification center with full control over the arguments.
        /// <see cref="NotificationCenterArgs.AndroidChannelId"/> is required.
        /// </summary>
        public void Initialize(NotificationCenterArgs args);

        /// <summary>
        /// Asks the user for permission to post notifications. The callback answers with the
        /// settled status, immediately when the platform already knows it.
        /// </summary>
        public void RequestPermission(Action<NotificationsPermissionStatus> callback);

        /// <summary>
        /// Schedules a notification at a wall-clock time, optionally repeating.
        /// Returns the notification identifier, -1 when the manager is not initialized.
        /// </summary>
        public int Schedule(Notification notification, DateTime fireTime, NotificationRepeatInterval repeatInterval = NotificationRepeatInterval.OneTime);

        /// <summary>
        /// Schedules a notification after a delay, optionally repeating with that interval.
        /// Returns the notification identifier, -1 when the manager is not initialized.
        /// </summary>
        public int Schedule(Notification notification, TimeSpan delay, bool repeats = false);

        public int Schedule(string title, string text, DateTime fireTime, NotificationRepeatInterval repeatInterval = NotificationRepeatInterval.OneTime);
        public int Schedule(string title, string text, TimeSpan delay, bool repeats = false);

        public void CancelScheduled(int id);
        public void CancelAllScheduled();

        /// <summary>
        /// Removes an already delivered notification from the tray / notification center.
        /// </summary>
        public void CancelDelivered(int id);
        public void CancelAllDelivered();

        /// <summary>
        /// Resets the application badge (iOS; Android launchers that show one follow the tray).
        /// </summary>
        public void ClearBadge();

        /// <summary>
        /// Opens the system notification settings for this application.
        /// </summary>
        public void OpenSettings();

        /// <summary>
        /// Answers with the notification the app was opened from, null when the app was opened
        /// normally. Right after launch the platform may need a few frames to deliver it; the
        /// callback fires once the answer settled.
        /// </summary>
        public void GetLastRespondedNotification(Action<Notification?> callback);
    }
}
