using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.UI.DesignPattern.MPV;
using UnityEngine;

namespace Mu3Library.Sample.MVP
{
    /// <summary>
    /// 예시 enum으로, 실제 사용 시에는 프로젝트 세팅에 맞게 고쳐 쓰자.
    /// </summary>
    public enum SortingLayer
    {
        Default = 0,
        Popup = 1000,
        SystemPopup = 10000,
    }

    public enum BackgroundInteractType
    {
        None = 0,
        Confirm = 1,
        Cancel = 2,
    }

    /// <summary>
    /// 예시 Paramerter로, 필요에 맞게 수정하여 사용하자.
    /// </summary>
    public class WindowParams
    {
        public Model Model;

        public Color BackgroundColor = new Color(0, 0, 0, 180 / 255f);
        public BackgroundInteractType BackgroundInteractType = BackgroundInteractType.None;
    }

    public class MVPManager : GenericSingleton<MVPManager>
    {
        private class ScheduleData
        {
            public SortingLayer Layer;
            public IPresenter Presenter;
            public WindowParams Param;
        }

        private Dictionary<string, View> _viewResources = null;
        private const string VIEW_RESOURCE_PATH = "UIView";

        /// <summary>
        /// 예약된 Open
        /// </summary>
        private Queue<ScheduleData> _scheduledOpen = new Queue<ScheduleData>();
        /// <summary>
        /// Open된 Presenter
        /// </summary>
        private List<ScheduleData> _opened = new List<ScheduleData>();
        /// <summary>
        /// 예약된 Close
        /// </summary>
        private Queue<ScheduleData> _scheduledClose = new Queue<ScheduleData>();
        /// <summary>
        /// 예약된 View 삭제
        /// </summary>
        private List<ScheduleData> _scheduledDestroy = new List<ScheduleData>();

        private ScheduleData _mostFrontData = null;

        private BackgroundBlock _backgroundBlock = null;



        private void Awake()
        {
            FillViewResources();
        }

        private void Update()
        {
            // Destroy Schedule
            if (_scheduledDestroy.Count > 0)
            {
                for (int i = 0; i < _scheduledDestroy.Count; i++)
                {
                    ScheduleData scheduleData = _scheduledDestroy[i];
                    if (!scheduleData.Presenter.View.IsOpened)
                    {
                        View view = scheduleData.Presenter.View as View;
                        view.Destroyed();

                        Destroy(view.gameObject);

                        _scheduledDestroy.RemoveAt(i);
                        i--;
                    }
                }
            }

            bool isViewChanged = false;

            // Close Schedule
            if (_scheduledClose.Count > 0)
            {
                while (_scheduledClose.Count > 0)
                {
                    ScheduleData scheduleData = _scheduledClose.Dequeue();

                    IPresenter presenter = scheduleData.Presenter;
                    presenter.Close();

                    _scheduledDestroy.Add(scheduleData);
                    _opened.Remove(scheduleData);
                }

                isViewChanged = true;
            }
            // Open Schedule
            if (_scheduledOpen.Count > 0)
            {
                while (_scheduledOpen.Count > 0)
                {
                    ScheduleData scheduleData = _scheduledOpen.Dequeue();

                    IPresenter presenter = scheduleData.Presenter;
                    presenter.View.SetActive(true);
                    presenter.Open();

                    // SortingOrder 설정
                    int maxSortingOrder = _opened
                        .Where(t => t.Layer == scheduleData.Layer)
                        .Select(t => t.Presenter.View.SortingOrder)
                        .DefaultIfEmpty(-1)
                        .Max();
                    presenter.View.ChangeSortingOrder(maxSortingOrder + 1);

                    _opened.Add(scheduleData);
                }

                isViewChanged = true;
            }

            // Change Most Front View
            if (isViewChanged)
            {
                UpdateMostFrontData();
            }
        }

        #region Utility
        public void Close(IView view)
        {
            ScheduleData data = _opened.Where(t => t.Presenter.View == view).LastOrDefault();

            if (data == null || data.Presenter.View.IsOpeningOrClosing)
            {
                return;
            }

            _scheduledClose.Enqueue(data);
        }

        public void Open<TView, TPresenter>(SortingLayer layer, WindowParams param)
            where TView : View
            where TPresenter : Presenter, new()
        {
            string resourcePath = $"{VIEW_RESOURCE_PATH}/{layer}/{typeof(TView).Name}";
            if (!_viewResources.TryGetValue(resourcePath, out View resource))
            {
                Debug.LogError($"View not found. resourcePath: {resourcePath}");
                return;
            }

            TView view = Instantiate(resource as TView, transform);
            view.SetActive(false);

            // 임의로 설정한 코드이며, 필요하다면 수정하자.
            switch (layer)
            {
                case SortingLayer.Default:
                case SortingLayer.Popup:
                    view.SetRenderModeToCamera($"{layer}");
                    break;

                case SortingLayer.SystemPopup:
                    view.SetRenderModeToOverlay($"{layer}");
                    break;

                    // Not Defined.
                    // case RenderMode.:
                    //     view.SetRenderModeToWorld($"{layer}");
                    //     break;
            }

            TPresenter presenter = new TPresenter();
            presenter.Init(param.Model, view);

            _scheduledOpen.Enqueue(new ScheduleData()
            {
                Layer = layer,
                Presenter = presenter,
                Param = param
            });
        }
        #endregion

