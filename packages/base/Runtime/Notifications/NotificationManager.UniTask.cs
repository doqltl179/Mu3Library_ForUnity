#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using Unity.Notifications;

namespace Mu3Library.Notifications
{
    public partial class NotificationManager
    {
        public async UniTask<NotificationsPermissionStatus> RequestPermissionAsync()
        {
            if (!IsUsable())
            {
                return NotificationsPermissionStatus.Denied;
            }

            NotificationsPermissionRequest request = NotificationCenter.RequestPermission();
            if (request.Status == NotificationsPermissionStatus.RequestPending)
            {
                // A disposed manager stops waiting and answers with whatever the request
                // reports at that moment, mirroring how the callback path goes quiet.
                await UniTask.WaitUntil(() => _isDisposed || request.Status != NotificationsPermissionStatus.RequestPending);
            }

            return request.Status;
        }

        public async UniTask<Notification?> GetLastRespondedNotificationAsync()
        {
            if (!IsUsable())
            {
                return null;
            }

            QueryLastRespondedNotificationOp operation = NotificationCenter.QueryLastRespondedNotification();
            if (operation.State == QueryLastRespondedNotificationState.Pending)
            {
                await UniTask.WaitUntil(() => _isDisposed || operation.State != QueryLastRespondedNotificationState.Pending);
            }

            if (_isDisposed)
            {
                return null;
            }

            return ResolveQuery(operation);
        }
    }
}
#endif
