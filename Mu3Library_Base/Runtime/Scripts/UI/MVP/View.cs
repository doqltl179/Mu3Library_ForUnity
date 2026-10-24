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

        public Vector2 SizeDelta
        {
            get => _rectTransform.sizeDelta;
            set => _rectTransform.sizeDelta = value;
        }

        public Vector3 LocalScale
        {
            get => _rectTransform.localScale;
            set => _rectTransform.localScale = value;
        }

        private IEnumerator _lifeCycleCoroutine = null;
        private bool _isDestroying = false;



        protected virtual void OnDisable()
        {
            if (_lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            // Unity does not resume a coroutine it stopped, so a close cut short here would stay
            // Closing and the manager would wait for Closed forever. This is what happens to a child
            // hosted under an owner that finished closing first and was hidden. The close ends here
            // without CloseEnd(), as an open that a force close interrupts ends without OpenEnd(),
            // and the manager unloads and hides the view next.
            if (_viewState == ViewState.Closing)
            {
                _viewState = ViewState.Closed;
            }
            // An open cut short the same way would stay Opening, so OpenAsync would never complete and
            // a Close that is not forced would be refused. Such a view finishes its open here instead:
            // it is on screen again once its host is shown, so OpenEnd() still runs, as it does for a
            // view opened under a host that is already hidden. A view that DestroySelf() destroys is
            // never shown again, so its open is left as it is.
            else if (_viewState == ViewState.Opening && !_isDestroying)
            {
                EndCutShortOpen();
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
                // Unity refuses to start a coroutine on an object that is inactive in the hierarchy,
                // such as a view opened under a host that is hidden. Such a view skips the wait of its
                // open transition instead of never reaching Opened: OpenStart() and OpenEnd() run back
                // to back, so the view is in its opened state when the host is shown. An AnimationView
                // takes its opened pose at once.
                if (!gameObject.activeInHierarchy)
                {
                    _viewState = ViewState.Opening;

                    OpenStart();
                    EndOpen();
                    return;
                }

                _lifeCycleCoroutine = OpenCoroutine();
                StartCoroutine(_lifeCycleCoroutine);
            }
        }

        private IEnumerator OpenCoroutine()
        {
            _viewState = ViewState.Opening;

            OpenStart();
            yield return StartCoroutine(WaitOpening());
            EndOpen();

            _lifeCycleCoroutine = null;
        }

        private void EndOpen()
        {
            OpenEnd();

            SetOpened();
        }

        private void SetOpened()
        {
            _canvasGroup.interactable = true;

            _viewState = ViewState.Opened;
        }

        /// <summary>
        /// Ends an open whose coroutine stopped because the view was hidden: the rest of the open
        /// transition is skipped and the view takes its opened state.
        /// </summary>
        private void EndCutShortOpen()
        {
            // The view leaves Opening even when one of these callbacks throws. The manager would
            // otherwise end the same open, and raise the same exception, on every update.
            try
            {
                SkipOpenTransition();
                OpenEnd();
            }
            finally
            {
                SetOpened();
            }
        }

        protected virtual void OpenStart() { }
        protected virtual bool WaitOpeningUntil() { return true; }
        protected virtual IEnumerator WaitOpening() { yield return new WaitUntil(WaitOpeningUntil); }
        protected virtual void OpenEnd() { }

        /// <summary>
        /// Runs when hiding the view cut its open transition short, before <see cref="OpenEnd"/>. A view
        /// that keeps a pose for its transitions takes its opened pose here, so it shows fully open once
        /// it is visible again.
        /// </summary>
        internal virtual void SkipOpenTransition() { }

        internal void Close(bool forceClose = false)
        {
            if (forceClose && _lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            if (_lifeCycleCoroutine == null)
            {
                // Unity refuses to start a coroutine on an object that is inactive in the hierarchy,
                // such as a view hosted under an owner that is still loading. Such a view is not on
                // screen, so it skips its close transition, CloseStart() and CloseEnd() included,
                // instead of never reaching Closed.
                if (!gameObject.activeInHierarchy)
                {
                    _canvasGroup.interactable = false;
                    _viewState = ViewState.Closed;

                    SkipCloseTransition();
                    return;
                }

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

        /// <summary>
        /// Runs when a view that is inactive in the hierarchy closes without its close transition.
        /// A view that keeps a pose for its transitions puts it in the closed state here, so its next
        /// open animates from the start.
        /// </summary>
        internal virtual void SkipCloseTransition() { }

        /// <summary>
        /// Ends a close that hiding the view cut short, as <see cref="OnDisable"/> does, for a view
        /// whose own OnDisable does not call base. Does nothing while the view is not closing or is
        /// still active in the hierarchy, where its close coroutine keeps running.
        /// </summary>
        internal void EndHiddenClose()
        {
            if (_viewState != ViewState.Closing || gameObject.activeInHierarchy)
            {
                return;
            }

            if (_lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            _viewState = ViewState.Closed;
        }

        /// <summary>
        /// Ends an open that hiding the view cut short, as <see cref="OnDisable"/> does, for a view
        /// whose own OnDisable does not call base. Does nothing while the view is not opening or is
        /// still active in the hierarchy, where its open coroutine keeps running.
        /// </summary>
        internal void EndHiddenOpen()
        {
            if (_viewState != ViewState.Opening || gameObject.activeInHierarchy)
            {
                return;
            }

            if (_lifeCycleCoroutine != null)
            {
                StopCoroutine(_lifeCycleCoroutine);
                _lifeCycleCoroutine = null;
            }

            EndCutShortOpen();
        }

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

            _isDestroying = true;
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
            _canvas.CopyTo(target, true, true);
        }

        public void Stretch() => _rectTransform.Stretch();
        #endregion
    }
}
