using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Linq;
using System;
using Mu3Library.Extensions;
using System.Collections.Generic;

#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem.UI;
#endif

namespace Mu3Library.UI.MVP
{
    public static class MVPCanvasUtil
    {
        private static EventSystem m_eventSystem = null;
        private static EventSystem _eventSystem
        {
            get
            {
                if (m_eventSystem == null)
                {
                    m_eventSystem = EventSystem.current;
                    if (m_eventSystem == null)
                    {
                        List<Type> components = new List<Type>();
                        components.Add(typeof(Canvas));
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
                        components.Add(typeof(InputSystemUIInputModule));
#else
                        components.Add(typeof(StandaloneInputModule));
#endif
                        GameObject go = new GameObject("EventSystem", components.ToArray());
                        m_eventSystem = go.GetComponent<EventSystem>();
                    }
                }

                return m_eventSystem;
            }
        }
        public static EventSystem EventSystem => _eventSystem;

        private static string[] m_sortingLayers = null;
        private static string[] _sortingLayers
        {
            get
            {
                if (m_sortingLayers == null)
                {
                    m_sortingLayers = SortingLayer.layers.Select(t => t.name).ToArray();
                }

                return m_sortingLayers;
            }
        }
        public static string[] SortingLayers => _sortingLayers;

        private static readonly Dictionary<string, int> m_sortingLayerOrderMap = new();
        private static Dictionary<string, int> _sortingLayerOrderMap
        {
            get
            {
                if (m_sortingLayerOrderMap.Count == 0)
                {
                    for (int i = 0; i < _sortingLayers.Length; i++)
                    {
                        m_sortingLayerOrderMap.Add(_sortingLayers[i], i);
                    }
                }

                return m_sortingLayerOrderMap;
            }
        }

        private static readonly Vector2 _defaultResolution = new(1920, 1080);

        public static Canvas ApplyCanvasSettings(
            GameObject go,
            MVPCanvasSettings settings,
            Camera renderCamera = null,
            string sortingLayerNameOverride = null)
        {
            if (go == null)
            {
                Debug.LogWarning("Exist null object.");
                return null;
            }

            Canvas canvas = go.GetOrAddComponent<Canvas>();
            ApplyCanvasSettings(canvas, settings, renderCamera, sortingLayerNameOverride);
            ApplyScalerSettings(go, settings);
            GetOrAddGraphicRaycaster(go);

            return canvas;
        }

        public static void ApplyCanvasSettings(
            Canvas canvas,
            MVPCanvasSettings settings,
            Camera renderCamera = null,
            string sortingLayerNameOverride = null)
        {
            if (canvas == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            canvas.renderMode = settings.RenderMode;
            canvas.sortingLayerName = string.IsNullOrEmpty(sortingLayerNameOverride)
                ? settings.SortingLayerName
                : sortingLayerNameOverride;
            canvas.sortingOrder = settings.SortingOrder;
            canvas.planeDistance = settings.PlaneDistance;

            switch (settings.RenderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    canvas.worldCamera = renderCamera != null ? renderCamera : Camera.main;
                    break;
                case RenderMode.ScreenSpaceOverlay:
                    canvas.worldCamera = null;
                    break;
            }
        }

        public static CanvasScaler ApplyScalerSettings(GameObject go, MVPCanvasSettings settings)
        {
            if (go == null)
            {
                Debug.LogWarning("Exist null object.");
                return null;
            }

            CanvasScaler component = go.GetOrAddComponent<CanvasScaler>();
            component.uiScaleMode = settings.UIScaleMode;
            component.referenceResolution = settings.Resolution != default
                ? settings.Resolution
                : _defaultResolution;
            component.screenMatchMode = settings.ScreenMatchMode;
            component.matchWidthOrHeight = settings.MatchWidthOrHeight;
            component.physicalUnit = settings.PhysicalUnit;
            component.fallbackScreenDPI = settings.FallbackScreenDPI;
            component.defaultSpriteDPI = settings.SpriteDPI;
            component.scaleFactor = settings.ScaleFactor;

            return component;
        }

        public static GraphicRaycaster GetOrAddGraphicRaycaster(GameObject go)
        {
            GraphicRaycaster component = go.GetOrAddComponent<GraphicRaycaster>();

            return component;
        }

        public static void Overwrite(Canvas source, Canvas target, bool overwriteScaler = false, bool overwriteRaycaster = false)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.renderMode = source.renderMode;
            target.overrideSorting = source.overrideSorting;
            target.pixelPerfect = source.pixelPerfect;
            target.worldCamera = source.worldCamera;
            target.planeDistance = source.planeDistance;
            target.sortingLayerName = source.sortingLayerName;
            target.sortingOrder = source.sortingOrder;
            target.targetDisplay = source.targetDisplay;
            target.additionalShaderChannels = source.additionalShaderChannels;

            if (overwriteScaler &&
                source.TryGetComponent(out GraphicRaycaster raycasterSource) &&
                target.TryGetComponent(out GraphicRaycaster raycasterTarget))
            {
                Overwrite(raycasterSource, raycasterTarget);
            }

            if (overwriteRaycaster &&
                source.TryGetComponent(out CanvasScaler scalerSource) &&
                target.TryGetComponent(out CanvasScaler scalerTarget))
            {
                Overwrite(scalerSource, scalerTarget);
            }
        }

        public static void Overwrite(CanvasScaler source, CanvasScaler target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.uiScaleMode = source.uiScaleMode;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
            target.scaleFactor = source.scaleFactor;
            target.referenceResolution = source.referenceResolution;
            target.screenMatchMode = source.screenMatchMode;
            target.matchWidthOrHeight = source.matchWidthOrHeight;
            target.physicalUnit = source.physicalUnit;
            target.fallbackScreenDPI = source.fallbackScreenDPI;
            target.defaultSpriteDPI = source.defaultSpriteDPI;
            target.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        }

        public static void Overwrite(GraphicRaycaster source, GraphicRaycaster target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.ignoreReversedGraphics = source.ignoreReversedGraphics;
            target.blockingObjects = source.blockingObjects;
            target.blockingMask = source.blockingMask;
        }

        public static int GetSortingLayerOrder(string layerName)
        {
            if (_sortingLayerOrderMap.TryGetValue(layerName, out int order))
            {
                return order;
            }
            return -1;
        }
    }
}
