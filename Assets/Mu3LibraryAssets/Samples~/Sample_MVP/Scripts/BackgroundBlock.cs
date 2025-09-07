using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Base.Sample.MVP
{
    public class BackgroundBlock : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Button _button;
        [SerializeField] private Image _image;

        public RenderMode RenderMode
        {
            get => _canvas.renderMode;
            set => _canvas.renderMode = value;
        }
        public Camera RenderCamera
        {
            get => _canvas.worldCamera;
            set => _canvas.worldCamera = value;
        }
        public string SortingLayerName
        {
            get => _canvas.sortingLayerName;
            set => _canvas.sortingLayerName = value;
        }
        public int SortingOrder
        {
            get => _canvas.sortingOrder;
            set => _canvas.sortingOrder = value;
        }

        public Color Color
        {
            get => _image.color;
            set => _image.color = value;
        }

        private event Action OnClickEvent;



#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }
            if (_button == null && _canvas != null)
            {
                Button btn = _canvas.GetComponentInChildren<Button>();
                if (btn == null)
                {
                    GameObject go = new GameObject("Block");
                    go.transform.SetParent(_canvas.transform);
                    go.AddComponent<CanvasRenderer>();
                    go.AddComponent<Image>();
                    btn = go.AddComponent<Button>();
                }
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation() { mode = Navigation.Mode.None };
                _button = btn;
            }
            if (_image == null && _button != null)
            {
                Image img = _button.image;
                if (img == null)
                {
                    img = _button.GetComponent<Image>();
                    _button.image = img;
                }
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.pivot = Vector2.one * 0.5f;
                img.rectTransform.offsetMin = Vector2.zero;
                img.rectTransform.offsetMax = Vector2.zero;
                _image = img;
            }
        }

        private void Reset()
        {
            OnValidate();
        }
#endif

        private void OnEnable()
        {
            if (_button == null)
            {
                return;
            }
            _button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            if (_button == null)
            {
                return;
            }
            _button.onClick.RemoveListener(OnClick);
        }

        private void Start()
        {
            _canvas.overrideSorting = true;
        }

        private void OnClick() => OnClickEvent?.Invoke();

        #region Utility
        public void SetOnClickEvent(Action onClick = null) => OnClickEvent = onClick;
        #endregion
    }
}