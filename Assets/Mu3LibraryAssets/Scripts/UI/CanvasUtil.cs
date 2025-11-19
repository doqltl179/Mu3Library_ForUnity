using Mu3Library.UI.MVP;
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

namespace Mu3Library.UI
{
    public static class CanvasUtil
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

        private static Vector2 _defaultResolution = new Vector2(1920, 1080);
        public static Vector2 DefaultResolution
        {
            get => _defaultResolution;
            set => _defaultResolution = value;
        }



        public static Canvas GetOrAddDefaultScreenOverlayCanvasComponent(
            GameObject go,
            int sortingOrder = 0)
        {
            Canvas component = go.GetOrAddComponent<Canvas>();
            component.renderMode = RenderMode.ScreenSpaceOverlay;
            component.sortingOrder = sortingOrder;

            return component;
        }

        public static Canvas GetOrAddDefaultScreenCameraCanvasComponent(
            GameObject go,
            Camera renderCamera = null,
            float plainDistance = 100f,
            string layerName = "Default",
            int sortingOrder = 0)
        {
            Canvas component = go.GetOrAddComponent<Canvas>();
            component.renderMode = RenderMode.ScreenSpaceCamera;
            component.sortingLayerName = layerName;
            component.sortingOrder = sortingOrder;
            component.worldCamera = renderCamera != null ? renderCamera : Camera.main;
            component.planeDistance = plainDistance;

            return component;
        }

        public static Canvas GetOrAddDefaultWorldCanvasComponent(
            GameObject go,
            Camera renderCamera = null,
            string layerName = "Default",
            int sortingOrder = 0)
        {
            Canvas component = go.GetOrAddComponent<Canvas>();
            component.renderMode = RenderMode.WorldSpace;
            component.sortingLayerName = layerName;
            component.sortingOrder = sortingOrder;
            component.worldCamera = renderCamera != null ? renderCamera : Camera.main;

            return component;
        }

        public static CanvasScaler GetOrAddDefaultCanvasPixelSizeScalerComponent(
            GameObject go,
            float scaleFactor = 1.0f)
        {
            CanvasScaler component = go.GetOrAddComponent<CanvasScaler>();
            component.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            component.scaleFactor = scaleFactor;

            return component;
        }

        public static CanvasScaler GetOrAddDefaultCanvasScaleSizeScalerComponent(
            GameObject go,
            Vector2 resolution = default,
            CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
            float matchFactor = 1.0f)
        {
            CanvasScaler component = go.GetOrAddComponent<CanvasScaler>();
            component.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            component.referenceResolution = resolution != default ? resolution : _defaultResolution;
            component.screenMatchMode = screenMatchMode;
            component.matchWidthOrHeight = matchFactor;

            return component;
        }

        public static CanvasScaler GetOrAddDefaultCanvasPhysicalSizeScalerComponent(
            GameObject go,
            CanvasScaler.Unit unit = CanvasScaler.Unit.Points,
            float fallbackScreenDPI = 96f,
            float defaultSpriteDPI = 96f)
        {
            CanvasScaler component = go.GetOrAddComponent<CanvasScaler>();
            component.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            component.physicalUnit = unit;
            component.fallbackScreenDPI = fallbackScreenDPI;
            component.defaultSpriteDPI = defaultSpriteDPI;

            return component;
        }

        public static GraphicRaycaster GetOrAddDefaultGraphicRaycasterComponent(GameObject go)
        {
            GraphicRaycaster component = go.GetOrAddComponent<GraphicRaycaster>();

            return component;
        }

        public static void Overwrite(View source, View target, bool overwriteScaler = false, bool overwriteRaycaster = false)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            if (source.TryGetComponent(out Canvas canvasSource) &&
                target.TryGetComponent(out Canvas canvasTarget))
            {
                Overwrite(canvasSource, canvasTarget, overwriteScaler, overwriteRaycaster);
            }
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