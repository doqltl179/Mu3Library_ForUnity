using System;
using Unity.Notifications;
using UnityEngine;

namespace Mu3Library.Notifications
{
    /// <summary>
    /// Thin wrapper over the unified <see cref="NotificationCenter"/>. The permission request
    /// and the last-responded query are yieldable operations in the underlying package, which
    /// pushes no events of its own; this manager watches each one in a short-lived async loop
    /// that exists only while that operation runs, the way <c>WebRequestManager</c> follows a
    /// download. Nothing polls while nothing is pending, and the manager works the same however
    /// it is hosted, with no per-frame driver required.
    /// </summary>
    public partial class NotificationManager : INotificationManager, INotificationManagerEventBus, IDisposable
    {
        private const int InvalidNotificationId = -1;

        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private bool _isDisposed = false;

        private bool _isReceivedHooked = false;
        private Action<Notification> _onNotificationReceived;

        /// <summary>
        /// Subscribing attaches this manager to the package's received event once; the manager
        /// owns that subscription and takes it back off in <see cref="Dispose"/>.
        /// </summary>
        public event Action<Notification> OnNotificationReceived
        {
            add
            {
                EnsureReceivedHook();
                _onNotificationReceived += value;
            }
            remove => _onNotificationReceived -= value;
        }



        public void Dispose()
        {
            // The flag also ends any operation watch still in flight, so a callback can never
            // land on a manager that has already been torn down.
            _isDisposed = true;

            if (_isReceivedHooked)
            {
                NotificationCenter.OnNotificationReceived -= HandleNotificationReceived;
                _isReceivedHooked = false;
            }

            _onNotificationReceived = null;
        }

        #region Initialize
        public void Initialize(string androidChannelId, string androidChannelName, string androidChannelDescription)
        {
            NotificationCenterArgs args = NotificationCenterArgs.Default;
            args.AndroidChannelId = androidChannelId;
            args.AndroidChannelName = androidChannelName;
            args.AndroidChannelDescription = androidChannelDescription;

            Initialize(args);
        }

        public void Initialize(NotificationCenterArgs args)
        {
            if (_isInitialized)
            {
                return;
            }

            // The underlying package throws for a missing channel id; reporting keeps the
            // manager on the library's log-and-return convention instead.
            if (string.IsNullOrEmpty(args.AndroidChannelId))
            {
                Debug.LogError("AndroidChannelId is required. Notifications are not initialized.");
                return;
            }

            NotificationCenter.Initialize(args);
            _isInitialized = true;
        }
        #endregion

        #region Permission
        public void RequestPermission(Action<NotificationsPermissionStatus> callback)
        {
            if (!IsUsable())
            {
                callback?.Invoke(NotificationsPermissionStatus.Denied);
                return;
            }

            NotificationsPermissionRequest request = NotificationCenter.RequestPermission();
            if (request.Status != NotificationsPermissionStatus.RequestPending)
            {
                callback?.Invoke(request.Status);
                return;
            }

            WatchPermissionRequest(request, callback);
        }

        /// <summary>
        /// Follows one permission request until the user settled it. Unity's synchronization
        /// context brings every continuation back to the main thread, and the loop ends with
        /// the request, so nothing keeps polling once the answer arrived.
        /// </summary>
        private async void WatchPermissionRequest(NotificationsPermissionRequest request, Action<NotificationsPermissionStatus> callback)
        {
            try
            {
                while (!_isDisposed && request.Status == NotificationsPermissionStatus.RequestPending)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

                if (_isDisposed)
                {
                    return;
                }

                callback?.Invoke(request.Status);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
        #endregion

        #region Schedule
        public int Schedule(Notification notification, DateTime fireTime, NotificationRepeatInterval repeatInterval = NotificationRepeatInterval.OneTime)
        {
            if (!IsUsable())
            {
                return InvalidNotificationId;
            }

            return NotificationCenter.ScheduleNotification(notification, new NotificationDateTimeSchedule(fireTime, repeatInterval));
        }

        public int Schedule(Notification notification, TimeSpan delay, bool repeats = false)
        {
            if (!IsUsable())
            {
                return InvalidNotificationId;
            }

            return NotificationCenter.ScheduleNotification(notification, new NotificationIntervalSchedule(delay, repeats));
        }

        public int Schedule(string title, string text, DateTime fireTime, NotificationRepeatInterval repeatInterval = NotificationRepeatInterval.OneTime)
            => Schedule(CreateNotification(title, text), fireTime, repeatInterval);

        public int Schedule(string title, string text, TimeSpan delay, bool repeats = false)
            => Schedule(CreateNotification(title, text), delay, repeats);

        private static Notification CreateNotification(string title, string text)
        {
            return new Notification
            {
                Title = title,
                Text = text,
                ShowInForeground = true,
            };
        }
        #endregion

        #region Cancel
        public void CancelScheduled(int id)
        {
            if (IsUsable())
            {
                NotificationCenter.CancelScheduledNotification(id);
            }
        }

        public void CancelAllScheduled()
        {
            if (IsUsable())
            {
                NotificationCenter.CancelAllScheduledNotifications();
            }
        }

        public void CancelDelivered(int id)
        {
            if (IsUsable())
            {
                NotificationCenter.CancelDeliveredNotification(id);
            }
        }

        public void CancelAllDelivered()
        {
            if (IsUsable())
            {
                NotificationCenter.CancelAllDeliveredNotifications();
            }
        }

        public void ClearBadge()
        {
            if (IsUsable())
            {
                NotificationCenter.ClearBadge();
            }
        }
        #endregion

        public void OpenSettings()
        {
            if (IsUsable())
            {
                NotificationCenter.OpenNotificationSettings();
            }
        }

        public void GetLastRespondedNotification(Action<Notification?> callback)
        {
            if (!IsUsable())
            {
                callback?.Invoke(null);
                return;
            }

            QueryLastRespondedNotificationOp operation = NotificationCenter.QueryLastRespondedNotification();
            if (operation.State != QueryLastRespondedNotificationState.Pending)
            {
                callback?.Invoke(ResolveQuery(operation));
                return;
            }

            WatchQuery(operation, callback);
        }

        /// <summary>
        /// Follows one last-responded query until the platform delivered its answer, on the
        /// same scoped pattern as <see cref="WatchPermissionRequest"/>.
        /// </summary>
        private async void WatchQuery(QueryLastRespondedNotificationOp operation, Action<Notification?> callback)
        {
            try
            {
                while (!_isDisposed && operation.State == QueryLastRespondedNotificationState.Pending)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

                if (_isDisposed)
                {
                    return;
                }

                callback?.Invoke(ResolveQuery(operation));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Notification? ResolveQuery(QueryLastRespondedNotificationOp operation)
        {
            return operation.State == QueryLastRespondedNotificationState.HaveRespondedNotification
                ? operation.Notification
                : (Notification?)null;
        }

        private void EnsureReceivedHook()
        {
            if (_isReceivedHooked)
            {
                return;
            }

            _isReceivedHooked = true;
            NotificationCenter.OnNotificationReceived += HandleNotificationReceived;
        }

        private void HandleNotificationReceived(Notification notification)
        {
            _onNotificationReceived?.Invoke(notification);
        }

        private bool IsUsable()
        {
            if (!_isInitialized)
            {
                Debug.LogError("NotificationManager is not initialized. Call Initialize() first.");
                return false;
            }

            return true;
        }
    }
}
