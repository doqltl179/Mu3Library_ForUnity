using System.Collections.Generic;
using System.Linq;
using Mu3Library.DI;
using Mu3Library.Event;
using Mu3Library.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using IDisposable = System.IDisposable;

#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem.UI;
#endif

namespace Mu3Library.UI.MVP
{
    /// <summary>
    /// Manages the lifecycle of MVP (Model-View-Presenter) UI components.
    /// Handles View loading, opening, closing, unloading, and layer management.
    /// </summary>
    public partial class MVPManager : IMVPManager, IMVPManagerEventBus, IUpdatable, IDisposable
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
        public IEnumerable<string> SortingLayers => _sortingLayers;

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

        private EventSystem m_eventSystem = null;
        private EventSystem _eventSystem
        {
            get
            {
                if (m_eventSystem == null)
                {
                    m_eventSystem = EventSystem.current;
                    if (m_eventSystem == null)
                    {
                        List<System.Type> components = new List<System.Type>();
                        components.Add(typeof(EventSystem));
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
                        components.Add(typeof(InputSystemUIInputModule));
#else
                        components.Add(typeof(StandaloneInputModule));
#endif
                        GameObject go = new GameObject("EventSystem", components.ToArray());
                        m_eventSystem = go.GetComponent<EventSystem>();

                        Object.DontDestroyOnLoad(go);
                    }
                }

                return m_eventSystem;
            }
        }
        public EventSystem EventSystem => _eventSystem;

        private readonly Dictionary<System.Type, Queue<PresenterBase>> _presenterPool = new();

        private sealed class PresenterEntry
        {
            public PresenterBase Presenter;
            public OutPanelSettings OutPanelSettings;

            public PresenterEntry Owner;
            public readonly List<PresenterEntry> OwnedChildren = new();

            public void AddOwnedChild(PresenterEntry child)
            {
                child.DetachFromOwner();
                child.Owner = this;
                OwnedChildren.Add(child);
            }

            public void DetachFromOwner()
            {
                Owner?.OwnedChildren.Remove(this);
                Owner = null;
            }
        }

        private readonly List<PresenterEntry> _openedPresenters = new();

        private readonly List<PresenterEntry> _presenterLoadChecker = new();
        private readonly List<PresenterEntry> _presenterOpenChecker = new();
        private readonly List<PresenterEntry> _presenterCloseChecker = new();
        private readonly List<PresenterEntry> _presenterUnloadChecker = new();

        private PresenterEntry _focused = null;

        private readonly HashSet<string> _focusIgnoredLayers = new();
        public IReadOnlyCollection<string> FocusIgnoredLayers => _focusIgnoredLayers;

        private OutPanel _outPanel = null;
        private readonly SubscribeHandler _subscribeHandler = new();

        public event System.Action<IPresenter> OnWindowLoaded;
        public event System.Action<IPresenter> OnWindowOpened;
        public event System.Action<IPresenter> OnWindowClosed;
        public event System.Action<IPresenter> OnWindowUnloaded;



        public void Dispose()
        {
            _subscribeHandler.Dispose();

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
            _focusIgnoredLayers.Clear();

            if (m_root != null)
            {
                Object.Destroy(m_root);
            }
        }

        public void Update()
        {
            CleanupDestroyedPresenters();

            CheckLifecycle(_presenterLoadChecker, ViewState.Loaded, WindowLoadedEvent);
            CheckLifecycle(_presenterOpenChecker, ViewState.Opened, WindowOpenedEvent);
            CheckLifecycle(_presenterCloseChecker, ViewState.Closed, WindowClosedEvent);
            CheckLifecycle(_presenterUnloadChecker, ViewState.Unloaded, WindowUnloadedEvent);
        }

        public uint SubscribeOnWindowLoadedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowLoadedOnce(callback, null);

        public uint SubscribeOnWindowLoadedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowLoaded += handler,
                handler => OnWindowLoaded -= handler,
                callback,
                onDisposed);

        public uint SubscribeOnWindowOpenedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowOpenedOnce(callback, null);

        public uint SubscribeOnWindowOpenedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowOpened += handler,
                handler => OnWindowOpened -= handler,
                callback,
                onDisposed);

        public uint SubscribeOnWindowClosedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowClosedOnce(callback, null);

        public uint SubscribeOnWindowClosedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowClosed += handler,
                handler => OnWindowClosed -= handler,
                callback,
                onDisposed);

        public uint SubscribeOnWindowUnloadedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowUnloadedOnce(callback, null);

        public uint SubscribeOnWindowUnloadedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowUnloaded += handler,
                handler => OnWindowUnloaded -= handler,
                callback,
                onDisposed);

        private void CheckLifecycle(List<PresenterEntry> list, ViewState checkState, System.Action<PresenterEntry> callback = null)
        {
            bool isViewDestroyed = false;

            for (int i = 0; i < list.Count; i++)
            {
                PresenterEntry param = list[i];
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

        private void WindowLoadedEvent(PresenterEntry param)
        {
            param.Presenter.SetActiveView(true);
            param.Presenter.Open();

            _presenterOpenChecker.Add(param);

            UpdateFocus();

            OnWindowLoaded?.Invoke(param.Presenter);
        }

        private void WindowOpenedEvent(PresenterEntry param)
        {
            _openedPresenters.Add(param);

            UpdateFocus();

            OnWindowOpened?.Invoke(param.Presenter);
        }

        private void WindowClosedEvent(PresenterEntry param)
        {
            param.Presenter.Unload();

            _presenterUnloadChecker.Add(param);

            UpdateFocus();

            OnWindowClosed?.Invoke(param.Presenter);
        }

        private void WindowUnloadedEvent(PresenterEntry param)
        {
            param.Presenter.SetActiveView(false);
            PoolPresenter(param.Presenter);

            UpdateFocus();

            OnWindowUnloaded?.Invoke(param.Presenter);
        }

        #region Utility
        public void CloseAllWithoutDefault(bool forceClose = false)
        {
            CleanupDestroyedPresenters();

            // Pre-allocate list with capacity estimate to reduce allocations
            List<PresenterEntry> paramList = new List<PresenterEntry>(_openedPresenters.Count + _presenterOpenChecker.Count);

            for (int i = 0; i < _openedPresenters.Count; i++)
            {
                if (_openedPresenters[i].Presenter.CanvasLayerName != "Default")
                {
                    paramList.Add(_openedPresenters[i]);
                }
            }

            for (int i = 0; i < _presenterOpenChecker.Count; i++)
            {
                if (_presenterOpenChecker[i].Presenter.CanvasLayerName != "Default")
                {
                    paramList.Add(_presenterOpenChecker[i]);
                }
            }

            CloseAll(paramList, forceClose);
        }

        public void CloseAll(bool forceClose = false)
        {
            CleanupDestroyedPresenters();

            // Pre-allocate list with exact capacity to avoid allocations
            List<PresenterEntry> paramList = new List<PresenterEntry>(_openedPresenters.Count + _presenterOpenChecker.Count);
            paramList.AddRange(_openedPresenters);
            paramList.AddRange(_presenterOpenChecker);

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

            CleanupDestroyedPresenters();

            // Pre-allocate list with capacity estimate
            List<PresenterEntry> paramList = new List<PresenterEntry>(_openedPresenters.Count + _presenterOpenChecker.Count);

            for (int i = 0; i < _openedPresenters.Count; i++)
            {
                if (layerNameSet.Contains(_openedPresenters[i].Presenter.CanvasLayerName))
                {
                    paramList.Add(_openedPresenters[i]);
                }
            }

            for (int i = 0; i < _presenterOpenChecker.Count; i++)
            {
                if (layerNameSet.Contains(_presenterOpenChecker[i].Presenter.CanvasLayerName))
                {
                    paramList.Add(_presenterOpenChecker[i]);
                }
            }

            CloseAll(paramList, forceClose);
        }

        public void CloseFocused(bool forceClose = false)
        {
            if (_focused == null || _focused.Presenter == null)
            {
                return;
            }

            PresenterEntry closeParam = _focused;
            _focused = null;

            if (!ClosePresenterWithOwnedChildren(closeParam, forceClose))
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

            CleanupDestroyedPresenters();

            PresenterEntry param = FindPresenterEntry(presenter as PresenterBase);
            if (param == null)
            {
                return false;
            }

            return ClosePresenterWithOwnedChildren(param, forceClose);
        }

        public IPresenter Open<TPresenter>() where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, null, OutPanelSettings.Disabled, null);

        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, args, OutPanelSettings.Disabled, null);

        public IPresenter Open<TPresenter>(OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, null, settings, null);

        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, args, settings, null);

        public IPresenter Open<TPresenter>(HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, null, OutPanelSettings.Disabled, hostOptions);

        public IPresenter Open<TPresenter>(Arguments args, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, args, OutPanelSettings.Disabled, hostOptions);

        public IPresenter Open<TPresenter>(OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, null, settings, hostOptions);

        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(null, args, settings, hostOptions);

        public IPresenter Open<TPresenter>(IPresenter owner) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, null, OutPanelSettings.Disabled, null);

        public IPresenter Open<TPresenter>(IPresenter owner, Arguments args) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, args, OutPanelSettings.Disabled, null);

        public IPresenter Open<TPresenter>(IPresenter owner, OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, null, settings, null);

        public IPresenter Open<TPresenter>(IPresenter owner, Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, args, settings, null);

        public IPresenter Open<TPresenter>(IPresenter owner, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, null, OutPanelSettings.Disabled, hostOptions);

        public IPresenter Open<TPresenter>(IPresenter owner, Arguments args, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, args, OutPanelSettings.Disabled, hostOptions);

        public IPresenter Open<TPresenter>(IPresenter owner, OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new()
            => Open<TPresenter>(owner, null, settings, hostOptions);

        public IPresenter Open<TPresenter>(IPresenter owner, Arguments args, OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new()
        {
            PresenterEntry ownerEntry = ResolveOwnerEntry(owner);

            TPresenter presenter = CreatePresenter<TPresenter>();

            System.Type viewType = presenter.ViewType;
            string viewLayerName = GetLayerName(viewType);
            if (string.IsNullOrEmpty(viewLayerName))
            {
                Debug.LogWarning($"View layer name is empty. type: {viewType}");
            }

            if (!_layerCanvases.TryGetValue(viewLayerName, out Canvas layerCanvas))
            {
                var layerCanvasSettings = MVPCanvasSettings.Standard;

                var canvasSettings = layerCanvasSettings.CanvasSettings;
                canvasSettings.SortingLayerName = viewLayerName;
                layerCanvasSettings.CanvasSettings = canvasSettings;

                layerCanvas = CreateLayerCanvas(layerCanvasSettings);

                _layerCanvases.Add(viewLayerName, layerCanvas);
            }

            TryGetViewResource(viewType, out View viewResource);
            RectTransform ownerHost = ownerEntry?.Presenter.RectTransform;

            if (!presenter.IsViewExist)
            {
                if (viewResource == null)
                {
                    return null;
                }

                View view = CreateView(viewResource, layerCanvas);
                if (view == null)
                {
                    return null;
                }

                PrepareViewForOpen(viewResource, view.Canvas, view.RectTransform, layerCanvas, ownerHost, hostOptions);
                presenter.Initialize(view, args);
            }
            else
            {
                PrepareViewForOpen(viewResource, presenter.ViewCanvas, presenter.RectTransform, layerCanvas, ownerHost, hostOptions);
                presenter.Initialize(args);
            }

            PresenterEntry presenterEntry = new PresenterEntry()
            {
                Presenter = presenter,
                OutPanelSettings = settings,
            };

            ownerEntry?.AddOwnedChild(presenterEntry);

            UpdateSortingOrderAsLast(presenterEntry);
            presenter.OptimizeView();
            presenter.SetActiveView(false);

            presenter.Load();

            _presenterLoadChecker.Add(presenterEntry);

            return presenter;
        }

        public void SetFocusIgnoredLayer(string layerName, bool ignored)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return;
            }

            if (ignored)
            {
                _focusIgnoredLayers.Add(layerName);
            }
            else
            {
                _focusIgnoredLayers.Remove(layerName);
            }

            UpdateFocus();
        }

        public void ClearEventSystem()
        {
            if (_eventSystem == null)
            {
                Debug.LogError("EventSystem cannot be null. The system is designed to ensure at least one EventSystem exists.");
                return;
            }

            var eventSystems = MonoBehaviour.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var es in eventSystems)
            {
                if (es == _eventSystem)
                {
                    continue;
                }

                Object.Destroy(es.gameObject);
            }
        }
        #endregion

        private PresenterEntry FindPresenterEntry(PresenterBase presenter)
        {
            if (presenter == null)
            {
                return null;
            }

            for (int i = 0; i < _openedPresenters.Count; i++)
            {
                if (_openedPresenters[i].Presenter == presenter)
                {
                    return _openedPresenters[i];
                }
            }

            for (int i = 0; i < _presenterOpenChecker.Count; i++)
            {
                if (_presenterOpenChecker[i].Presenter == presenter)
                {
                    return _presenterOpenChecker[i];
                }
            }

            for (int i = 0; i < _presenterLoadChecker.Count; i++)
            {
                if (_presenterLoadChecker[i].Presenter == presenter)
                {
                    return _presenterLoadChecker[i];
                }
            }

            return null;
        }

        private PresenterEntry ResolveOwnerEntry(IPresenter owner)
        {
            if (owner == null)
            {
                return null;
            }

            PresenterEntry ownerEntry = FindPresenterEntry(owner as PresenterBase);
            if (ownerEntry == null)
            {
                Debug.LogWarning($"Owner presenter not found or not active. Opening without owner. type: {owner.GetType()}");
            }

            return ownerEntry;
        }

        private static Transform ResolveHostTransform(RectTransform ownerHost, HostOptions hostOptions, Transform layerRoot)
        {
            if (hostOptions != null && hostOptions.Host != null)
            {
                return hostOptions.Host;
            }

            return ownerHost != null ? ownerHost.transform : layerRoot;
        }

        private static void RestoreViewRootLayout(View viewResource, RectTransform targetRectTransform)
        {
            if (viewResource == null || targetRectTransform == null)
            {
                return;
            }

            RectTransform sourceRectTransform = viewResource.RectTransform;
            if (sourceRectTransform == null)
            {
                return;
            }

            targetRectTransform.anchorMin = sourceRectTransform.anchorMin;
            targetRectTransform.anchorMax = sourceRectTransform.anchorMax;
            targetRectTransform.anchoredPosition3D = sourceRectTransform.anchoredPosition3D;
            targetRectTransform.sizeDelta = sourceRectTransform.sizeDelta;
            targetRectTransform.pivot = sourceRectTransform.pivot;
            targetRectTransform.localRotation = sourceRectTransform.localRotation;
            targetRectTransform.localScale = sourceRectTransform.localScale;
        }

        private static void PrepareViewForOpen(
            View viewResource,
            Canvas viewCanvas,
            RectTransform viewRectTransform,
            Canvas layerCanvas,
            RectTransform ownerHost,
            HostOptions hostOptions)
        {
            if (viewCanvas == null || viewRectTransform == null || layerCanvas == null)
            {
                return;
            }

            layerCanvas.CopyTo(viewCanvas, true, true);
            viewCanvas.overrideSorting = true;

            Transform hostTransform = ResolveHostTransform(ownerHost, hostOptions, layerCanvas.transform);
            viewRectTransform.SetParent(hostTransform, false);

            if (viewResource != null)
            {
                RestoreViewRootLayout(viewResource, viewRectTransform);
                viewCanvas.sortingOrder = viewResource.SortingOrder;
            }

            hostOptions?.ApplyLayout?.Invoke(viewRectTransform);
        }

        /// <summary>
        /// Closes a presenter and all its chained children depth-first (deepest child first).
        /// Cascade children are always force-closed to interrupt any ongoing open animation.
        /// </summary>
        private bool ClosePresenterWithOwnedChildren(PresenterEntry param, bool forceClose)
        {
            if (param == null)
            {
                return false;
            }

            // Close children depth-first. Copy to avoid mutation during iteration.
            if (param.OwnedChildren.Count > 0)
            {
                List<PresenterEntry> children = new List<PresenterEntry>(param.OwnedChildren);
                foreach (PresenterEntry child in children)
                {
                    ClosePresenterWithOwnedChildren(child, forceClose: true);
                }
            }

            bool removed = _openedPresenters.Remove(param);
            if (!removed && forceClose)
            {
                removed = _presenterOpenChecker.Remove(param);
            }

            if (!removed)
            {
                return false;
            }

            param.DetachFromOwner();
            param.Presenter.Close(forceClose);
            _presenterCloseChecker.Add(param);

            return true;
        }

        private void CloseAll(List<PresenterEntry> paramList, bool forceClose = false)
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
            IEnumerable<PresenterEntry> paramList = RunningPresenterEntries();
            PresenterEntry mostFront = null;

            foreach (PresenterEntry param in paramList)
            {
                if (_focusIgnoredLayers.Contains(param.Presenter.CanvasLayerName))
                {
                    continue;
                }

                if (mostFront == null)
                {
                    mostFront = param;
                    continue;
                }

                int frontLayerOrder = GetSortingLayerOrder(mostFront.Presenter.CanvasLayerName);
                int compareLayerOrder = GetSortingLayerOrder(param.Presenter.CanvasLayerName);
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
            UpdateEventSystem();

            _focused = mostFront;
        }

        private void CleanupDestroyedPresenters()
        {
            bool removed = false;

            removed |= RemoveDestroyedPresenters(_openedPresenters);
            removed |= RemoveDestroyedPresenters(_presenterLoadChecker);
            removed |= RemoveDestroyedPresenters(_presenterOpenChecker);
            removed |= RemoveDestroyedPresenters(_presenterCloseChecker);
            removed |= RemoveDestroyedPresenters(_presenterUnloadChecker);

            if (removed)
            {
                UpdateFocus();
            }
        }

        private static bool RemoveDestroyedPresenters(List<PresenterEntry> list)
        {
            bool removed = false;

            for (int i = 0; i < list.Count; i++)
            {
                PresenterEntry param = list[i];
                if (param?.Presenter != null && param.Presenter.IsViewExist)
                {
                    continue;
                }

                list.RemoveAt(i);
                i--;

                removed = true;
            }

            return removed;
        }

        private void UpdateSortingOrderAsLast(PresenterEntry presenterParam)
        {
            if (presenterParam == null || presenterParam.Presenter == null || !presenterParam.Presenter.IsViewExist)
            {
                return;
            }

            System.Type viewType = presenterParam.Presenter.ViewType;
            IEnumerable<int> sameViewSortingOrders = RunningPresenterEntries()
                .Where(t => t.Presenter.ViewType == viewType)
                .Select(t => t.Presenter.SortingOrder);

            if (sameViewSortingOrders.Any())
            {
                int maxSortingOrder = sameViewSortingOrders.Max();
                // OutPanel uses sorting order {view.SortingOrder - 1}, so keep view gap at 2.
                if (presenterParam.Presenter.SortingOrder < maxSortingOrder + 2)
                {
                    presenterParam.Presenter.SetSortingOrder(maxSortingOrder + 2);
                }
            }
        }

        private void UpdateEventSystem()
        {
            // 변수 호출만으로 EventSystem이 존재하도록 보장
            if (_eventSystem == null)
            {
                Debug.LogError("EventSystem cannot be null. The system is designed to ensure at least one EventSystem exists.");
                return;
            }
        }

        private void UpdateOutPanel(PresenterEntry param)
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

        private IEnumerable<PresenterEntry> RunningPresenterEntries()
        {
            return Enumerable.Empty<PresenterEntry>()
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

            presenter.Context(this);

            return presenter;
        }

        private static int GetSortingLayerOrder(string layerName)
        {
            if (_sortingLayerOrderMap.TryGetValue(layerName, out int order))
            {
                return order;
            }

            return -1;
        }
    }
}
