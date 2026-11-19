# Mu3 Library Notifications

Local notifications package of **Mu3Library For Unity**: `NotificationManager` wraps the unified `NotificationCenter` of Unity's Mobile Notifications package, with initialization with an Android channel, permission requests, scheduling at a time or after a delay, cancels, badge clearing, opening the system settings, and the notification the app was opened from.

- Package name: `com.github.doqltl179.mu3library.notifications`
- Assembly and namespace: `Mu3Library.Notifications`, compiled on Android, iOS, and in the editor only.
- Depends on Unity Mobile Notifications (`com.unity.mobile.notifications`), which Package Manager installs with this package. It uses no other Mu3Library package.
- With [UniTask](https://github.com/Cysharp/UniTask) installed (`MU3LIBRARY_UNITASK_SUPPORT`), `RequestPermissionAsync` and `GetLastRespondedNotificationAsync` await the answers that `RequestPermission` and `GetLastRespondedNotification` pass to their callbacks.

## Installation

Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation) gives the package's Git URL with its latest release tag.

Up to Base 0.28.0, `NotificationManager` came with Base. A project that uses it installs this package when it updates Base past 0.28.0. The assembly name, the namespace, and the GUIDs in the `.meta` files are unchanged, so its code needs no change, and an assembly definition that references `Mu3Library.Notifications` by name or by GUID keeps working.

## Usage

Initialize the manager before anything else: until then every method other than `Initialize` and `Dispose` logs an error and does nothing or answers a safe default, such as `-1` from `Schedule` and `Denied` from `RequestPermission`. Subscribing to `OnNotificationReceived` needs no initialization. In the editor the Mobile Notifications package delivers nothing, so the calls do nothing there.

```csharp
using System;
using Mu3Library.Notifications;
using Unity.Notifications;
using UnityEngine;

public class RewardReminder : MonoBehaviour
{
    private NotificationManager _notifications;

    private void Start()
    {
        _notifications = new NotificationManager();
        _notifications.Initialize("rewards", "Rewards", "Daily reward reminders");
        _notifications.OnNotificationReceived += notification => Debug.Log($"Received: {notification.Title}");

        _notifications.RequestPermission(status =>
        {
            if (status == NotificationsPermissionStatus.Granted)
            {
                _notifications.Schedule("Daily reward", "Your reward is ready.", TimeSpan.FromHours(24));
            }
        });
    }

    private void OnDestroy()
    {
        _notifications.Dispose();
    }
}
```

`OnNotificationReceived`, from `INotificationManagerEventBus`, reports a notification that arrives while the app runs: on Android also when the app returns from the background, on iOS only while the app is in the foreground. `Dispose` takes the manager's subscription to the Mobile Notifications package back off and ends any permission request or query it still follows, whose callback then never runs.

With the Mu3 Library DI package ([README](../di/README.md)), a core registers the manager like any other service, as a singleton under `NotificationManager` and its interfaces `INotificationManager` and `INotificationManagerEventBus`:

```csharp
using Mu3Library.DI;
using Mu3Library.Notifications;

public class NotificationCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<NotificationManager>();
    }
}
```

Code that uses the package compiles only where its assembly does. Put it in an assembly definition of your own that references `Mu3Library.Notifications` and lists the same platforms (Android, Editor, iOS) under `includePlatforms`, or behind `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR`; otherwise a player for any other platform fails to compile.

## Documentation

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
