using System.Collections.Generic;
using Mu3Library.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    public partial class MVPManager
    {
        private readonly Dictionary<System.Type, View> _viewResourceMap = new();
        private readonly Dictionary<System.Type, string> _viewLayerMap = new();
        private readonly Dictionary<string, Canvas> _layerCanvases = new();



        #region Interface
        public void SetLayerCanvasSettings(string layerName, MVPCanvasSettings settings)
        {
            if (!_layerCanvases.TryGetValue(layerName, out Canvas layerCanvas))
            {
                layerCanvas = CreateLayerCanvas(layerName, settings);

                _layerCanvases.Add(layerName, layerCanvas);
            }
            else
            {
                ApplyMVPCanvasSettings(layerCanvas.gameObject, settings, layerName);
            }
        }

        public void RegisterViewResource(View viewResource)
        {
            if (viewResource == null)
            {
                Debug.LogWarning("View resource is null.");
                return;
            }

            System.Type type = viewResource.GetType();
            if (!_viewResourceMap.ContainsKey(type))
            {
                _viewResourceMap.Add(type, viewResource);

                Canvas canvas = viewResource.GetComponent<Canvas>();
                _viewLayerMap.Add(
                    type,
                    canvas != null ? canvas.sortingLayerName : _sortingLayers[0]);
            }
            else
            {
                Debug.LogWarning($"View type already exist. type: {type}");
            }
        }

        public void RegisterViewResources(IEnumerable<View> viewResources)
        {
            foreach (var view in viewResources)
            {
                RegisterViewResource(view);
            }
        }
        #endregion

        private Canvas CreateLayerCanvas(string layerName, MVPCanvasSettings settings)
        {
            GameObject go = new GameObject($"Canvas_{layerName}");
            go.transform.SetParent(_rootTransform);

            ApplyMVPCanvasSettings(go, settings, layerName);

            return go.GetComponent<Canvas>();
        }

        private TView CreateView<TView>(Canvas layerCanvas) where TView : View
        {
            System.Type viewType = typeof(TView);
            return CreateView(viewType, layerCanvas) as TView;
        }

        private View CreateView(System.Type viewType, Canvas layerCanvas)
        {
            if (!_viewResourceMap.ContainsKey(viewType))
            {
                Debug.LogError($"View not found. type: {viewType}");
                return null;
            }

            View resource = _viewResourceMap[viewType];
            if (resource == null)
            {
                Debug.LogError($"Resource view is NULL. type: {viewType}");
                return null;
            }

            View inst = Object.Instantiate(resource, layerCanvas.transform);

            layerCanvas.CopyTo(inst.Canvas, true, true);
            inst.Canvas.overrideSorting = true;
            inst.SetSortingOrder(resource.SortingOrder);

            return inst;
        }

        private string GetLayerName<TView>() where TView : View => GetLayerName(typeof(TView));

        private string GetLayerName(System.Type viewType)
        {
            if (!_viewLayerMap.TryGetValue(viewType, out string layerName))
            {
                Debug.LogWarning($"View layer not registered. type: {viewType}");
                return _sortingLayers.Length > 0
                    ? _sortingLayers[0]
                    : string.Empty;
            }

            return layerName;
        }

        private void ApplyMVPCanvasSettings(GameObject go, MVPCanvasSettings settings, string sortingLayerNameOverride = null)
        {
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            ApplyCanvasSettings(canvas, settings.CanvasSettings, sortingLayerNameOverride);

            CanvasScaler scaler = go.GetOrAddComponent<CanvasScaler>();
            ApplyScalerSettings(scaler, settings.CanvasScalerSettings);

            GraphicRaycaster graphicRaycaster = go.GetOrAddComponent<GraphicRaycaster>();
            ApplyGraphicRaycasterSettings(graphicRaycaster, settings.GraphicRaycasterSettings);
        }

        private void ApplyCanvasSettings(
            Canvas canvas,
            CanvasSettings settings,
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
                    canvas.worldCamera = _renderCamera != null ?
                        _renderCamera :
                        Camera.main;
                    break;
                case RenderMode.ScreenSpaceOverlay:
                    canvas.worldCamera = null;
                    break;
            }
        }

        private void ApplyScalerSettings(CanvasScaler canvasScaler, CanvasScalerSettings settings)
        {
            if (canvasScaler == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            canvasScaler.uiScaleMode = settings.UIScaleMode;
            canvasScaler.referenceResolution = settings.Resolution != default
                ? settings.Resolution
                : new Vector2(1920, 1080);
            canvasScaler.screenMatchMode = settings.ScreenMatchMode;
            canvasScaler.matchWidthOrHeight = settings.MatchWidthOrHeight;
            canvasScaler.physicalUnit = settings.PhysicalUnit;
            canvasScaler.fallbackScreenDPI = settings.FallbackScreenDPI;
            canvasScaler.defaultSpriteDPI = settings.SpriteDPI;
            canvasScaler.scaleFactor = settings.ScaleFactor;
        }

        private void ApplyGraphicRaycasterSettings(GraphicRaycaster graphicRaycaster, GraphicRaycasterSettings settings)
        {
            if (graphicRaycaster == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            graphicRaycaster.ignoreReversedGraphics = settings.IgnoreReversedGraphics;
            graphicRaycaster.blockingObjects = settings.BlockingObjects;
            graphicRaycaster.blockingMask = settings.BlockingMask;
        }
    }
}
