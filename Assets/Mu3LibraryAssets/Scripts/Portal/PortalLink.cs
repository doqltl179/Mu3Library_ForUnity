/*
 * [ 참고 링크 ]
 * https://youtu.be/cWpFZbjtSQg?si=Sf7pZHRo4OV-CsGa
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Portal {
    public class PortalLink : MonoBehaviour {
        private Camera viewCamera;

        public PortalLink LinkedTo => linkedTo;
        [SerializeField] private PortalLink linkedTo;

        [Space(20)]
        [SerializeField] private Camera portalCamera;
        [SerializeField] private MeshRenderer portalScreen;
        private RenderTexture portalRT;
        private Texture2D outBoundsTex;

        public Bounds ScreenBounds => portalScreen.bounds;

        public bool IsBoundsInCamera => isBoundsInCamera;
        private bool isBoundsInCamera = false;

        [Space(20)]
        [SerializeField] private Collider collider;



        private void Awake() {
            // Portal이 서로 연결되어 있는 상태가 아니라, 꼬리잡기를 하듯이 연결되어 있기 때문에
            // 아래의 코드를 실행하지 않으면 가장 마지막 "linkedTo"의 화면만이 비춰지게 된다.
            // 이를 막기 위해 모든 Portal이 자신의 "linkedTo"의 영역만 렌더링할 수 있도록 아래의 코드를 실행해준다.
            // "Ignore Render"는 임의로 설정한 Layer이며, 필요에 따라 바꾸어 사용해주자.
            portalScreen.gameObject.layer = LayerMask.NameToLayer("Ignore Render");
            portalCamera.cullingMask = ~(1 << portalScreen.gameObject.layer);

            // 수동 Render를 위해 enabled는 끈 상태로 카메라를 사용한다.
            portalCamera.enabled = false;
        }

        private void OnDestroy() {
            if(portalRT != null) {
                portalRT.Release();
                Destroy(portalRT);
            }
            if(outBoundsTex != null) {
                Destroy(outBoundsTex);
            }
        }

        private void Start() {
            if(linkedTo == null) {
                collider.enabled = false;
                portalScreen.enabled = false;
            }
        }

        #region Utility
        public void StopRender() {
            if(portalRT != null) {
                portalRT.Release();
                Destroy(portalRT);
                portalRT = null;
            }
            if(outBoundsTex != null) {
                Destroy(outBoundsTex);
                outBoundsTex = null;
            }

            portalCamera.targetTexture = null;
        }

        public void Render() {
            if(viewCamera == null || linkedTo == null) {
                return;
            }

            // RenderTexture 세팅
            if(portalRT != null) {
                if(portalRT.width != Screen.width || portalRT.height != Screen.height) {
                    portalRT.Release();
                    Destroy(portalRT);
                    portalRT = null;
                }
            }
            if(portalRT == null) {
                portalRT = new RenderTexture(Screen.width, Screen.height, 0);

                portalCamera.targetTexture = portalRT;
            }
            if(outBoundsTex == null) {
                outBoundsTex = new Texture2D(1, 1);
                outBoundsTex.SetPixel(0, 0, Color.red);
                outBoundsTex.Apply();
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(viewCamera);
            bool boundsTest = GeometryUtility.TestPlanesAABB(planes, portalScreen.bounds);
            if(boundsTest != isBoundsInCamera) {
                if(boundsTest) {
                    portalScreen.material.SetTexture("_MainTex", portalRT);
                }
                else {
                    portalScreen.material.SetTexture("_MainTex", outBoundsTex);
                }

                isBoundsInCamera = boundsTest;
            }

            if(!isBoundsInCamera) {
                return;
            }

            // 이 과정을 거쳐야 Portal에 화면에 제대로 렌더링 된다.
            // 이 작업을 하지 않았을 때 Portal 화면에 잔상이 생긴다.
            portalScreen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            // [ 동작 순서 ]
            // viewCamera를 월드 공간에 투영한다.
            // 월드 공간에 투영된 viewCamera를 linkedTo의 로컬 공간에 투영한다.
            // linkedTo의 로컬 공간으로 투영된 값을 현재 객체의 월드 공간에 투영한다.
            Matrix4x4 m = linkedTo.transform.localToWorldMatrix *
                transform.worldToLocalMatrix *
                viewCamera.transform.localToWorldMatrix;
            portalCamera.transform.SetPositionAndRotation(m.GetPosition(), m.rotation);

            // 수동 렌더링
            portalCamera.Render();

            // 이전 작업 해제
            portalScreen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void SetPortalLink(PortalLink link) {
            if(link != null) {
                collider.enabled = true;
                portalScreen.enabled = true;
            }
            else {
                collider.enabled = false;
                portalScreen.enabled = false;
            }

            linkedTo = link;
        }

        public void CollectLinks(ref List<PortalLink> portals, Camera view = null) {
            if(portals == null) {
                portals = new List<PortalLink>();
            }
            else if(portals.Any(t => t == this)) {
                return;
            }

            viewCamera = view;
            if(viewCamera == null) {
                viewCamera = Camera.main;
                if(viewCamera == null) {
                    Debug.LogWarning($"Main Camera not found.");
                }
            }

            portals.Add(this);
            if(linkedTo != null) {
                linkedTo.CollectLinks(ref portals, viewCamera);
            }
        }
        #endregion
    }
}
