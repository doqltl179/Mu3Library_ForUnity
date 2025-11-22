using System;
using System.Collections.Generic;
using Mu3Library.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    public class MVPFactory : GenericSingleton<MVPFactory>
    {
        private readonly Dictionary<Type, View> _viewResourceMap = new();
        private readonly Dictionary<Type, string> _viewLayerMap = new();
        private readonly Dictionary<Type, Queue<IPresenter>> _presenterPool = new();

        public int ViewResourceCount => _viewResourceMap.Count;

        #region Default Properties

        private RenderMode _defaultRenderMode = RenderMode.ScreenSpaceCamera;
        public RenderMode DefaultRenderMode
        {
            get => _defaultRenderMode;
            set => _defaultRenderMode = value;
        }

        private string _defaultSortingLayerName = "Default";
        public string DefaultSortingLayerName
        {
            get => _defaultSortingLayerName;
            set => _defaultSortingLayerName = value;
        }

        private int _defaultSortingOrder = 0;
        public int DefaultSortingOrder
        {
            get => _defaultSortingOrder;
            set => _defaultSortingOrder = value;
        }

        private float _defaultPlaneDistance = 0;
        public float DefaultPlaneDistance
        {
            get => _defaultPlaneDistance;
            set => _defaultPlaneDistance = value;
        }

        private CanvasScaler.ScaleMode _defaultUIScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        public CanvasScaler.ScaleMode DefaultUIScaleMode
        {
            get => _defaultUIScaleMode;
            set => _defaultUIScaleMode = value;
        }

        private Vector2 _defaultResolution = new Vector2(1920, 1080);
        public Vector2 DefaultResolution
        {
            get => _defaultResolution;
            set => _defaultResolution = value;
        }

        private CanvasScaler.ScreenMatchMode _defaultScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        public CanvasScaler.ScreenMatchMode DefaultScreenMatchMode
        {
            get => _defaultScreenMatchMode;
            set => _defaultScreenMatchMode = value;
        }

        private float _defaultMatchWidthOrHeight = 0.0f;
        public float DefaultMatchWidthOrHeight
        {
            get => _defaultMatchWidthOrHeight;
            set => _defaultMatchWidthOrHeight = value;
        }

        private CanvasScaler.Unit _defaultPhysicalUnit = CanvasScaler.Unit.Points;
        public CanvasScaler.Unit DefaultPhysicalUnit
        {
            get => _defaultPhysicalUnit;
            set => _defaultPhysicalUnit = value;
        }

        private float _defaultFallbackScreenDPI = 96.0f;
        public float DefaultFallbackScreenDPI
        {
            get => _defaultFallbackScreenDPI;
            set => _defaultFallbackScreenDPI = value;
        }

        private float _defaultSpriteDPI = 96.0f;
        public float DefaultSpriteDPI
        {
            get => _defaultSpriteDPI;
            set => _defaultSpriteDPI = value;
        }

        private float _defaultScaleFactor = 1.0f;
        public float DefaultScaleFactor
        {
            get => _defaultScaleFactor;
            set => _defaultScaleFactor = value;
        }

        #endregion



        #region Utility
        public OutPanel CreateOutPanel(Transform parent = null)
        {
            GameObject go = new GameObject(
                "OutPanel",
                new Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(OutPanel) });
            go.transform.SetParent(parent);

            return go.GetComponent<OutPanel>();
        }

        public Canvas CreateLayerDefaultCanvas(string layerName, Transform parent = null, Camera renderCamera = null)
        {
            GameObject go = new GameObject(
                $"Canvas_{layerName}",
                new Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) });
            go.transform.SetParent(parent);

            Canvas result = null;
            switch (_defaultRenderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                    {
                        result = CanvasUtil.GetOrAddScreenCameraCanvasComponent(
                            go,
                            renderCamera,
                            _defaultPlaneDistance,
                            _defaultSortingLayerName,
                            _defaultSortingOrder);
                    }
                    break;
                case RenderMode.ScreenSpaceOverlay:
                    {
                        result = CanvasUtil.GetOrAddScreenOverlayCanvasComponent(
                            go,
                            _defaultSortingOrder);
                    }
                    break;
                case RenderMode.WorldSpace:
                    {
                        result = CanvasUtil.GetOrAddWorldCanvasComponent(
                            go,
                            renderCamera,
                            _defaultSortingLayerName,
                            _defaultSortingOrder);
                    }
                    break;
            }

            switch (_defaultUIScaleMode)
            {
                case CanvasScaler.ScaleMode.ConstantPhysicalSize:
                    {
                        CanvasUtil.GetOrAddCanvasPhysicalSizeScalerComponent(
                            go,
                            _defaultPhysicalUnit,
                            _defaultFallbackScreenDPI,
                            _defaultSpriteDPI);
                    }
                    break;
                case CanvasScaler.ScaleMode.ConstantPixelSize:
                    {
                        CanvasUtil.GetOrAddCanvasPixelSizeScalerComponent(
                            go,
                            _defaultScaleFactor);
                    }
                    break;
                case CanvasScaler.ScaleMode.ScaleWithScreenSize:
                    {
                        CanvasUtil.GetOrAddCanvasScaleSizeScalerComponent(
                            go,
                            _defaultResolution,
                            _defaultScreenMatchMode,
                            _defaultMatchWidthOrHeight);
                    }
                    break;
            }

            CanvasUtil.GetOrAddGraphicRaycasterComponent(go);

            result.sortingLayerName = layerName;

            return result;
        }

        public void PoolPrsenter<TPresenter>(TPresenter presenter) where TPresenter : IPresenter
        {
            if (presenter == null || !presenter.IsViewExist)
            {
                return;
            }

            Type presenterType = presenter.GetType();
            if (!_presenterPool.TryGetValue(presenterType, out var pool))
            {
                pool = new Queue<IPresenter>();
                _presenterPool.Add(presenterType, pool);
            }

            pool.Enqueue(presenter);
        }

        public TPresenter CreatePresenter<TPresenter>() where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = null;

            Type presenterType = typeof(TPresenter);
            if (_presenterPool.TryGetValue(presenterType, out Queue<IPresenter> pool) && pool.Count > 0)
            {
                IPresenter inst = null;

                while (inst == null && pool.Count > 0)
                {
                    inst = pool.Dequeue();
                }

                if (pool.Count == 0)
                {
                    _presenterPool.Remove(presenterType);
                }

                presenter = inst as TPresenter;
            }

            if (presenter == null)
            {
                presenter = new TPresenter();
            }

            return presenter;
        }

        public TView CreateView<TView>(Canvas rootCanvas) where TView : View
        {
            Type viewType = typeof(TView);
            return CreateView(viewType, rootCanvas) as TView;
        }

        public View CreateView(Type viewType, Canvas rootCanvas)
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

            View inst = Instantiate(resource, rootCanvas.transform);

            CanvasUtil.Overwrite(rootCanvas, inst.Canvas, true, true);
            inst.Canvas.overrideSorting = true;
            inst.SetSortingOrder(resource.SortingOrder);

            return inst;
        }

        public string GetLayerName<TView>() where TView : View => GetLayerName(typeof(TView));

        public string GetLayerName(Type viewType)
        {
            if (!_viewLayerMap.ContainsKey(viewType))
            {
                return "";
            }

            return _viewLayerMap[viewType];
        }

        public void AddViewResource(View view)
        {
            Type type = view.GetType();
            if (!_viewResourceMap.ContainsKey(type))
            {
                _viewResourceMap.Add(type, view);

                Canvas canvas = view.GetComponent<Canvas>();
                _viewLayerMap.Add(
                    type,
                    canvas != null ? canvas.sortingLayerName : CanvasUtil.SortingLayers[0]);
            }
            else
            {
                Debug.LogWarning($"View type already exist. type: {type}");
            }
        }

        public void FillViewResources(string resourcesPath)
        {
            var viewResources = Resources.LoadAll<View>(resourcesPath);
            foreach (var view in viewResources)
            {
                AddViewResource(view);
            }
        }
        #endregion
    }
}
