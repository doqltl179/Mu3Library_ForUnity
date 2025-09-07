using System;
using System.Collections;
using Mu3Library.Base.Attribute;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mu3Library.Base.UI.DesignPattern.MPV
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

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class View : MonoBehaviour, IView
    {
        protected Model _model = null;

        protected ViewState _viewState = ViewState.None;
        public ViewState ViewState { get => _viewState; }

        [Title("Parent Properties")]
        [SerializeField] protected Canvas _canvas;
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        public RenderMode RenderMode
        {
            get => _canvas == null ? RenderMode.ScreenSpaceOverlay : _canvas.renderMode;
        }
        public string SortingLayerName
        {
            get => _canvas == null ? "" : _canvas.sortingLayerName;
        }
        public Camera RenderCamera
        {
            get => _canvas.worldCamera;
        }
        public int SortingOrder
        {
            get => _canvas.sortingOrder;
        }

        private delegate bool Trigger();
        private Trigger _animationTrigger = null;

        private IEnumerator _openCloseCoroutine = null;



#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }
            if (_canvasGroup == null && _canvas != null)
            {
                _canvasGroup = _canvas.GetComponent<CanvasGroup>();
            }
        }

        protected virtual void Reset()
        {
            OnValidate();
        }
#endif

        public void OnLoad<TModel>(TModel model) where TModel : Model
        {
            _canvasGroup.interactable = false;

            OnLoadFunc(model);

            _model = model;

            _viewState = ViewState.Loaded;
        }
        protected virtual void OnLoadFunc<TModel>(TModel model) where TModel : Model { }

        public void OnUnload()
        {
            OnUnloadFunc();

            _viewState = ViewState.Unloaded;
        }
        protected virtual void OnUnloadFunc() { }

        public void Open()
        {
            if (_openCloseCoroutine != null)
            {
                return;
            }

            _animationTrigger = OpenAnimationTrigger;

            _openCloseCoroutine = OpenCoroutine();
            StartCoroutine(_openCloseCoroutine);
        }

        private IEnumerator OpenCoroutine()
        {
            _viewState = ViewState.Opening;

            OpenFunc();

            while (!_animationTrigger())
            {
                yield return null;
            }
            _animationTrigger = null;

            _canvasGroup.interactable = true;

            _openCloseCoroutine = null;
            
            _viewState = ViewState.Opened;
        }
        protected virtual void OpenFunc() { }
        protected virtual bool OpenAnimationTrigger() { return true; }


        public void Close()
        {
            if (_openCloseCoroutine != null)
            {
                return;
            }

            _animationTrigger = CloseAnimationTrigger;

            _openCloseCoroutine = CloseCoroutine();
            StartCoroutine(_openCloseCoroutine);
        }

        private IEnumerator CloseCoroutine()
        {
            _viewState = ViewState.Closing;

            _canvasGroup.interactable = false;

            CloseFunc();

            while (!_animationTrigger())
            {
                yield return null;
            }
            _animationTrigger = null;

            _openCloseCoroutine = null;

            _viewState = ViewState.Closed;
        }
        protected virtual void CloseFunc() { }
        protected virtual bool CloseAnimationTrigger() { return true; }

        public void CloseImmediately()
        {
            _canvasGroup.interactable = false;

            if (_openCloseCoroutine != null)
            {
                StopCoroutine(_openCloseCoroutine);
                _openCloseCoroutine = null;
            }

            if (_viewState == ViewState.Opening ||
                _viewState == ViewState.Opened)
            {
                CloseFunc();
            }

            _viewState = ViewState.Closed;
        }

        public void SetActive(bool value) => gameObject.SetActive(value);

        public void ChangeSortingOrder(int value) => _canvas.sortingOrder = value;

        public void ForceConfirm()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick?.Invoke();
            }
        }

        public void ForceCancel()
        {
            if (_cancelButton != null)
            {
                _cancelButton.onClick?.Invoke();
            }
        }

        public void AddConfirmEvent(UnityAction onClick)
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(onClick);
            }
        }

        public void RemoveConfirmEvent(UnityAction onClick)
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(onClick);
            }
        }

        public void AddCancelEvent(UnityAction onClick)
        {
            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(onClick);
            }
        }

        public void RemoveCancelEvent(UnityAction onClick)
        {
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(onClick);
            }
        }

        public void SetRenderModeToDefault(RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.WorldSpace: SetRenderModeToWorld(); break;
                case RenderMode.ScreenSpaceCamera: SetRenderModeToCamera(); break;
                case RenderMode.ScreenSpaceOverlay: SetRenderModeToOverlay(); break;
            }
        }

        public void SetRenderModeToWorld(string sortingLayerName = "Default", int sortingOrder = 0, Camera eventCamera = null)
        {
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera == null ? Camera.main : eventCamera;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerName = sortingLayerName;
            _canvas.sortingOrder = sortingOrder;
        }

        public void SetRenderModeToCamera(string sortingLayerName = "Default", int sortingOrder = 0, Camera eventCamera = null, float cameraDistance = 100f)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = eventCamera == null ? Camera.main : eventCamera;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerName = sortingLayerName;
            _canvas.sortingOrder = sortingOrder;
            _canvas.planeDistance = cameraDistance;
        }

        public void SetRenderModeToOverlay(string sortingLayerName = "Default", int sortingOrder = 0)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerName = sortingLayerName;
            _canvas.sortingOrder = sortingOrder;
        }
    }
}
