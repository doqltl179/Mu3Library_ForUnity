/* --------------------------------------------------------------------------
 * Do not set properties 'chunkCount', 'chunkSize' to max at same time!!
 --------------------------------------------------------------------------*/

using Mu3Library.Attribute;
using Mu3Library.CameraUtil;
using Mu3Library.MarchingCubes;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Demo.MarchingCubes {
    public class SampleSceneMarchingCubesController : MonoBehaviour {
        [SerializeField] private MarchingCubesGenerator marchingCubes;
        [SerializeField, Range(1, 16)] private int chunkCount = 1;
        [SerializeField, Range(2, 100)] private int chunkSize = 10;
        [SerializeField] private bool useCollider = false;
        [SerializeField] private ForceUpdateType marchingCubesForceUpdateType = ForceUpdateType.Cube;
        [SerializeField] private PointWeightEditMode marchingCubesEditMode = PointWeightEditMode.Increase;

        [Title("Edit Settings")]
        [SerializeField] private SimpleCameraFreeView cameraFreeView;
        [SerializeField] private Camera camera;
        [SerializeField] private MeshRenderer editSphereRenderer;
        private Ray camRay;
        private RaycastHit camHit;
        private bool isCamRayHit = false;

        [Space(20)]
        [SerializeField, Range(0.1f, 20.0f)] private float editSphereRadius = 10;
        [SerializeField] private Color editSphereIncreaseColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color editSphereDecreaseColor = new Color(1, 0, 0, 0.3f);



        private void Start() {
            marchingCubes.CreateChunks(chunkCount, chunkSize, useCollider);

            marchingCubes.ForceUpdateMarchingCubes(marchingCubesForceUpdateType);

            if(camera == null) {
                camera = Camera.main;
            }
            cameraFreeView.Init(camera);
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Space)) {
                marchingCubes.Clear();

                marchingCubes.CreateChunks(chunkCount, chunkSize, useCollider);

                marchingCubes.ForceUpdateMarchingCubes(marchingCubesForceUpdateType);
            }

            if(camera != null) {
                camRay = camera.ScreenPointToRay(Input.mousePosition);
                isCamRayHit = Physics.Raycast(camRay, out camHit, 1000);
            }

            if(Input.GetKeyDown(KeyCode.Tab)) {
                System.Array modeArray = System.Enum.GetValues(typeof(PointWeightEditMode));

                int mode = (int)marchingCubesEditMode;
                mode++;
                if(mode >= modeArray.Length) {
                    mode -= modeArray.Length;
                }

                marchingCubesEditMode = (PointWeightEditMode)modeArray.GetValue(mode);
            }

            if(editSphereRenderer != null) {
                if(marchingCubesEditMode == PointWeightEditMode.Increase) {
                    editSphereRenderer.sharedMaterial.color = editSphereIncreaseColor;
                }
                else if(marchingCubesEditMode == PointWeightEditMode.Decrease) {
                    editSphereRenderer.sharedMaterial.color = editSphereDecreaseColor;
                }

                if(isCamRayHit && marchingCubesEditMode != PointWeightEditMode.None) {
                    editSphereRenderer.gameObject.SetActive(true);

                    editSphereRadius += Input.mouseScrollDelta.y * Time.deltaTime * 100;
                    editSphereRadius = Mathf.Clamp(editSphereRadius, 0.1f, 20);

                    editSphereRenderer.transform.position = camHit.point;
                    editSphereRenderer.transform.localScale = Vector3.one * editSphereRadius * 2;

                    if(Input.GetKey(KeyCode.Mouse0)) {
                        marchingCubes.EditChunkPointWeight(marchingCubesEditMode, camHit.point, editSphereRadius);
                    }
                }
                else {
                    editSphereRenderer.gameObject.SetActive(false);
                }
            }
        }
    }
}