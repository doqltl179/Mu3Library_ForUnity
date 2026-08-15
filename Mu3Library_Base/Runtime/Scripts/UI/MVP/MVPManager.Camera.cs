using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public partial class MVPManager
    {
        private Camera m_renderCamera = null;
        private Camera _renderCamera
        {
            get
            {
                if (m_renderCamera == null)
                {
                    GameObject go = new GameObject("RenderCamera");
                    go.transform.SetParent(_rootTransform);

                    Camera camera = go.AddComponent<Camera>();
                    camera.cullingMask = LayerMask.GetMask("UI");
                    camera.clearFlags = CameraClearFlags.Depth;

                    m_renderCamera = camera;
                }

                return m_renderCamera;
            }
        }
        public Camera RenderCamera => _renderCamera;



        /// <summary>
        /// Drops the render camera reference. The camera object itself hangs under the manager
        /// <br/> root and goes away with it.
        /// </summary>
        private void DisposeRenderCamera()
        {
            m_renderCamera = null;
        }

        public void RemoveCullingMask(string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                _renderCamera.cullingMask &= ~(1 << layerIndex);
            }
        }

        public void AddCullingMask(string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                _renderCamera.cullingMask |= 1 << layerIndex;
            }
        }

        public void SetCullingMask(params string[] layerNames)
        {
            int mask = 0;

            foreach (var layerName in layerNames)
            {
                int layerIndex = LayerMask.NameToLayer(layerName);
                if (layerIndex >= 0)
                {
                    mask |= 1 << layerIndex;
                }
            }

            RenderCamera.cullingMask = mask;
        }
    }
}
