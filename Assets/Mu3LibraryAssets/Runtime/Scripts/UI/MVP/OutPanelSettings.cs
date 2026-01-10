using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public struct OutPanelSettings
    {
        public bool UseOutPanel;
        public Color Color;
        public bool InteractAsClose;

        public static readonly OutPanelSettings Standard = new()
        {
            UseOutPanel = true,
            Color = new Color(0, 0, 0, 210 / 255f),
            InteractAsClose = true,
        };

        public static readonly OutPanelSettings Disabled = new()
        {
            UseOutPanel = false,
        };
    }
}
