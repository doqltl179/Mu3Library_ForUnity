using Mu3Library.MarchingCube;
using System.Collections;
using UnityEngine;

namespace Mu3Library.Demo.MarchingCube {
    /*
     * 싱글 스레드로 생성할 때는 Cube의 사이즈가 (16, 16, 16) 정도만 되도 엄청 버벅인다.
     */
    public class SampleSceneMarchingCubeController : MonoBehaviour {
        [SerializeField] private MarchingCubeGenerator generator;
        [SerializeField] private bool setRandomHeight = true;

        [Space(20)]
        [SerializeField, Range(MarchingCubeGenerator.CubeWidthMin, MarchingCubeGenerator.CubeWidthMax)] private int cubeWidth = 8;
        [SerializeField, Range(MarchingCubeGenerator.CubeHeightMin, MarchingCubeGenerator.CubeHeightMax)] private int cubeHeight = 8;
        [SerializeField, Range(MarchingCubeGenerator.CubeDepthMin, MarchingCubeGenerator.CubeDepthMax)] private int cubeDepth = 8;

        private enum CubeEditMode {
            None = 0,
            Increase = 1, 
            Decrease = 2, 
        }
        [Space(20)]
        [SerializeField] private CubeEditMode editMode = CubeEditMode.None;

        [SerializeField, Range(0.1f, 5.0f)] private float editGuideRadius = 0.5f;
        [SerializeField, Range(1.0f, 100.0f)] private float editGuideDistanceMax = 20.0f;
        private Color normalColor = new Color(1, 1, 1, 0.25f);
        private Color increaseColor = new Color(0, 1.0f, 0, 0.25f);
        private Color decreaseColor = new Color(1.0f, 0, 0, 0.25f);

        [Space(20)]
        [SerializeField] private MeshRenderer editGuideBall = null;
        private Camera camera;



        private IEnumerator Start() {
            if(generator == null) {
                GameObject go = new GameObject(typeof(MarchingCubeGenerator).Name);
                go.transform.position = Vector3.zero;

                generator = go.AddComponent<MarchingCubeGenerator>();
            }

            if(editGuideBall == null) {
                Debug.LogError($"Guide Ball not found");

                enabled = false;

                yield break;
            }

            if(camera == null) {
                camera = Camera.main;
                if(camera == null) {
                    Debug.LogError("Camera not found.");

                    enabled = false;
                }
            }

            yield return null;

            generator.GenerateMarchingCube(cubeWidth, cubeHeight, cubeDepth, setRandomHeight);
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Space)) {
                generator.Clear();
                generator.GenerateMarchingCube(cubeWidth, cubeHeight, cubeDepth, setRandomHeight);
            }

            if(Input.GetKeyDown(KeyCode.Tab)) {
                System.Array modeArray = System.Enum.GetValues(typeof(CubeEditMode));

                int modeValue = (int)editMode + 1;
                modeValue = modeValue % modeArray.Length;

                editMode = (CubeEditMode)modeArray.GetValue(modeValue);
            }

            switch(editMode) {
                case CubeEditMode.None: {
                        if(editGuideBall != null) {
                            editGuideBall.gameObject.SetActive(false);
                        }
                    }
                    break;

                case CubeEditMode.Increase: {
                        if(editGuideBall != null) {
                            editGuideBall.gameObject.SetActive(true);

                            editGuideBall.transform.localScale = Vector3.one * editGuideRadius * 2;
                        }

                        MouseEvent();
                    }
                    break;

                case CubeEditMode.Decrease: {
                        if(editGuideBall != null) {
                            editGuideBall.gameObject.SetActive(true);

                            editGuideBall.transform.localScale = Vector3.one * editGuideRadius * 2;
                        }

                        MouseEvent();
                    }
                    break;
            }
        }

        private Ray mouseScreenRay;
        private RaycastHit mouseScreenHit;
        private void MouseEvent() {
            if(camera == null || editGuideBall == null) {
                return;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            editGuideRadius = Mathf.Clamp(editGuideRadius + scroll * Time.deltaTime * 5.0f, 0.01f, 5.0f);

            mouseScreenRay = camera.ScreenPointToRay(Input.mousePosition);
            bool isHitMarchingCube = false;
            if(Physics.Raycast(mouseScreenRay, out mouseScreenHit, editGuideDistanceMax)) {
                if(mouseScreenHit.collider.gameObject == generator.gameObject) {
                    isHitMarchingCube = true;
                }
            }

            Color guideBallColor = normalColor;

            if(isHitMarchingCube) {
                editGuideBall.transform.position = mouseScreenHit.point;

                if(editMode == CubeEditMode.Increase) guideBallColor = increaseColor;
                else if(editMode == CubeEditMode.Decrease) guideBallColor = decreaseColor;
            }
            else {
                editGuideBall.transform.position = camera.transform.position + mouseScreenRay.direction * editGuideDistanceMax;
            }

            editGuideBall.material.color = guideBallColor;

            if(Input.GetKey(KeyCode.Mouse0)) {
                EditMarchingCube();
            }
        }

        private void EditMarchingCube() {
            if(editMode == CubeEditMode.Increase) {
                generator.IncreasePointHeight(editGuideBall.transform.position, editGuideRadius);
            }
            else if(editMode == CubeEditMode.Decrease) {
                generator.DecreasePointHeight(editGuideBall.transform.position, editGuideRadius);
            }
        }
    }
}