using System.Collections.Generic;
using System.Linq;
using Mu3Library.DI;
using UnityEngine;
using UnityEngine.UI;

using IDisposable = System.IDisposable;

namespace Mu3Library.UI.MVP
{
    public partial class MVPManager : IMVPManager, IUpdatable, IDisposable
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

        private readonly Dictionary<System.Type, Queue<PresenterBase>> _presenterPool = new();

        private class PresenterParams
        {
            public PresenterBase Presenter;
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
            param.Presenter.Open();

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
            param.Presenter.Unload();

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
                .Where(t => t.Presenter.CanvasLayerName != "Default")
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

        public void CloseAll(IEnumerable<string> layerNames, bool forceClose = false)
        {
            if (layerNames == null)
            {
                return;
            }

            var layerNameSet = new HashSet<string>(layerNames);
            if (layerNameSet.Count == 0)
            {
                return;
            }

            var paramList = Enumerable.Empty<PresenterParams>()
                .Concat(_openedPresenters)
                .Concat(_presenterOpenChecker)
                .Where(t => layerNameSet.Contains(t.Presenter.CanvasLayerName))
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
                closeParam.Presenter.Close(forceClose);
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

            param.Presenter.Close(forceClose);
            _presenterCloseChecker.Add(param);

            return true;
        }

        public IPresenter Open<TPresenter>() where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, OutPanelSettings.Disabled);

        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(args, OutPanelSettings.Disabled);

        public IPresenter Open<TPresenter>(OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, settings);

        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new()
        {
            TPresenter presenter = CreatePresenter<TPresenter>();

            System.Type viewType = presenter.ViewType;
            string viewLayerName = GetLayerName(viewType);

            if (!_layerCanvases.TryGetValue(viewLayerName, out Canvas layerCanvas))
            {
                layerCanvas = CreateLayerCanvas(viewLayerName, MVPCanvasSettings.Standard);

                _layerCanvases.Add(viewLayerName, layerCanvas);
            }

            if (!presenter.IsViewExist)
            {
                View view = CreateView(viewType, layerCanvas);
                if (view == null)
                {
                    return null;
                }

                presenter.Initialize(view, args);
            }
            else
            {
                presenter.Initialize(args);
            }

            PresenterParams presenterParams = new PresenterParams()
            {
                Presenter = presenter,
                OutPanelSettings = settings,
            };

            UpdateSortingOrderAsLast(presenterParams);
            presenter.OptimizeView();
            presenter.SetActiveView(true);

            presenter.Load();

            _presenterLoadChecker.Add(presenterParams);

            return presenter;
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

                int frontLayerOrder = MVPCanvasUtil.GetSortingLayerOrder(mostFront.Presenter.CanvasLayerName);
                int compareLayerOrder = MVPCanvasUtil.GetSortingLayerOrder(param.Presenter.CanvasLayerName);
                if (frontLayerOrder < compareLayerOrder)
                {
                    mostFront = param;
                    continue;
                }
                else if (frontLayerOrder > compareLayerOrder)
                {
                    continue;
                }
                else
                {
                    if (mostFront.Presenter.SortingOrder <= param.Presenter.SortingOrder)
                    {
                        mostFront = param;
                        continue;
                    }
                }
            }

            UpdateOutPanel(mostFront);

            _focused = mostFront;
        }

        private void UpdateSortingOrderAsLast(PresenterParams presenterParam)
        {
            if (presenterParam == null || presenterParam.Presenter == null || !presenterParam.Presenter.IsViewExist)
            {
                return;
            }

            System.Type viewType = presenterParam.Presenter.ViewType;
            IEnumerable<int> sameViewSortingOrders = RunningPresenterParams()
                .Where(t => t.Presenter.ViewType == viewType)
                .Select(t => t.Presenter.SortingOrder);

            if (sameViewSortingOrders.Any())
            {
                int maxSortingOrder = sameViewSortingOrders.Max();
                // OutPanel에서 SortingOrder를 {view.SortingOrder - 1}로 설정하기 때문에
                // View 간의 SortingOrder 간격은 2로 설정한다.
                if (presenterParam.Presenter.SortingOrder < maxSortingOrder + 2)
                {
                    presenterParam.Presenter.SetSortingOrder(maxSortingOrder + 2);
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

                _outPanel.ObjectLayerName = param.Presenter.ObjectLayerName;
                _outPanel.UpdateOutPanel(param.Presenter, param.OutPanelSettings);
            }
        }

        private IEnumerable<PresenterParams> RunningPresenterParams()
        {
            return Enumerable.Empty<PresenterParams>()
                .Concat(_presenterCloseChecker)
                .Concat(_openedPresenters)
                .Concat(_presenterOpenChecker)
                .Where(param => param.Presenter.IsViewExist);
        }

        private OutPanel CreateOutPanel(Transform parent = null)
        {
            GameObject go = new GameObject(
                "OutPanel",
                new System.Type[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(OutPanel) });
            go.transform.SetParent(parent);

            return go.GetComponent<OutPanel>();
        }

        private void PoolPresenter(PresenterBase presenter)
        {
            if (presenter == null || !presenter.IsViewExist)
            {
                return;
            }

            System.Type presenterType = presenter.GetType();
            if (!_presenterPool.TryGetValue(presenterType, out var pool))
            {
                pool = new Queue<PresenterBase>();
                _presenterPool.Add(presenterType, pool);
            }

            pool.Enqueue(presenter);
        }

        private TPresenter CreatePresenter<TPresenter>() where TPresenter : PresenterBase, new()
        {
            TPresenter presenter = null;

            System.Type presenterType = typeof(TPresenter);
            if (_presenterPool.TryGetValue(presenterType, out Queue<PresenterBase> pool) && pool.Count > 0)
            {
                PresenterBase inst = null;

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

    }
}
