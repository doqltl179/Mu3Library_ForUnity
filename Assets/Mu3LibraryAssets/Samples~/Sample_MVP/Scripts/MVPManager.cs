using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Base.UI.DesignPattern.MPV;
using UnityEngine;

namespace Mu3Library.Base.Sample.MVP
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

    public class MVPManager : GenericSingleton<MVPManager>
    {
        /// <summary>
        /// <br/> string: View resource path
        /// </summary>
        private Dictionary<string, View> _viewResources = null;
        /// <summary>
        /// <br/> string: View name
        /// </summary>
        private Dictionary<string, SortingLayer> _viewLayerMap = null;
        private const string VIEW_RESOURCE_PATH = "UIView";

        private Dictionary<string, List<IPresenter>> _presenterPool = null;

        /// <summary>
        /// 예약된 Open
        /// </summary>
        private Queue<MVPScheduleData> _scheduledOpen = new Queue<MVPScheduleData>();
        /// <summary>
        /// Open된 Presenter
        /// </summary>
        private List<MVPScheduleData> _opens = new List<MVPScheduleData>();
        /// <summary>
        /// 예약된 Close
        /// </summary>
        private Queue<MVPScheduleData> _scheduledClose = new Queue<MVPScheduleData>();
        /// <summary>
        /// 예약된 View 삭제
        /// </summary>
        private List<MVPScheduleData> _scheduledPool = new List<MVPScheduleData>();

        private MVPScheduleData _mostFrontData = null;

        private BackgroundBlock _backgroundBlock = null;



        private void Awake()
        {
            FillViewResources();
            ResetPool();
        }

        private void Update()
        {
            // Pool Schedule
            if (_scheduledPool.Count > 0)
            {
                for (int i = 0; i < _scheduledPool.Count; i++)
                {
                    MVPScheduleData scheduleData = _scheduledPool[i];
                    if (scheduleData.Presenter.ViewState == ViewState.Closed)
                    {
                        ExcuteSchedulePool(scheduleData);

                        _scheduledPool.RemoveAt(i);
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
                    MVPScheduleData scheduleData = _scheduledClose.Dequeue();
                    ExcuteScheduleClose(scheduleData);
                }
                isViewChanged = true;
            }
            // Open Schedule
            if (_scheduledOpen.Count > 0)
            {
                while (_scheduledOpen.Count > 0)
                {
                    MVPScheduleData scheduleData = _scheduledOpen.Dequeue();
                    ExcuteScheduleOpen(scheduleData);
                }
                isViewChanged = true;
            }

            // Change Most Front View
            if (isViewChanged)
            {
                UpdateMostFrontData();
            }
        }

        private void ExcuteSchedulePool(MVPScheduleData scheduleData)
        {
            IPresenter presenter = scheduleData.Presenter;
            presenter.OnUnload();

            string mvpName = GetMVPFullNameWithPresenter(presenter);
            List<IPresenter> pool = null;
            if (!_presenterPool.TryGetValue(mvpName, out pool))
            {
                pool = new List<IPresenter>();
                _presenterPool.Add(mvpName, pool);
            }

            pool.Add(presenter);
        }

        private void ExcuteScheduleClose(MVPScheduleData scheduleData)
        {
            IPresenter presenter = scheduleData.Presenter;
            presenter.Close();

            _opens.Remove(scheduleData);

            _scheduledPool.Add(scheduleData);
        }

        private void ExcuteScheduleOpen(MVPScheduleData scheduleData)
        {
            IPresenter presenter = scheduleData.Presenter;
            presenter.Open();

            // SortingOrder 설정
            // 같은 LayerName을 갖는 View에서 가장 큰 Order값을 반환한다.
            string currentLayerName = scheduleData.Presenter.ViewSortingLayerName;
            int maxSortingOrder = _opens
                .Where(t => t.Presenter.ViewSortingLayerName == currentLayerName)
                .Select(t => t.Presenter.ViewSortingOrder)
                .DefaultIfEmpty(-1)
                .Max();
            presenter.ChangeViewSortingOrder(maxSortingOrder + 1);

            _opens.Add(scheduleData);
        }

        #region Utility
        public void CloseAllImmediately(SortingLayer layer)
        {
            AddScheduleData(layer, _opens, _scheduledClose);
            AddScheduleData(layer, _scheduledOpen, _scheduledClose);

            while (_scheduledClose.Count > 0)
            {
                MVPScheduleData scheduleData = _scheduledClose.Dequeue();
                scheduleData.Presenter.CloseImmediately();

                _scheduledPool.Add(scheduleData);
            }

            UpdateMostFrontData();
        }

        public void CloseAllImmediately()
        {
            foreach (MVPScheduleData scheduleData in _opens)
            {
                _scheduledClose.Enqueue(scheduleData);
            }
            _opens.Clear();

            while (_scheduledOpen.Count > 0)
            {
                MVPScheduleData scheduleData = _scheduledOpen.Dequeue();
                _scheduledClose.Enqueue(scheduleData);
            }

            while (_scheduledClose.Count > 0)
            {
                MVPScheduleData scheduleData = _scheduledClose.Dequeue();
                scheduleData.Presenter.CloseImmediately();

                _scheduledPool.Add(scheduleData);
            }

            UpdateMostFrontData();
        }

        public void Close<TPresenter>(TPresenter presenter) where TPresenter : IPresenter
        {
            IPresenter compare = presenter;
            MVPScheduleData data = _opens.Where(t => t.Presenter == compare).LastOrDefault();

            if (data == null ||
                data.Presenter.ViewState == ViewState.Opening ||
                data.Presenter.ViewState == ViewState.Closing)
            {
                return;
            }

            _scheduledClose.Enqueue(data);
        }

        public void Open<TPresenter>(WindowParams param) where TPresenter : IPresenter, new()
        {
            string mvpName = GetMVPFullNameWithPresenter<TPresenter>();
            if (string.IsNullOrEmpty(mvpName))
            {
                return;
            }

            IPresenter presenter = null;
            if (_presenterPool.TryGetValue(mvpName, out List<IPresenter> pool))
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    IPresenter p = pool[i];
                    if (p.ViewState == ViewState.Unloaded)
                    {
                        presenter = p;
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }

            if (presenter == null)
            {
                if (!_viewLayerMap.TryGetValue(mvpName, out var layer))
                {
                    Debug.LogError($"Layer not found. mvpName: {mvpName}");
                    return;
                }

                if (!_viewResources.TryGetValue(mvpName, out View resource))
                {
                    Debug.LogError($"View Resource not found. mvpName: {mvpName}");
                    return;
                }

                presenter = new TPresenter();
                presenter.CreateView(resource, transform);

#pragma warning disable 0162 // "Unreachable code detected" 경고 번호
                // 임의로 설정한 코드이며, 필요하다면 수정하자.
                const RenderMode renderMode = RenderMode.ScreenSpaceCamera;
                switch (renderMode)
                {
                    case RenderMode.ScreenSpaceCamera:
                        presenter.View.SetRenderModeToCamera($"{layer}");
                        break;
                    case RenderMode.ScreenSpaceOverlay:
                        presenter.View.SetRenderModeToOverlay($"{layer}");
                        break;

                    default:
                        presenter.View.SetRenderModeToDefault(RenderMode.ScreenSpaceCamera);
                        break;
                }
#pragma warning restore 0162
            }

            presenter.OnLoad(param.Model);

            _scheduledOpen.Enqueue(new MVPScheduleData()
            {
                Presenter = presenter,
                Param = param
            });
        }

        public void ResetPool()
        {
            if (_presenterPool != null)
            {
                foreach (List<IPresenter> pool in _presenterPool.Values)
                {
                    foreach (IPresenter presenter in pool)
                    {
                        presenter.DestroyView();
                    }
                }
            }

            _presenterPool = new Dictionary<string, List<IPresenter>>();
        }
        #endregion

        private void AddScheduleData(SortingLayer compareLayer, Queue<MVPScheduleData> from, Queue<MVPScheduleData> to)
        {
            if (from == null || from.Count == 0)
            {
                return;
            }
            string layerName = $"{compareLayer}";

            List<MVPScheduleData> scheduledOpenList = from.ToList();
            for (int i = 0; i < scheduledOpenList.Count; i++)
            {
                MVPScheduleData scheduleData = scheduledOpenList[i];
                if (scheduleData.Presenter.ViewSortingLayerName == layerName)
                {
                    to.Enqueue(scheduleData);

                    scheduledOpenList.RemoveAt(i);
                    i--;
                }
            }

            from.Clear();
            foreach (MVPScheduleData scheduleData in scheduledOpenList)
            {
                from.Enqueue(scheduleData);
            }
        }

        private void AddScheduleData(SortingLayer compareLayer, List<MVPScheduleData> from, Queue<MVPScheduleData> to)
        {
            string layerName = $"{compareLayer}";
            for (int i = 0; i < from.Count; i++)
            {
                MVPScheduleData scheduleData = from[i];
                if (scheduleData.Presenter.ViewSortingLayerName == layerName)
                {
                    to.Enqueue(scheduleData);

                    from.RemoveAt(i);
                    i--;
                }
            }
        }

        private void UpdateMostFrontData()
        {
            MVPScheduleData frontData = null;

            if (_opens.Count > 0)
            {
                SortingLayer[] layers = Enum.GetValues(typeof(SortingLayer)) as SortingLayer[];
                for (int i = layers.Length - 1; i >= 0; i--)
                {
                    SortingLayer layer = layers[i];
                    string layerName = layer.ToString();

                    MVPScheduleData data = _opens.Where(t => t.Presenter.ViewSortingLayerName == layerName).LastOrDefault();
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
                block.gameObject.SetActive(true);
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
            _viewLayerMap = new Dictionary<string, SortingLayer>();

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

                    // 2. Resource는 View 컴포넌트를 가지고 있어야 한다.
                    View viewComponent = resource.GetComponent<View>();
                    if (viewComponent == null)
                    {
                        Debug.Log($"View Component not found. resourceName: {resourceName}");
                        continue;
                    }

                    // 3. View 타입과 대응되는 Presenter 타입이 존재해야 한다.
                    string mvpName = GetMVPFullNameWithView(viewComponent);
                    string presenterTypeString = $"{mvpName}Presenter";
                    Type presenterType = Type.GetType(presenterTypeString);
                    if (presenterType == null)
                    {
                        Debug.Log($"Presenter Type not found. mvpName: {mvpName}");
                        continue;
                    }

                    _viewResources.Add(mvpName, viewComponent);
                    _viewLayerMap.Add(mvpName, layer);

                    Debug.Log($"Found View Resource. mvpName: {mvpName}");
                }
            }
        }

        private string GetMVPFullNameWithView<TView>(TView view) where TView : IView
        {
            return GetMVPFullNameWithView(view.GetType().FullName);
        }

        private string GetMVPFullNameWithView<TView>() where TView : IView
        {
            return GetMVPFullNameWithView(typeof(TView).FullName);
        }

        private string GetMVPFullNameWithView(string fullName)
        {
            const string removeString = "View";
            if (fullName.EndsWith(removeString))
            {
                return fullName[0..(fullName.Length - removeString.Length)];
            }
            else
            {
                Debug.LogError($"'removeString' not found in full name. fullName: {fullName}");
                return "";
            }
        }

        private string GetMVPFullNameWithPresenter<TPresenter>(TPresenter presenter) where TPresenter : IPresenter
        {
            return GetMVPFullNameWithPresenter(presenter.GetType().FullName);
        }

        private string GetMVPFullNameWithPresenter<TPresenter>() where TPresenter : IPresenter
        {
            return GetMVPFullNameWithPresenter(typeof(TPresenter).FullName);
        }

        private string GetMVPFullNameWithPresenter(string fullName)
        {
            const string removeString = "Presenter";
            if (fullName.EndsWith(removeString))
            {
                return fullName[0..(fullName.Length - removeString.Length)];
            }
            else
            {
                Debug.LogError($"'removeString' not found in full name. fullName: {fullName}");
                return "";
            }
        }
    }
}
