using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Scene
{
    public delegate void SceneVoidEventHandler();
    public delegate void SceneFloatEventHandler(float value);

    public class SceneLoadEventHelper
    {
        private readonly Dictionary<string, List<SceneVoidEventHandler>> _onSceneLoadStart = new();
        private readonly Dictionary<string, List<SceneVoidEventHandler>> _onSceneLoadEnd = new();
        private readonly Dictionary<string, List<SceneFloatEventHandler>> _onSceneLoadProgress = new();



        #region Utility
        public void CallSceneLoadStartEvents(string sceneName) => CallVoidEvents(sceneName, _onSceneLoadStart);
        public void CallSceneLoadEndEvents(string sceneName) => CallVoidEvents(sceneName, _onSceneLoadEnd);
        public void CallSceneLoadProgressEvents(string sceneName, float value) => CallFloatEvents(sceneName, _onSceneLoadProgress, value);

        public void AddSceneLoadStartListener(string sceneName, SceneVoidEventHandler callback) => AddVoidEvent(sceneName, _onSceneLoadStart, callback);
        public void RemoveSceneLoadStartListener(string sceneName, SceneVoidEventHandler callback) => RemoveVoidEvent(sceneName, _onSceneLoadStart, callback);

        public void AddSceneLoadEndListener(string sceneName, SceneVoidEventHandler callback) => AddVoidEvent(sceneName, _onSceneLoadEnd, callback);
        public void RemoveSceneLoadEndListener(string sceneName, SceneVoidEventHandler callback) => RemoveVoidEvent(sceneName, _onSceneLoadEnd, callback);

        public void AddSceneLoadProgressListener(string sceneName, SceneFloatEventHandler callback) => AddFloatEvent(sceneName, _onSceneLoadProgress, callback);
        public void RemoveSceneLoadProgressListener(string sceneName, SceneFloatEventHandler callback) => RemoveFloatEvent(sceneName, _onSceneLoadProgress, callback);
        #endregion

        private void AddVoidEvent(string sceneName, Dictionary<string, List<SceneVoidEventHandler>> eventMap, SceneVoidEventHandler callback)
        {
            if (string.IsNullOrEmpty(sceneName) || eventMap == null || callback == null)
            {
                return;
            }

            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                events = new List<SceneVoidEventHandler>();
                eventMap.Add(sceneName, events);
            }
            events.Add(callback);
        }

        private void RemoveVoidEvent(string sceneName, Dictionary<string, List<SceneVoidEventHandler>> eventMap, SceneVoidEventHandler callback)
        {
            if (string.IsNullOrEmpty(sceneName) || eventMap == null || callback == null)
            {
                return;
            }

            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                Debug.LogWarning($"Event not found. sceneName: {sceneName}");
                return;
            }
            events.Remove(callback);
        }

        private void AddFloatEvent(string sceneName, Dictionary<string, List<SceneFloatEventHandler>> eventMap, SceneFloatEventHandler callback)
        {
            if (string.IsNullOrEmpty(sceneName) || eventMap == null || callback == null)
            {
                return;
            }

            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                events = new List<SceneFloatEventHandler>();
                eventMap.Add(sceneName, events);
            }
            events.Add(callback);
        }

        private void RemoveFloatEvent(string sceneName, Dictionary<string, List<SceneFloatEventHandler>> eventMap, SceneFloatEventHandler callback)
        {
            if (string.IsNullOrEmpty(sceneName) || eventMap == null || callback == null)
            {
                return;
            }

            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                Debug.LogWarning($"Event not found. sceneName: {sceneName}");
                return;
            }
            events.Remove(callback);
        }

        private void CallVoidEvents(string sceneName, Dictionary<string, List<SceneVoidEventHandler>> eventMap)
        {
            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var callback = events[i];
                if (callback == null)
                {
                    events.RemoveAt(i);
                    i--;
                    continue;
                }
                callback();
            }
        }
        
        private void CallFloatEvents(string sceneName, Dictionary<string, List<SceneFloatEventHandler>> eventMap, float value)
        {
            if (!eventMap.TryGetValue(sceneName, out var events))
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var callback = events[i];
                if (callback == null)
                {
                    events.RemoveAt(i);
                    i--;
                    continue;
                }
                callback(value);
            }
        }
    }
}