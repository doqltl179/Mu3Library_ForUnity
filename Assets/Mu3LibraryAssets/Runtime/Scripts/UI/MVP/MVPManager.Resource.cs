using System.Collections.Generic;
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
                SetCanvasSettings(layerCanvas, settings, layerName);
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
                    canvas != null ? canvas.sortingLayerName : MVPCanvasUtil.SortingLayers[0]);
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
            GameObject go = new GameObject(
                $"Canvas_{layerName}",
                new System.Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) });
            go.transform.SetParent(_rootTransform);

            return MVPCanvasUtil.ApplyCanvasSettings(go, settings, _renderCamera, layerName);
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

            MVPCanvasUtil.Overwrite(layerCanvas, inst.Canvas, true, true);
            inst.Canvas.overrideSorting = true;
            inst.SetSortingOrder(resource.SortingOrder);

            return inst;
        }

        private string GetLayerName<TView>() where TView : View => GetLayerName(typeof(TView));

        private string GetLayerName(System.Type viewType)
        {
            if (!_viewLayerMap.ContainsKey(viewType))
            {
                return "";
            }

            return _viewLayerMap[viewType];
        }

        private void SetCanvasSettings(Canvas canvas, MVPCanvasSettings settings, string sortingLayerNameOverride = null)
        {
            if (canvas == null)
            {
                return;
            }

            MVPCanvasUtil.ApplyCanvasSettings(canvas, settings, _renderCamera, sortingLayerNameOverride);
            MVPCanvasUtil.ApplyScalerSettings(canvas.gameObject, settings);
            MVPCanvasUtil.GetOrAddGraphicRaycaster(canvas.gameObject);
        }
    }
}
