using System.Collections;
using Mu3Library.Attribute;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mu3Library.UI.DesignPattern.MPV
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class View : MonoBehaviour, IView
    {
        protected Model _model = null;

        private bool _isOpened = false;
        public bool IsOpened { get => _isOpened; }

        public bool IsOpeningOrClosing { get => _openCloseCoroutine != null; }

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

        protected delegate bool Trigger();
        protected Trigger _animationTrigger = null;

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

        public virtual void OnLoad<TModel>(TModel model) where TModel : Model
        {
            _canvasGroup.interactable = false;

            _model = model;
        }

        public virtual void OnUnload()
        {

        }

        public void Open()
        {
            if (_openCloseCoroutine != null)
            {
                Debug.LogError($"Logic Error!");
                return;
            }

            _animationTrigger = OpenAnimationTrigger;

            _openCloseCoroutine = OpenCoroutine();
            StartCoroutine(_openCloseCoroutine);
        }

        private IEnumerator OpenCoroutine()
        {
            while (!_animationTrigger())
            {
                yield return null;
            }
            _animationTrigger = null;

            _canvasGroup.interactable = true;

            _isOpened = true;
            _openCloseCoroutine = null;
        }

        protected virtual bool OpenAnimationTrigger() { return true; }

        public void Close()
        {
            if (_openCloseCoroutine != null)
            {
                Debug.LogError($"Logic Error!");
                return;
            }

            _animationTrigger = CloseAnimationTrigger;

            _openCloseCoroutine = CloseCoroutine();
            StartCoroutine(_openCloseCoroutine);
        }

        private IEnumerator CloseCoroutine()
        {
            _canvasGroup.interactable = false;

            while (!_animationTrigger())
            {
                yield return null;
            }
            _animationTrigger = null;

            _isOpened = false;
            _openCloseCoroutine = null;
        }

        protected virtual bool CloseAnimationTrigger() { return true; }

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
            _canvas.sortingLayerName = sortingLayerName;
            _canvas.sortingOrder = sortingOrder;
        }
    }
}
