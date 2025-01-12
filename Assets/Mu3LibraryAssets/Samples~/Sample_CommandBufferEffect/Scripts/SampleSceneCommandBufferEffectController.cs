using Mu3Library.PostEffect.CommandBufferEffect;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.Demo.CommandBuffer {
    /*
    [ 렌더링 파이프라인 순서 ]
    
    1. OnPreCull: 카메라의 시야에서 렌더링할 오브젝트를 결정하기 전에 호출.
    2. OnPreRender: 카메라 렌더링 직전에 호출.
    3. 실제 렌더링 작업 (GPU에서 처리).
    4. OnPostRender: 렌더링이 끝난 직후 호출.
    5. OnRenderImage: 카메라의 렌더 타겟에 대한 후처리가 필요한 경우 호출.
    */
    public class SampleSceneCommandBufferController : MonoBehaviour {
        [SerializeField] private Camera bufferCamera;
        [SerializeField] private Light bufferLight;
        [SerializeField] private GameObject[] cubes;

        private CameraGrayScaleBuffer cameraGrayScaleBuffer;
        [Space(20), SerializeField] private CameraEvent cameraGrayScaleEvent = CameraEvent.AfterEverything;
        [SerializeField, Range(0.0f, 1.0f)] private float cameraGrayScaleStrength = 1.0f;

        private CameraBlurBuffer cameraBlurBuffer;
        [Space(20), SerializeField] private CameraEvent cameraBlurEvent = CameraEvent.AfterEverything;
        [SerializeField, Range(0.0f, 1.0f)] private float cameraBlurStrength = 1.0f;
        [SerializeField, Range(0.0f, 10.0f)] private float cameraBlurAmount = 5.0f;
        [SerializeField, Range(0, 10)] private int cameraBlurKernelSize = 2;

        private CameraEdgeDetectBuffer caneraEdgeDetectBuffer;
        [Space(20), SerializeField] private CameraEvent cameraEdgeDetectEvent = CameraEvent.AfterEverything;
        [SerializeField] private Color cameraEdgeDetectEdgeColor = Color.black;
        [SerializeField, Range(0.0f, 1.0f)] private float cameraEdgeDetectFactor = 0.1f;
        [SerializeField, Range(0.0f, 10.0f)] private float cameraEdgeDetectEdgeThickness = 2.0f;

        private CameraToonBuffer cameraToonBuffer;
        [Space(20), SerializeField] private CameraEvent cameraToonEvent = CameraEvent.BeforeForwardOpaque;

        private LightToonBuffer lightToonBuffer;
        [Space(20), SerializeField] private LightEvent lightToonEvent = LightEvent.AfterShadowMap;



        private void OnDestroy() {
            if(cameraGrayScaleBuffer != null) {
                cameraGrayScaleBuffer.Clear();
            }
            if(cameraBlurBuffer != null) {
                cameraBlurBuffer.Clear();
            }
            if(caneraEdgeDetectBuffer != null) {
                caneraEdgeDetectBuffer.Clear();
            }
            if(cameraToonBuffer != null) {
                cameraToonBuffer.Clear();
            }

            if(lightToonBuffer != null) {
                lightToonBuffer.Clear();
            }
        }

        private void Update() {
            for(int i = 0; i < cubes.Length; i++) {
                cubes[i].transform.Rotate(Vector3.one * (i + 1) * 5 * Time.deltaTime);
            }

            if(Input.GetKeyDown(KeyCode.Q)) {
                if(cameraGrayScaleBuffer == null) {
                    cameraGrayScaleBuffer = new CameraGrayScaleBuffer();
                    cameraGrayScaleBuffer.Init(bufferCamera, cameraGrayScaleEvent);
                }
                else {
                    cameraGrayScaleBuffer.Clear();
                    cameraGrayScaleBuffer = null;
                }
            }
            if(Input.GetKeyDown(KeyCode.W)) {
                if(cameraBlurBuffer == null) {
                    cameraBlurBuffer = new CameraBlurBuffer();
                    cameraBlurBuffer.Init(bufferCamera, cameraBlurEvent);
                }
                else {
                    cameraBlurBuffer.Clear();
                    cameraBlurBuffer = null;
                }
            }
            if(Input.GetKeyDown(KeyCode.E)) {
                if(caneraEdgeDetectBuffer == null) {
                    caneraEdgeDetectBuffer = new CameraEdgeDetectBuffer();
                    caneraEdgeDetectBuffer.Init(bufferCamera, cameraEdgeDetectEvent);
                }
                else {
                    caneraEdgeDetectBuffer.Clear();
                    caneraEdgeDetectBuffer = null;
                }
            }
            if(Input.GetKeyDown(KeyCode.R)) {
                if(cameraToonBuffer == null) {
                    cameraToonBuffer = new CameraToonBuffer();
                    cameraToonBuffer.Init(bufferCamera, cameraToonEvent);
                }
                else {
                    cameraToonBuffer.Clear();
                    cameraToonBuffer = null;
                }
            }

            if(Input.GetKeyDown(KeyCode.A)) {
                if(lightToonBuffer == null) {
                    lightToonBuffer = new LightToonBuffer();
                    lightToonBuffer.Init(bufferLight, lightToonEvent);
                }
                else {
                    lightToonBuffer.Clear();
                    lightToonBuffer = null;
                }
            }

            if(cameraGrayScaleBuffer != null) {
                cameraGrayScaleBuffer.ChangeStrength(cameraGrayScaleStrength);
            }
            if(cameraBlurBuffer != null) {
                cameraBlurBuffer.ChangeStrength(cameraBlurStrength);
                cameraBlurBuffer.ChangeAmount(cameraBlurAmount);
                cameraBlurBuffer.ChangeKernelSize(cameraBlurKernelSize);
            }
            if(caneraEdgeDetectBuffer != null) {
                caneraEdgeDetectBuffer.ChangeColor(cameraEdgeDetectEdgeColor);
                caneraEdgeDetectBuffer.ChangeFactor(cameraEdgeDetectFactor);
                caneraEdgeDetectBuffer.ChangeThickness(cameraEdgeDetectEdgeThickness);
            }
            if(cameraToonBuffer != null) {

            }

            if(lightToonBuffer != null) {

            }
        }
    }
}