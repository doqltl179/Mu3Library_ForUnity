using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public partial interface IMVPManager
    {
        public Camera RenderCamera { get; }

        public void RemoveCullingMask(string layerName);
        public void AddCullingMask(string layerName);
        public void SetCullingMask(params string[] layerNames);
    }
}
