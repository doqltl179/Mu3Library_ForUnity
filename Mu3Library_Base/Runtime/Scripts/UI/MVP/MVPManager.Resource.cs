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
        public void SetLayerCanvasSettings(MVPCanvasSettings settings)
        {
            if (!_layerCanvases.TryGetValue(settings.CanvasSettings.SortingLayerName, out Canvas layerCanvas))
            {
                layerCanvas = CreateLayerCanvas(settings);

                _layerCanvases.Add(settings.CanvasSettings.SortingLayerName, layerCanvas);
            }
            else
            {
                ApplyMVPCanvasSettings(layerCanvas.gameObject, settings);
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

        private Canvas CreateLayerCanvas(MVPCanvasSettings settings)
        {
            GameObject go = new GameObject($"Canvas_{settings.CanvasSettings.SortingLayerName}");
            go.transform.SetParent(_rootTransform);
            go.layer = LayerMask.NameToLayer("UI");

            ApplyMVPCanvasSettings(go, settings);

            return go.GetComponent<Canvas>();
        }

        private TView CreateView<TView>(Canvas layerCanvas) where TView : View
        {
            System.Type viewType = typeof(TView);
            return CreateView(viewType, layerCanvas) as TView;
        }

        private View CreateView(System.Type viewType, Canvas layerCanvas)
            => CreateView(viewType, layerCanvas, null);

        private View CreateView(System.Type viewType, Canvas layerCanvas, Transform parentTransform)
        {
            if (!TryGetViewResource(viewType, out View resource))
            {
                return null;
            }

            return CreateView(resource, layerCanvas, parentTransform);
        }

        private View CreateView(View resource, Canvas layerCanvas, Transform parentTransform = null)
        {
            if (resource == null)
            {
                Debug.LogError("Resource view is NULL.");
                return null;
            }

            View inst = Object.Instantiate(resource, parentTransform != null ? parentTransform : layerCanvas.transform);

            layerCanvas.CopyTo(inst.Canvas, true, true);
            inst.Canvas.overrideSorting = true;
            inst.SetSortingOrder(resource.SortingOrder);

            return inst;
        }

        private bool TryGetViewResource(System.Type viewType, out View resource)
        {
            if (!_viewResourceMap.TryGetValue(viewType, out resource))
            {
                Debug.LogError($"View not found. type: {viewType}");
                return false;
            }

            if (resource == null)
            {
                Debug.LogError($"Resource view is NULL. type: {viewType}");
                return false;
            }

            return true;
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

        private void ApplyMVPCanvasSettings(GameObject go, MVPCanvasSettings settings)
        {
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            ApplyCanvasSettings(canvas, settings.CanvasSettings);

            CanvasScaler scaler = go.GetOrAddComponent<CanvasScaler>();
            ApplyScalerSettings(scaler, settings.CanvasScalerSettings);

            GraphicRaycaster graphicRaycaster = go.GetOrAddComponent<GraphicRaycaster>();
            ApplyGraphicRaycasterSettings(graphicRaycaster, settings.GraphicRaycasterSettings);
        }

        private void ApplyCanvasSettings(Canvas canvas, CanvasSettings settings)
        {
            if (canvas == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            // The camera is not part of the serialized settings: a screen space camera or world
            // space canvas renders through the manager camera, an overlay canvas through none.
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

            settings.WriteToTarget(canvas);
        }

        private void ApplyScalerSettings(CanvasScaler canvasScaler, CanvasScalerSettings settings)
        {
            if (canvasScaler == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            // An unset reference resolution would scale the UI against zero, so the standard one
            // stands in for it. The settings are a struct, so this only changes the local copy.
            if (settings.Resolution == default)
            {
                settings.Resolution = CanvasScalerSettings.Standard.Resolution;
            }

            settings.WriteToTarget(canvasScaler);
        }

        private void ApplyGraphicRaycasterSettings(GraphicRaycaster graphicRaycaster, GraphicRaycasterSettings settings)
        {
            if (graphicRaycaster == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            settings.WriteToTarget(graphicRaycaster);
        }
    }
}
