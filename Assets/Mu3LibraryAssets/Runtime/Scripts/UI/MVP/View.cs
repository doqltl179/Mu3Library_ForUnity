using System.Collections;
using UnityEngine;
using Mu3Library.Extensions;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    public enum ViewState
    {
        None = 0,

        Loaded = 1,
        Opening = 2,
        Opened = 3,
        Closing = 4,
        Closed = 5,
        Unloaded = 6,
    }

    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public abstract class View : MonoBehaviour, IView
    {
        private ViewState _viewState = ViewState.None;
        public ViewState ViewState => _viewState;

        private RectTransform m_rectTransform = null;
        protected RectTransform _rectTransform
        {
            get
            {
                if (m_rectTransform == null)
                {
                    m_rectTransform = gameObject.GetOrAddComponent<RectTransform>();
                }

                return m_rectTransform;
            }
        }
        internal RectTransform RectTransform => _rectTransform;

        private Canvas m_canvas = null;
        protected Canvas _canvas
        {
            get
            {
                if (m_canvas == null)
                {
                    m_canvas = gameObject.GetOrAddComponent<Canvas>();
                    gameObject.GetOrAddComponent<CanvasScaler>();
                    gameObject.GetOrAddComponent<GraphicRaycaster>();

                    m_canvas.overrideSorting = true;
                }

                return m_canvas;
            }
        }
        internal Canvas Canvas => _canvas;

        public string ObjectLayerName => LayerMask.LayerToName(gameObject.layer);
        public string CanvasLayerName => _canvas.sortingLayerName;
        public int SortingOrder => _canvas.sortingOrder;

        private CanvasGroup m_canvasGroup = null;
        protected CanvasGroup _canvasGroup
        {
            get
            {
                if (m_canvasGroup == null)
                {
                    m_canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
                }

                return m_canvasGroup;
            }
        }
        internal CanvasGroup CanvasGroup => _canvasGroup;

        public bool Interactable
        {
            get => _canvasGroup.interactable;
            set => _canvasGroup.interactable = value;
        }
        public float Alpha
        {
            get => _canvasGroup.alpha;
            set => _canvasGroup.alpha = value;
        }

        public Vector2 AnchoredPosition
        {
            get => _rectTransform.anchoredPosition;
            set => _rectTransform.anchoredPosition = value;
        }

        public Vector3 LocalScale
        {
            get => _rectTransform.localScale;
            set => _rectTransform.localScale = value;
        }

        private IEnumerator _lifeCycleCoroutine = null;



        protected virtual void OnDisable()
        {
            if (_lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }
        }

        internal void Load()
        {
            _canvasGroup.interactable = false;

            LoadFunc();

            _viewState = ViewState.Loaded;
        }

        protected virtual void LoadFunc() { }

        internal void Open()
        {
            if (_lifeCycleCoroutine == null)
            {
                _lifeCycleCoroutine = OpenCoroutine();
                StartCoroutine(_lifeCycleCoroutine);
            }
        }

        private IEnumerator OpenCoroutine()
        {
            _viewState = ViewState.Opening;

            OpenStart();
            yield return StartCoroutine(WaitOpening());
            OpenEnd();

            _canvasGroup.interactable = true;

            _viewState = ViewState.Opened;

            _lifeCycleCoroutine = null;
        }

        protected virtual void OpenStart() { }
        protected virtual bool WaitOpeningUntil() { return true; }
        protected virtual IEnumerator WaitOpening() { yield return new WaitUntil(WaitOpeningUntil); }
        protected virtual void OpenEnd() { }

        internal void Close(bool forceClose = false)
        {
            if(forceClose && _lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            if (_lifeCycleCoroutine == null)
            {
                _lifeCycleCoroutine = CloseCoroutine();
                StartCoroutine(_lifeCycleCoroutine);
            }
        }

        private IEnumerator CloseCoroutine()
        {
            _viewState = ViewState.Closing;

            _canvasGroup.interactable = false;

            CloseStart();
            yield return StartCoroutine(WaitClosing());
            CloseEnd();

            _viewState = ViewState.Closed;

            _lifeCycleCoroutine = null;
        }

        protected virtual void CloseStart() { }
        protected virtual bool WaitClosingUntil() { return true; }
        protected virtual IEnumerator WaitClosing() { yield return new WaitUntil(WaitClosingUntil); }
        protected virtual void CloseEnd() { }

        internal void Unload()
        {
            UnloadFunc();

            _viewState = ViewState.Unloaded;
        }

        protected virtual void UnloadFunc() { }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public void DestroySelf()
        {
            if (_lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            Destroy(gameObject);
        }

        #region Utility
        public void SetSortingLayer(string layerName)
        {
            _canvas.sortingLayerName = layerName;
        }

        public void SetSortingOrder(int sortingOrder)
        {
            _canvas.sortingOrder = sortingOrder;
        }

        internal void OverwriteInto(Canvas target)
        {
            MVPCanvasUtil.Overwrite(_canvas, target, true, true);
        }

        public void Stretch() => _rectTransform.Stretch();
        #endregion
    }
}
