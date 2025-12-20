using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Utility;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public class MVPManager : GenericSingleton<MVPManager>
    {
        private Camera m_renderCamera = null;
        private Camera _renderCamera
        {
            get
            {
                if (m_renderCamera == null)
                {
                    GameObject go = new GameObject("RenderCamera");
                    go.transform.SetParent(transform);

                    m_renderCamera = go.AddComponent<Camera>();
                }

                return m_renderCamera;
            }
        }
        public Camera RenderCamera => _renderCamera;

        private readonly Dictionary<string, Canvas> _layerCanvases = new();

        private class PresenterParams
        {
            public IPresenter Presenter;
            public OutPanelParams OutPanelParams;
        }

        private readonly List<PresenterParams> _openedPresenters = new();

        private readonly List<PresenterParams> _presenterLoadChecker = new();
        private readonly List<PresenterParams> _presenterOpenChecker = new();
        private readonly List<PresenterParams> _presenterCloseChecker = new();
        private readonly List<PresenterParams> _presenterUnloadChecker = new();

        private PresenterParams _focused = null;

        private OutPanel _outPanel = null;

        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;



        private void Update()
        {
            CheckLifecycle(_presenterLoadChecker, ViewState.Loaded, WindowLoadedEvent);
            CheckLifecycle(_presenterOpenChecker, ViewState.Opened, WindowOpenedEvent);
            CheckLifecycle(_presenterCloseChecker, ViewState.Closed, WindowClosedEvent);
            CheckLifecycle(_presenterUnloadChecker, ViewState.Unloaded, WindowUnloadedEvent);
        }

        private void CheckLifecycle(List<PresenterParams> list, ViewState checkState, Action<PresenterParams> callback = null)
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
            MVPFactory.Instance.PoolPrsenter(param.Presenter);

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

        public IPresenter Open<TPresenter>() where TPresenter : class, IPresenter, new() => Open<TPresenter>(null, null);

        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : class, IPresenter, new() => Open<TPresenter>(args, null);

        public IPresenter Open<TPresenter>(OutPanelParams param) where TPresenter : class, IPresenter, new() => Open<TPresenter>(null, param);

        public IPresenter Open<TPresenter>(Arguments args, OutPanelParams param) where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = MVPFactory.Instance.CreatePresenter<TPresenter>();
            GetInterface(presenter, out var initialize, out var lifecycle);

            Type viewType = presenter.ViewType;
            string viewLayerName = MVPFactory.Instance.GetLayerName(viewType);

            if (!_layerCanvases.TryGetValue(viewLayerName, out Canvas layerCanvas))
            {
                layerCanvas = MVPFactory.Instance.CreateLayerDefaultCanvas(viewLayerName, transform, _renderCamera);

                _layerCanvases.Add(viewLayerName, layerCanvas);
            }

            if (!presenter.IsViewExist)
            {
                View view = MVPFactory.Instance.CreateView(viewType, layerCanvas);
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
                OutPanelParams = param,
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

            _renderCamera.cullingMask = mask;
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

            Type viewType = view.GetType();
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
                OutPanel panel = MVPFactory.Instance.CreateOutPanel(transform);

                _outPanel = panel;
            }

            if (param == null || param.OutPanelParams == null)
            {
                _outPanel.gameObject.SetActive(false);
            }
            else
            {
                _outPanel.gameObject.SetActive(true);

                _outPanel.UpdateOutPanel(param.Presenter, param.OutPanelParams);
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
    }
}
