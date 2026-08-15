using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Foundation.Event;
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
        [Inject]
        private IObjectInjector _objectInjector = null;

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
                    SortingLayer[] layers = SortingLayer.layers;
                    string[] sortingLayers = new string[layers.Length];
                    for (int i = 0; i < layers.Length; i++)
                    {
                        sortingLayers[i] = layers[i].name;
                    }

                    m_sortingLayers = sortingLayers;
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
        private bool _isEventSystemOwned = false;
        private EventSystem _eventSystem
        {
            get
            {
                if (m_eventSystem == null)
                {
                    _isEventSystemOwned = false;

                    m_eventSystem = EventSystem.current;
                    if (m_eventSystem == null)
                    {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
                        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
                        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
                        m_eventSystem = go.GetComponent<EventSystem>();

                        // The manager created it, so it is also the one that destroys it. It lives
                        // outside the manager root because it has to survive a scene change.
                        _isEventSystemOwned = true;

                        Object.DontDestroyOnLoad(go);
                    }
                }

                return m_eventSystem;
            }
        }
        public EventSystem EventSystem => _eventSystem;

        private readonly Dictionary<System.Type, Queue<PresenterBase>> _presenterPool = new();

        /// <summary>
        /// Where a presenter stands in the manager lifecycle. Every phase except <see cref="PresenterPhase.None"/>
        /// and <see cref="PresenterPhase.Released"/> owns exactly one state list, so an entry is reachable through
        /// the registry for its whole life instead of only while it sits in one of those lists.
        /// </summary>
        private enum PresenterPhase
        {
            None = 0,

            Loading = 1,
            Opening = 2,
            Opened = 3,
            Closing = 4,
            Unloading = 5,
            Released = 6,
        }

        private sealed class PresenterEntry
        {
            public PresenterBase Presenter;
            public OutPanelSettings OutPanelSettings;

            public PresenterPhase Phase = PresenterPhase.None;

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

            public void DetachOwnedChildren()
            {
                for (int i = 0; i < OwnedChildren.Count; i++)
                {
                    OwnedChildren[i].Owner = null;
                }

                OwnedChildren.Clear();
            }
        }

        /// <summary>
        /// Every managed presenter, from the moment it is opened until it is pooled or its view is destroyed.
        /// Owner lookup goes through this map, so a presenter stays resolvable while its own lifecycle callback
        /// is running and the state lists below are free to be pure per-state queues.
        /// </summary>
        private readonly Dictionary<PresenterBase, PresenterEntry> _presenterEntries = new();

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
            _presenterEntries.Clear();
            _openedPresenters.Clear();
            _presenterLoadChecker.Clear();
            _presenterOpenChecker.Clear();
            _presenterCloseChecker.Clear();
            _presenterUnloadChecker.Clear();

            _focused = null;
            _focusIgnoredLayers.Clear();
            _outPanel = null;

            DisposeRenderCamera();
            DestroyOwnedEventSystem();

            if (m_root != null)
            {
                Object.Destroy(m_root);
                m_root = null;
            }

            OnWindowLoaded = null;
            OnWindowOpened = null;
            OnWindowClosed = null;
            OnWindowUnloaded = null;
        }

        private void DestroyOwnedEventSystem()
        {
            EventSystem eventSystem = m_eventSystem;
            bool isOwned = _isEventSystemOwned;

            m_eventSystem = null;
            _isEventSystemOwned = false;

            // An EventSystem that already lived in the scene belongs to the project and is left
            // alone; only the one this manager created is destroyed with it.
            if (!isOwned || eventSystem == null)
            {
                return;
            }

            Object.Destroy(eventSystem.gameObject);
        }

        public void Update()
        {
            CleanupDestroyedPresenters();

            CheckLifecycle(_presenterLoadChecker, ViewState.Loaded, WindowLoadedEvent);
            CheckLifecycle(_presenterOpenChecker, ViewState.Opened, WindowOpenedEvent);
            CheckLifecycle(_presenterCloseChecker, ViewState.Closed, WindowClosedEvent);
            CheckLifecycle(_presenterUnloadChecker, ViewState.Unloaded, WindowUnloadedEvent);
        }

        public ISubscriptionInfo SubscribeOnWindowLoadedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowLoadedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnWindowLoadedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowLoaded += handler,
                handler => OnWindowLoaded -= handler,
                callback,
                onDisposed);

        public ISubscriptionInfo SubscribeOnWindowOpenedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowOpenedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnWindowOpenedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowOpened += handler,
                handler => OnWindowOpened -= handler,
                callback,
                onDisposed);

        public ISubscriptionInfo SubscribeOnWindowClosedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowClosedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnWindowClosedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnWindowClosed += handler,
                handler => OnWindowClosed -= handler,
                callback,
                onDisposed);

        public ISubscriptionInfo SubscribeOnWindowUnloadedOnce(System.Action<IPresenter> callback)
            => SubscribeOnWindowUnloadedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnWindowUnloadedOnce(System.Action<IPresenter> callback, System.Action onDisposed)
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

                    ReleaseEntry(param);

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

            // The entry enters its phase before OpenFunc() runs, so a presenter that opens a child
            // from OpenFunc() is already resolvable as that child's owner.
            EnterPhase(param, PresenterPhase.Opening);

            param.Presenter.Open();

            UpdateFocus();

            OnWindowLoaded?.Invoke(param.Presenter);
        }

        private void WindowOpenedEvent(PresenterEntry param)
        {
            EnterPhase(param, PresenterPhase.Opened);

            UpdateFocus();

            OnWindowOpened?.Invoke(param.Presenter);
        }

        private void WindowClosedEvent(PresenterEntry param)
        {
            EnterPhase(param, PresenterPhase.Unloading);

            param.Presenter.Unload();

            UpdateFocus();

            OnWindowClosed?.Invoke(param.Presenter);
        }

        private void WindowUnloadedEvent(PresenterEntry param)
        {
            param.Presenter.SetActiveView(false);

            PresenterBase presenter = param.Presenter;

            ReleaseEntry(param);
            PoolPresenter(presenter);

            UpdateFocus();

            OnWindowUnloaded?.Invoke(presenter);
        }

        #region Utility
        public void CloseAllWithoutDefault(bool forceClose = false)
        {
            CleanupDestroyedPresenters();

            CloseAll(CollectCloseCandidates(entry => entry.Presenter.CanvasLayerName != "Default"), forceClose);
        }

        public void CloseAll(bool forceClose = false)
        {
            CleanupDestroyedPresenters();

            CloseAll(CollectCloseCandidates(null), forceClose);
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

            CloseAll(CollectCloseCandidates(entry => layerNameSet.Contains(entry.Presenter.CanvasLayerName)), forceClose);
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

            RegisterEntry(presenterEntry);
            ownerEntry?.AddOwnedChild(presenterEntry);

            UpdateSortingOrderAsLast(presenterEntry);
            presenter.OptimizeView();
            presenter.SetActiveView(false);

            // The entry enters its phase before LoadFunc() runs, so a presenter that opens a child
            // from LoadFunc() is already resolvable as that child's owner.
            EnterPhase(presenterEntry, PresenterPhase.Loading);

            presenter.Load();

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

            return _presenterEntries.TryGetValue(presenter, out PresenterEntry entry) ? entry : null;
        }

        private List<PresenterEntry> PhaseList(PresenterPhase phase)
        {
            switch (phase)
            {
                case PresenterPhase.Loading: return _presenterLoadChecker;
                case PresenterPhase.Opening: return _presenterOpenChecker;
                case PresenterPhase.Opened: return _openedPresenters;
                case PresenterPhase.Closing: return _presenterCloseChecker;
                case PresenterPhase.Unloading: return _presenterUnloadChecker;
                default: return null;
            }
        }

        /// <summary>
        /// Moves an entry to its next phase and its matching state list. Callers run this before invoking the
        /// presenter callback that belongs to the phase, so code running inside that callback sees a consistent
        /// manager state. Removing from the previous list is a no-op when <see cref="CheckLifecycle"/> already
        /// took the entry out while iterating.
        /// </summary>
        private void EnterPhase(PresenterEntry entry, PresenterPhase phase)
        {
            if (entry == null)
            {
                return;
            }

            PhaseList(entry.Phase)?.Remove(entry);

            entry.Phase = phase;

            PhaseList(phase)?.Add(entry);
        }

        private void RegisterEntry(PresenterEntry entry)
        {
            if (entry?.Presenter == null)
            {
                return;
            }

            if (_presenterEntries.TryGetValue(entry.Presenter, out PresenterEntry stale))
            {
                Debug.LogWarning($"Presenter is already registered. The stale entry is discarded. type: {entry.Presenter.GetType()}");
                ReleaseEntry(stale);
            }

            _presenterEntries[entry.Presenter] = entry;
        }

        /// <summary>
        /// Drops an entry out of the manager: its state list, the registry, the owner chain, and the focus slot.
        /// The presenter itself is left alone so the caller can decide whether it is poolable.
        /// </summary>
        private void ReleaseEntry(PresenterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            PhaseList(entry.Phase)?.Remove(entry);
            entry.Phase = PresenterPhase.Released;

            entry.DetachFromOwner();
            entry.DetachOwnedChildren();

            if (entry.Presenter != null)
            {
                _presenterEntries.Remove(entry.Presenter);
            }

            if (_focused == entry)
            {
                _focused = null;
            }
        }

        /// <summary>
        /// A presenter can take children while it is on its way in or already open. Once it starts closing it
        /// must not gain new ones, because the cascade close has already walked its child list.
        /// </summary>
        private static bool CanOwnChild(PresenterPhase phase)
            => phase == PresenterPhase.Loading || phase == PresenterPhase.Opening || phase == PresenterPhase.Opened;

        /// <summary>
        /// An open presenter closes on request. One that is still on its way in is interrupted only by a force
        /// close, which is what the cascade path uses for every child.
        /// </summary>
        private static bool CanClose(PresenterPhase phase, bool forceClose)
        {
            if (phase == PresenterPhase.Opened)
            {
                return true;
            }

            return forceClose && (phase == PresenterPhase.Loading || phase == PresenterPhase.Opening);
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
                Debug.LogWarning($"Owner presenter is not managed by this manager. Opening without owner. type: {owner.GetType()}");
                return null;
            }

            if (!CanOwnChild(ownerEntry.Phase))
            {
                Debug.LogWarning($"Owner presenter is already closing. Opening without owner. type: {owner.GetType()}, phase: {ownerEntry.Phase}");
                return null;
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
        /// Cascade children are always force-closed to interrupt any ongoing open animation, which reaches a
        /// child that is still loading as well. The presenter itself is checked before the cascade runs, so a
        /// presenter that cannot close leaves its children alone.
        /// </summary>
        private bool ClosePresenterWithOwnedChildren(PresenterEntry param, bool forceClose)
        {
            if (param == null || !CanClose(param.Phase, forceClose))
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

            // A presenter that is still loading has never been shown, so its view object is inactive and
            // cannot run the close coroutine. Activate it first to keep the close/unload pipeline intact.
            if (param.Phase == PresenterPhase.Loading)
            {
                param.Presenter.SetActiveView(true);
            }

            param.DetachFromOwner();

            EnterPhase(param, PresenterPhase.Closing);

            param.Presenter.Close(forceClose);

            return true;
        }

        /// <summary>
        /// Gathers what a close-all pass should try: the opened windows first, then the ones still on their way
        /// in, so an owner is reached before the children it cascades to. A presenter that is only loading is
        /// offered as well, otherwise a window opened in the same frame outlives the pass. <see cref="CanClose"/>
        /// makes the final call and refuses a presenter that is not open yet unless the close is forced.
        /// Returns null when there is nothing to try.
        /// </summary>
        private List<PresenterEntry> CollectCloseCandidates(System.Func<PresenterEntry, bool> filter)
        {
            int capacity = _openedPresenters.Count + _presenterOpenChecker.Count + _presenterLoadChecker.Count;
            if (capacity == 0)
            {
                return null;
            }

            List<PresenterEntry> candidates = new List<PresenterEntry>(capacity);

            AddCloseCandidates(candidates, _openedPresenters, filter);
            AddCloseCandidates(candidates, _presenterOpenChecker, filter);
            AddCloseCandidates(candidates, _presenterLoadChecker, filter);

            return candidates;
        }

        private static void AddCloseCandidates(
            List<PresenterEntry> candidates,
            List<PresenterEntry> source,
            System.Func<PresenterEntry, bool> filter)
        {
            for (int i = 0; i < source.Count; i++)
            {
                PresenterEntry entry = source[i];
                if (filter == null || filter(entry))
                {
                    candidates.Add(entry);
                }
            }
        }

        private void CloseAll(List<PresenterEntry> paramList, bool forceClose = false)
        {
            if (paramList == null)
            {
                return;
            }

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
            IEnumerable<PresenterEntry> paramList = FocusCandidateEntries();
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
            if (_presenterEntries.Count == 0)
            {
                return;
            }

            // One registry sweep covers every state list. The destroyed set is only allocated when
            // something actually went missing, so the common per-frame path stays allocation free.
            List<PresenterEntry> destroyed = null;

            foreach (PresenterEntry entry in _presenterEntries.Values)
            {
                if (entry?.Presenter != null && entry.Presenter.IsViewExist)
                {
                    continue;
                }

                if (destroyed == null)
                {
                    destroyed = new List<PresenterEntry>();
                }

                destroyed.Add(entry);
            }

            if (destroyed == null)
            {
                return;
            }

            foreach (PresenterEntry entry in destroyed)
            {
                ReleaseEntry(entry);
            }

            UpdateFocus();
        }

        private void UpdateSortingOrderAsLast(PresenterEntry presenterParam)
        {
            if (presenterParam == null || presenterParam.Presenter == null || !presenterParam.Presenter.IsViewExist)
            {
                return;
            }

            System.Type viewType = presenterParam.Presenter.ViewType;
            bool foundSameView = false;
            int maxSortingOrder = default;

            // Every managed presenter counts here, not just the visible ones. A presenter that is only
            // loading already holds a sorting order in its layer, so leaving it out let two views of the
            // same type opened in one frame land on the same order. The presenter being placed is skipped
            // because it is registered before this runs and would otherwise be compared against itself.
            foreach (PresenterEntry param in _presenterEntries.Values)
            {
                if (param == presenterParam || !param.Presenter.IsViewExist || param.Presenter.ViewType != viewType)
                {
                    continue;
                }

                int sortingOrder = param.Presenter.SortingOrder;
                if (!foundSameView || sortingOrder > maxSortingOrder)
                {
                    foundSameView = true;
                    maxSortingOrder = sortingOrder;
                }
            }

            if (foundSameView)
            {
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

        /// <summary>
        /// The entries focus can land on: the ones whose view is on screen or leaving it. A presenter that is
        /// only loading is deliberately left out, because its view has never been shown. Anything that needs
        /// every managed presenter, such as sorting order placement, reads the registry instead.
        /// </summary>
        private IEnumerable<PresenterEntry> FocusCandidateEntries()
        {
            foreach (PresenterEntry param in _presenterCloseChecker)
            {
                if (param.Presenter.IsViewExist)
                {
                    yield return param;
                }
            }

            foreach (PresenterEntry param in _openedPresenters)
            {
                if (param.Presenter.IsViewExist)
                {
                    yield return param;
                }
            }

            foreach (PresenterEntry param in _presenterOpenChecker)
            {
                if (param.Presenter.IsViewExist)
                {
                    yield return param;
                }
            }
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
            _objectInjector?.Inject(presenter);

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
