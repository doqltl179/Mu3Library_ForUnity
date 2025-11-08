using System;
using Mu3Library.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasGroup))]
    public class OutPanel : MonoBehaviour
    {
        private Canvas m_canvas = null;
        private Canvas _canvas
        {
            get
            {
                if (m_canvas == null)
                {
                    m_canvas = gameObject.GetOrAddComponent<Canvas>();
                    gameObject.GetOrAddComponent<CanvasScaler>();
                    gameObject.GetOrAddComponent<GraphicRaycaster>();
                }

                return m_canvas;
            }
        }
        public Canvas Canvas => _canvas;

        public string LayerName
        {
            get => _canvas.sortingLayerName;
            set => _canvas.sortingLayerName = value;
        }
        public int SortingOrder
        {
            get => _canvas.sortingOrder;
            set => _canvas.sortingOrder = value;
        }

        [SerializeField] private Button _button;
        public bool ButtonEnabled
        {
            get => _button.enabled;
            set => _button.enabled = value;
        }

        public event Action OnClick;


        private void Awake()
        {
            _canvas.overrideSorting = true;

            if (_button == null)
            {
                GameObject go = new GameObject(
                    "Panel",
                    new Type[] { typeof(CanvasRenderer), typeof(Image), typeof(Button) });
                go.transform.SetParent(transform);

                Image image = go.GetComponent<Image>();
                image.rectTransform.Stretch();

                Button button = go.GetComponent<Button>();
                button.image = image;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation() { mode = Navigation.Mode.None };

                _button = button;
            }

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClickEvent);
        }

        #region Utility
        public void Overwrite(IView view) => view.OverwriteInto(_canvas);

        public void Overwrite(Canvas source)
        {
            if (source == null || _canvas == null)
            {
                return;
            }

            CanvasUtil.Overwrite(source, _canvas, true, true);
        }

        public void SetColor(Color color)
        {
            _button.image.color = color;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        #endregion

        private void OnClickEvent()
        {
            OnClick?.Invoke();
        }
    }
}