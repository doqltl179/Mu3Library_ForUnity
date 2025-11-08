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



        private void Update()
        {
            for (int i = 0; i < _presenterLoadChecker.Count; i++)
            {
                if (_presenterLoadChecker[i].Presenter.ViewState == ViewState.Loaded)
                {
                    PresenterParams param = _presenterLoadChecker[i];

                    _presenterLoadChecker.RemoveAt(i);
                    i--;

                    param.Presenter.Open();

                    _presenterOpenChecker.Add(param);

                    UpdateFocus();
                }
            }

            for (int i = 0; i < _presenterOpenChecker.Count; i++)
            {
                if (_presenterOpenChecker[i].Presenter.ViewState == ViewState.Opened)
                {
                    PresenterParams param = _presenterOpenChecker[i];

                    _presenterOpenChecker.RemoveAt(i);
                    i--;

                    _openedPresenters.Add(param);
                }
            }

            for (int i = 0; i < _presenterCloseChecker.Count; i++)
            {
                if (_presenterCloseChecker[i].Presenter.ViewState == ViewState.Closed)
                {
                    PresenterParams param = _presenterCloseChecker[i];

                    _presenterCloseChecker.RemoveAt(i);
                    i--;

                    param.Presenter.Unload();

                    _presenterUnloadChecker.Add(param);

                    UpdateFocus();
                }
            }

            for (int i = 0; i < _presenterUnloadChecker.Count; i++)
            {
                if (_presenterUnloadChecker[i].Presenter.ViewState == ViewState.Unloaded)
                {
                    PresenterParams param = _presenterUnloadChecker[i];

                    _presenterUnloadChecker.RemoveAt(i);
                    i--;

                    if (param.Presenter.IsViewExist)
                    {
                        param.Presenter.SetActiveView(false);

                        MVPFactory.Instance.PoolPrsenter(param.Presenter);
                    }
                }
            }
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
                closeParam.Presenter.Close();

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

            param.Presenter.Close();

            _presenterCloseChecker.Add(param);
        }

        public void Open<TPresenter>(Arguments args, OutPanelParams param = null) where TPresenter : class, IPresenter, new()
        {
            TPresenter presenter = MVPFactory.Instance.CreatePresenter<TPresenter>();

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

                presenter.Init(view, args);
            }
            else
            {
                presenter.Init(args);
            }

            UpdateSortingOrderAsLast(presenter.View);
            presenter.OptimizeView();
            presenter.SetActiveView(true);

            presenter.Load();

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
                .Concat(_presenterOpenChecker);
        }
    }
}
