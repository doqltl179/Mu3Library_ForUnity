using UnityEngine;

namespace Mu3Library.UI.MVP
{
    [System.Serializable]
    public struct OutPanelSettings
    {
        [SerializeField] private bool _useOutPanel;
        [SerializeField] private Color _color;
        [SerializeField] private bool _interactAsClose;

        public bool UseOutPanel
        {
            get => _useOutPanel;
            set => _useOutPanel = value;
        }

        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public bool InteractAsClose
        {
            get => _interactAsClose;
            set => _interactAsClose = value;
        }

        public static readonly OutPanelSettings Standard = new()
        {
            _useOutPanel = true,
            _color = new Color(0, 0, 0, 0.5f),
            _interactAsClose = true,
        };

        public static readonly OutPanelSettings Disabled = new()
        {
            _useOutPanel = false,
        };
    }
}