        private void UpdateMostFrontData()
        {
            ScheduleData frontData = null;

            if (_opened.Count > 0)
            {
                SortingLayer[] layers = Enum.GetValues(typeof(SortingLayer)) as SortingLayer[];
                for (int i = layers.Length - 1; i >= 0; i--)
                {
                    SortingLayer layer = layers[i];

                    ScheduleData data = _opened.Where(t => t.Layer == layer).LastOrDefault();
                    if (data != null)
                    {
                        frontData = data;
                        break;
                    }
                }
            }

            RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
            Camera renderCamera = null;
            string sortingLayerName = "Default";
            int sortingOrder = -1;
            Color backgroundColor = Color.clear;
            Action backgroundInteractEvent = null;
            if (frontData != null)
            {
                IView view = frontData.Presenter.View;
                renderMode = view.RenderMode;
                renderCamera = view.RenderCamera;
                sortingLayerName = view.SortingLayerName;
                sortingOrder = view.SortingOrder - 1;

                WindowParams param = frontData.Param;
                backgroundColor = param.BackgroundColor;

                BackgroundInteractType bgInteractType = param.BackgroundInteractType;
                if (bgInteractType != BackgroundInteractType.None)
                {
                    View classView = view as View;
                    if (bgInteractType == BackgroundInteractType.Confirm)
                    {
                        backgroundInteractEvent = classView.ForceConfirm;
                    }
                    else
                    {
                        backgroundInteractEvent = classView.ForceCancel;
                    }
                }
            }
            SetBackgroundBlock
            (
                renderMode,
                renderCamera,
                sortingLayerName,
                sortingOrder,
                backgroundColor,
                backgroundInteractEvent
            );

            _mostFrontData = frontData;
        }

        private void SetBackgroundBlock(
            RenderMode renderMode,
            Camera renderCamera,
            string sortingLayerName,
            int sortingOrder,
            Color color,
            Action onClick = null)
        {
            if (_backgroundBlock == null)
            {
                string resourcePath = $"{VIEW_RESOURCE_PATH}/{typeof(BackgroundBlock).Name}";
                BackgroundBlock resource = ResourceLoader.GetResource<BackgroundBlock>(resourcePath);
                if (resource == null)
                {
                    return;
                }

                BackgroundBlock block = Instantiate(resource, transform);
                block.transform.SetAsFirstSibling();

                _backgroundBlock = block;
            }

            _backgroundBlock.RenderMode = renderMode;
            _backgroundBlock.RenderCamera = renderCamera;
            _backgroundBlock.SortingLayerName = sortingLayerName;
            _backgroundBlock.SortingOrder = sortingOrder;
            _backgroundBlock.Color = color;
            _backgroundBlock.SetOnClickEvent(onClick);
        }

        private void FillViewResources()
        {
            if (_viewResources != null)
            {
                return;
            }
            _viewResources = new Dictionary<string, View>();

            foreach (SortingLayer layer in Enum.GetValues(typeof(SortingLayer)))
            {
                string folder = $"{VIEW_RESOURCE_PATH}/{layer}";
                GameObject[] resources = ResourceLoader.GetAllResources<GameObject>(folder);
                foreach (GameObject resource in resources)
                {
                    // String을 Type으로 변환하기 위해 규칙을 세운다.
                    // 1. Resources 폴더에 존재하는 View 오브젝트의 이름은 View로 끝나도록 한다.
                    string resourceName = resource.name;
                    if (!resourceName.EndsWith("View"))
                    {
                        continue;
                    }

                    // 2. resourceName과 같은 이름의 View 타입이 존재해야 한다.
                    string viewTypeName = $"Mu3Library.Sample.MVP.{resourceName}";
                    Type viewType = Type.GetType(viewTypeName);
                    if (viewType == null)
                    {
                        Debug.Log($"View Type not found. name: {viewTypeName}");
                        continue;
                    }

                    // 3. Resource 오브젝트가 View를 가지고 있어야 한다.
                    View view = resource.GetComponent(viewType) as View;
                    if (view == null)
                    {
                        Debug.Log($"View component not found in this object. name: {resourceName}");
                        continue;
                    }

                    _viewResources.Add($"{folder}/{resourceName}", view);
                }
            }
        }
    }
}
