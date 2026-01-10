using System.Collections.Generic;
using System.Linq;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    public class MVPManager : IMVPManager, IUpdatable, IDisposable
    {
        private GameObject m_root;
        private GameObject _root
        {
            get
            {
                if (m_root == null)
                {
                    m_root = new GameObject("MVPManagerRoot");
                    Object.DontDestroyOnLoad(m_root);
                }

                return m_root;
            }
        }

        private Transform _rootTransform => _root.transform;

        private readonly Dictionary<System.Type, View> _viewResourceMap = new();
        private readonly Dictionary<System.Type, string> _viewLayerMap = new();
        private readonly Dictionary<System.Type, Queue<IPresenter>> _presenterPool = new();
        private MVPCanvasSettings _canvasSettings = MVPCanvasSettings.Standard;
        public MVPCanvasSettings CanvasSettings
        {
            get => _canvasSettings;
            set => _canvasSettings = value;
        }

        private Camera m_renderCamera = null;
        private Camera _renderCamera
        {
            get
            {
                if (m_renderCamera == null)
                {
                    GameObject go = new GameObject("RenderCamera");
                    go.transform.SetParent(_rootTransform);

                    Camera camera = go.AddComponent<Camera>();
                    camera.cullingMask = LayerMask.GetMask("UI");
                    camera.clearFlags = CameraClearFlags.Depth;

                    m_renderCamera = camera;
                }

                return m_renderCamera;
            }
        }
        public Camera RenderCamera => _renderCamera;

        private readonly Dictionary<string, Canvas> _layerCanvases = new();

        private class PresenterParams
        {
            public IPresenter Presenter;
            public OutPanelSettings OutPanelSettings;
        }

        private readonly List<PresenterParams> _openedPresenters = new();

        private readonly List<PresenterParams> _presenterLoadChecker = new();
        private readonly List<PresenterParams> _presenterOpenChecker = new();
        private readonly List<PresenterParams> _presenterCloseChecker = new();
        private readonly List<PresenterParams> _presenterUnloadChecker = new();

        private PresenterParams _focused = null;

        private OutPanel _outPanel = null;

        public event System.Action<IPresenter> OnWindowLoaded;
        public event System.Action<IPresenter> OnWindowOpened;
        public event System.Action<IPresenter> OnWindowClosed;
        public event System.Action<IPresenter> OnWindowUnloaded;



        public void Dispose()
        {
            _layerCanvases.Clear();
            _viewResourceMap.Clear();
            _viewLayerMap.Clear();
            _presenterPool.Clear();
            _openedPresenters.Clear();
            _presenterLoadChecker.Clear();
            _presenterOpenChecker.Clear();
            _presenterCloseChecker.Clear();
            _presenterUnloadChecker.Clear();

            _focused = null;

            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        public void Update()
        {
            CheckLifecycle(_presenterLoadChecker, ViewState.Loaded, WindowLoadedEvent);
            CheckLifecycle(_presenterOpenChecker, ViewState.Opened, WindowOpenedEvent);
            CheckLifecycle(_presenterCloseChecker, ViewState.Closed, WindowClosedEvent);
            CheckLifecycle(_presenterUnloadChecker, ViewState.Unloaded, WindowUnloadedEvent);
        }

        private void CheckLifecycle(List<PresenterParams> list, ViewState checkState, System.Action<PresenterParams> callback = null)
        {
            bool isViewDestroyed = false;

            for (int i = 0; i < list.Count; i++)
            {
                PresenterParams param = list[i];
                if (!param.Presenter.IsViewExist)
                {
                    Debug.LogWarning($"View descroyed. checkState: {checkState}, presenter: {param.Presenter.GetType()}");
                    list.RemoveAt(i);
                    i--;

                    isViewDestroyed = true;
                }
                else if (param.Presenter.ViewState == checkState)
                {
                    list.RemoveAt(i);
                    i--;

                    callback?.Invoke(param);
                }
            }

            if (isViewDestroyed)
            {
                UpdateFocus();
            }
        }

        private void WindowLoadedEvent(PresenterParams param)
        {
            if (param.Presenter is ILifecycle lifecycle)
            {
                lifecycle.Open();
            }

            _presenterOpenChecker.Add(param);

            UpdateFocus();

            OnWindowLoaded?.Invoke(param.Presenter);
        }

        private void WindowOpenedEvent(PresenterParams param)
        {
            _openedPresenters.Add(param);

            UpdateFocus();

            OnWindowOpened?.Invoke(param.Presenter);
        }

        private void WindowClosedEvent(PresenterParams param)
        {
            if (param.Presenter is ILifecycle lifecycle)
            {
                lifecycle.Unload();
            }

            _presenterUnloadChecker.Add(param);

            UpdateFocus();

            OnWindowClosed?.Invoke(param.Presenter);
        }

        private void WindowUnloadedEvent(PresenterParams param)
        {
            param.Presenter.SetActiveView(false);
            PoolPresenter(param.Presenter);

            UpdateFocus();

            OnWindowUnloaded?.Invoke(param.Presenter);
        }

        #region Utility
        public void CloseAllWithoutDefault(bool forceClose = false)
        {
            var paramList = Enumerable.Empty<PresenterParams>()
                .Concat(_openedPresenters)
                .Concat(_presenterOpenChecker)
                .Where(t => t.Presenter.LayerName != "Default")
                .ToArray();

            CloseAll(paramList, forceClose);
        }

        public void CloseAll(bool forceClose = false)
        {
            var paramList = Enumerable.Empty<PresenterParams>()
                .Concat(_openedPresenters)
                .Concat(_presenterOpenChecker)
                .ToArray();

            CloseAll(paramList, forceClose);
        }

        public void CloseFocused(bool forceClose = false)
        {
            if (_focused == null || _focused.Presenter == null)
            {
                return;
            }

            PresenterParams closeParam = _focused;
            _focused = null;

            bool canClose = _openedPresenters.Remove(closeParam) ||
                (forceClose && _presenterOpenChecker.Remove(closeParam));

            if (canClose)
            {
                GetInterface(closeParam.Presenter, out var lifecycle);

                lifecycle.Close(forceClose);
                _presenterCloseChecker.Add(closeParam);
            }
            else
            {
                _focused = closeParam;
            }
        }

        public bool Close(IPresenter presenter, bool forceClose = false)
        {
            if (presenter == null)
            {
                return false;
            }

            PresenterParams param = _openedPresenters
                .Where(t => t.Presenter == presenter)
                .LastOrDefault();

            if (param != null)
            {
                _openedPresenters.Remove(param);
            }
            else if (forceClose)
            {
                param = _presenterOpenChecker
                    .Where(t => t.Presenter == presenter)
                    .LastOrDefault();

                if (param != null)
                {
                    _presenterOpenChecker.Remove(param);
                }
            }

            if (param == null)
            {
                return false;
            }

            GetInterface(param.Presenter, out var lifecycle);

            lifecycle.Close(forceClose);
            _presenterCloseChecker.Add(param);

            return true;
        }

        public IPresenter Open<TPresenter>() where TPresenter : class, IPresenter, new()
            => Open<TPresenter>(null, OutPanelSettings.Disabled);

        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : class, IPresenter, new()
            => Open<TPresenter>(args, OutPanelSettings.Disabled);

        public IPresenter Open<TPresenter>(OutPanelSettings settings) where TPresenter : class, IPresenter, new()
            => Open<TPresenter>(null, settings);

        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = CreatePresenter<TPresenter>();
            GetInterface(presenter, out var initialize, out var lifecycle);

            System.Type viewType = presenter.ViewType;
            string viewLayerName = GetLayerName(viewType);

            if (!_layerCanvases.TryGetValue(viewLayerName, out Canvas layerCanvas))
            {
                layerCanvas = CreateLayerCanvas(viewLayerName);

                _layerCanvases.Add(viewLayerName, layerCanvas);
            }

            if (!presenter.IsViewExist)
            {
                View view = CreateView(viewType, layerCanvas);
                if (view == null)
                {
                    return null;
                }

                initialize.Init(view, args);
            }
            else
            {
                initialize.Init(args);
            }

            UpdateSortingOrderAsLast(presenter.View);
            presenter.OptimizeView();
            presenter.SetActiveView(true);

            lifecycle.Load();

            PresenterParams presenterParams = new PresenterParams()
            {
                Presenter = presenter,
                OutPanelSettings = settings,
            };
            _presenterLoadChecker.Add(presenterParams);

            return presenter;
        }

        public void RemoveCullingMask(string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                _renderCamera.cullingMask &= ~(1 << layerIndex);
            }
        }

        public void AddCullingMask(string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                _renderCamera.cullingMask |= 1 << layerIndex;
            }
        }

        public void SetCullingMask(params string[] layerNames)
        {
            int mask = 0;

            foreach (var layerName in layerNames)
            {
                int layerIndex = LayerMask.NameToLayer(layerName);
                if (layerIndex >= 0)
                {
                    mask |= 1 << layerIndex;
                }
            }

            RenderCamera.cullingMask = mask;
        }
        #endregion

        private void CloseAll(PresenterParams[] paramList, bool forceClose = false)
        {
            bool isCloseExcuted = false;

            foreach (var param in paramList)
            {
                if (Close(param.Presenter, forceClose))
                {
                    isCloseExcuted = true;
                }
            }

            if (isCloseExcuted)
            {
                UpdateFocus();
            }
        }

        private void UpdateFocus()
        {
            IEnumerable<PresenterParams> paramList = RunningPresenterParams();
            PresenterParams mostFront = null;

            foreach (PresenterParams param in paramList)
            {
                if (mostFront == null)
                {
                    mostFront = param;
                    continue;
                }

                int frontLayerOrder = CanvasUtil.GetSortingLayerOrder(mostFront.Presenter.LayerName);
                int compareLayerOrder = CanvasUtil.GetSortingLayerOrder(param.Presenter.LayerName);
                if (frontLayerOrder < compareLayerOrder)
                {
                    mostFront = param;
                    continue;
                }
                else if (frontLayerOrder > compareLayerOrder)
                {
                    continue;
                }
                else if (mostFront.Presenter.SortingOrder <= param.Presenter.SortingOrder)
                {
                    mostFront = param;
                    continue;
                }
            }

            UpdateOutPanel(mostFront);

            _focused = mostFront;
        }

        private void UpdateSortingOrderAsLast(IView view)
        {
            if (view == null)
            {
                return;
            }

            System.Type viewType = view.GetType();
            IEnumerable<IView> sameViews = RunningPresenterParams()
                .Where(t => t.Presenter.ViewType == viewType)
                .Select(t => t.Presenter.View);

            if (sameViews.Any())
            {
                int maxSortingOrder = sameViews.Max(t => t.SortingOrder);
                // OutPanel에서 SortingOrder를 {view.SortingOrder - 1}로 설정하기 때문에
                // View 간의 SortingOrder 간격은 2로 설정한다.
                if (view.SortingOrder < maxSortingOrder + 2)
                {
                    view.SetSortingOrder(maxSortingOrder + 2);
                }
            }
        }

        private void UpdateOutPanel(PresenterParams param)
        {
            if (_outPanel == null)
            {
                OutPanel panel = CreateOutPanel(_rootTransform);

                _outPanel = panel;
                _outPanel.SetManager(this);
            }

            if (param == null || !param.OutPanelSettings.UseOutPanel)
            {
                _outPanel.gameObject.SetActive(false);
            }
            else
            {
                _outPanel.gameObject.SetActive(true);

                _outPanel.UpdateOutPanel(param.Presenter, param.OutPanelSettings);
            }
        }

        private void GetInterface(IPresenter presenter, out IPresenterInitialize initialize, out ILifecycle lifecycle)
        {
            initialize = presenter as IPresenterInitialize;
            lifecycle = presenter as ILifecycle;
        }

        private void GetInterface(IPresenter presenter, out ILifecycle lifecycle)
        {
            lifecycle = presenter as ILifecycle;
        }

        private IEnumerable<PresenterParams> RunningPresenterParams()
        {
            return Enumerable.Empty<PresenterParams>()
                .Concat(_presenterCloseChecker)
                .Concat(_openedPresenters)
                .Concat(_presenterOpenChecker)
                .Where(param => param.Presenter.IsViewExist);
        }

        #region Factory Methods
        private OutPanel CreateOutPanel(Transform parent = null)
        {
            GameObject go = new GameObject(
                "OutPanel",
                new System.Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(OutPanel) });
            go.transform.SetParent(parent);

            return go.GetComponent<OutPanel>();
        }

        private Canvas CreateLayerCanvas(string layerName)
        {
            GameObject go = new GameObject(
                $"Canvas_{layerName}",
                new System.Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) });
            go.transform.SetParent(_rootTransform);

            Canvas result = null;
            switch (_canvasSettings.RenderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                    result = CanvasUtil.GetOrAddScreenCameraCanvasComponent(
                        go,
                        _renderCamera,
                        _canvasSettings.PlaneDistance,
                        _canvasSettings.SortingLayerName,
                        _canvasSettings.SortingOrder);
                    break;
                case RenderMode.ScreenSpaceOverlay:
                    result = CanvasUtil.GetOrAddScreenOverlayCanvasComponent(
                        go,
                        _canvasSettings.SortingOrder);
                    break;
                case RenderMode.WorldSpace:
                    result = CanvasUtil.GetOrAddWorldCanvasComponent(
                        go,
                        _renderCamera,
                        _canvasSettings.SortingLayerName,
                        _canvasSettings.SortingOrder);
                    break;
            }

            switch (_canvasSettings.UIScaleMode)
            {
                case CanvasScaler.ScaleMode.ConstantPhysicalSize:
                    CanvasUtil.GetOrAddCanvasPhysicalSizeScalerComponent(
                        go,
                        _canvasSettings.PhysicalUnit,
                        _canvasSettings.FallbackScreenDPI,
                        _canvasSettings.SpriteDPI);
                    break;
                case CanvasScaler.ScaleMode.ConstantPixelSize:
                    CanvasUtil.GetOrAddCanvasPixelSizeScalerComponent(
                        go,
                        _canvasSettings.ScaleFactor);
                    break;
                case CanvasScaler.ScaleMode.ScaleWithScreenSize:
                    CanvasUtil.GetOrAddCanvasScaleSizeScalerComponent(
                        go,
                        _canvasSettings.Resolution,
                        _canvasSettings.ScreenMatchMode,
                        _canvasSettings.MatchWidthOrHeight);
                    break;
            }

            CanvasUtil.GetOrAddGraphicRaycasterComponent(go);

            result.sortingLayerName = layerName;

            return result;
        }

        private void PoolPresenter<TPresenter>(TPresenter presenter) where TPresenter : IPresenter
        {
            if (presenter == null || !presenter.IsViewExist)
            {
                return;
            }

            System.Type presenterType = presenter.GetType();
            if (!_presenterPool.TryGetValue(presenterType, out var pool))
            {
                pool = new Queue<IPresenter>();
                _presenterPool.Add(presenterType, pool);
            }

            pool.Enqueue(presenter);
        }

        private TPresenter CreatePresenter<TPresenter>() where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = null;

            System.Type presenterType = typeof(TPresenter);
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

        private TView CreateView<TView>(Canvas rootCanvas) where TView : View
        {
            System.Type viewType = typeof(TView);
            return CreateView(viewType, rootCanvas) as TView;
        }

        private View CreateView(System.Type viewType, Canvas rootCanvas)
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

            View inst = Object.Instantiate(resource, rootCanvas.transform);

            CanvasUtil.Overwrite(rootCanvas, inst.Canvas, true, true);
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

        public void RegisterViewResource(View viewResource)
        {
            if(viewResource == null)
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
                    canvas != null ? canvas.sortingLayerName : CanvasUtil.SortingLayers[0]);
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
    }
}
