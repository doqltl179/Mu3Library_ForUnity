using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Portal {
    public class PortalRenderer : MonoBehaviour {
        private List<PortalLink> portals = new List<PortalLink>();
        [SerializeField] private int renderCountMax = 5;



        private void OnPreCull() {
            if(portals.Count == 0) {
                return;
            }

            foreach(PortalLink portal in portals) {
                portal.Render();
            }
        }

        #region Utility
        public void StartRendering(PortalLink beginPortal, Camera view = null) {
            foreach(PortalLink portal in portals) {
                portal.StopRender();
            }
            portals.Clear();

            beginPortal.CollectLinks(ref portals, view);
        }

        public void StopRendering() {
            foreach(PortalLink portal in portals) {
                portal.StopRender();
            }
            portals.Clear();
        }
        #endregion
    }
}
