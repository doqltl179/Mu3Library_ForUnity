using Mu3Library.DI;
using UnityEngine;
#if TEMPLATE_NOTIFICATIONS_SUPPORT && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using Mu3Library.Notifications;
#endif

namespace Mu3Library.Sample.Template.Global
{
    /// <summary>
    /// The platform gate mirrors the notifications assembly: it only exists on Android, iOS,
    /// and in the editor, so a standalone build compiles this core down to the warning branch.
    /// </summary>
    public class NotificationCore : CoreBase
    {
        protected override void ConfigureContainer()
        {
#if TEMPLATE_NOTIFICATIONS_SUPPORT && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
            RegisterClass<NotificationManager>();
#else
            Debug.LogWarning("Mobile Notifications is not installed.");
#endif
        }
    }
}
