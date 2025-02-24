using Mu3Library.Portal;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Demo.Portal {
    public class SampleScenePortalController : MonoBehaviour {
        [SerializeField] private PortalRenderer portalRenderer;
        [SerializeField] private PortalLink firstLink;



        private void Start() {
            portalRenderer.StartRendering(firstLink);
        }
    }
}