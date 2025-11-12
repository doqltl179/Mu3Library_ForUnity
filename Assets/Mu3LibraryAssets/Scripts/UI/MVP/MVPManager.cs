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

            if(isViewDestroyed)
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

            OnWindowUnloaded?.Invoke(param.Presenter);
        }

        #region Utility
        public void CloseFocused()
        {
            if (_focused == null || _focused.Presenter == null)
            {
                return;
            }

            PresenterParams closeParam = _focused;
            _focused = null;

            if (_openedPresenters.Remove(closeParam))
            {
                if (closeParam.Presenter is ILifecycle lifecycle)
                {
                    lifecycle.Close();
                }

                _presenterCloseChecker.Add(closeParam);
            }
            else
            {
                Debug.LogWarning("Focused not found.");

                if (closeParam.Presenter.IsViewExist)
                {
                    closeParam.Presenter.ForceDestroyView();
                }
            }
        }

        public void Close<TPresenter>() where TPresenter : IPresenter
        {
            Type presenterType = typeof(TPresenter);

            PresenterParams param = null;
            for (int i = 0; i < _openedPresenters.Count; i++)
            {
                param = _openedPresenters[i];

                if (param.Presenter == null)
                {
                    _openedPresenters.RemoveAt(i);
                    i--;
                }
                else if (param.Presenter.GetType() == presenterType)
                {
                    _openedPresenters.RemoveAt(i);
                    break;
                }
            }

            if (param == null || param.Presenter == null)
            {
                Debug.LogWarning($"Presenter not found. type: {presenterType}");
                return;
            }

            if (param.Presenter is ILifecycle lifecycle)
            {
                lifecycle.Close();
            }

            _presenterCloseChecker.Add(param);
        }

        public void Open<TPresenter>(Arguments args, OutPanelParams param = null) where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = MVPFactory.Instance.CreatePresenter<TPresenter>();
            IPresenterInitialize initialize = presenter as IPresenterInitialize;
            ILifecycle lifecycle = presenter as ILifecycle;

            if (args.GetType() != presenter.ArgumentType)
            {
                Debug.LogError($"Undefined argument type. requested presenter: {presenter.GetType()}, requested argument: {args.GetType()}");
                return;
            }

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
                    return;
                }

                if (initialize != null)
                {
                    initialize.Init(view, args);
                }
            }
            else
            {
                if (initialize != null)
                {
                    initialize.Init(args);
                }
            }

            UpdateSortingOrderAsLast(presenter.View);
            presenter.OptimizeView();
            presenter.SetActiveView(true);

            if (lifecycle != null)
            {
                lifecycle.Load();
            }

            PresenterParams presenterParams = new PresenterParams()
            {
                Presenter = presenter,
                OutPanelParams = param,
            };
            _presenterLoadChecker.Add(presenterParams);
        }
        #endregion

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
                panel.OnClick += () =>
                {
                    CloseFocused();
                };

                _outPanel = panel;
            }

            if (param == null || param.OutPanelParams == null)
            {
                _outPanel.gameObject.SetActive(false);
            }
            else
            {
                _outPanel.gameObject.SetActive(true);

                _outPanel.SetColor(param.OutPanelParams.Color);
                _outPanel.ButtonEnabled = param.OutPanelParams.InteractAsClose;

                if (_layerCanvases.TryGetValue(param.Presenter.LayerName, out Canvas rootCanvas))
                {
                    Canvas viewCanvas = param.Presenter.ViewCanvas;
                    _outPanel.Overwrite(viewCanvas.overrideSorting ? viewCanvas : rootCanvas);
                }
                else
                {
                    _outPanel.LayerName = param.Presenter.LayerName;
                }

                _outPanel.SortingOrder = param.Presenter.SortingOrder - 1;
            }
        }

        private int GetRunningCount<TPresenter>() where TPresenter : IPresenter
        {
            Type presenterType = typeof(TPresenter);
            return RunningPresenterParams().Count(t => t.Presenter.GetType() == presenterType);
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
