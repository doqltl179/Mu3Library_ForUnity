#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using Unity.Notifications;

namespace Mu3Library.Notifications
{
    public partial interface INotificationManager
    {
        /// <summary>
        /// Asks the user for permission to post notifications and completes with the settled
        /// status, immediately when the platform already knows it.
        /// </summary>
        public UniTask<NotificationsPermissionStatus> RequestPermissionAsync();

        /// <summary>
        /// Completes with the notification the app was opened from, null when the app was
        /// opened normally. Right after launch the platform may need a few frames to deliver
        /// it, which this waits for.
        /// </summary>
        public UniTask<Notification?> GetLastRespondedNotificationAsync();
    }
}
#endif
