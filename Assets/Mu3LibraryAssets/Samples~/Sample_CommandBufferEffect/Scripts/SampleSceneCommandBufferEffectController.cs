using Mu3Library.PostEffect.CommandBufferEffect;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.Demo.CommandBuffer {
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

        private CameraEdgeDetectBuffer cameraEdgeDetectBuffer;
        [Space(20), SerializeField] private CameraEvent cameraEdgeDetectEvent = CameraEvent.AfterEverything;
        [SerializeField] private Color cameraEdgeDetectEdgeColor = Color.black;
        [SerializeField, Range(0.0f, 1.0f)] private float cameraEdgeDetectFactor = 0.1f;
        [SerializeField, Range(0.0f, 10.0f)] private float cameraEdgeDetectEdgeThickness = 2.0f;

        private CameraToonBuffer cameraToonBuffer;
        [Space(20), SerializeField] private CameraEvent cameraToonEvent = CameraEvent.AfterGBuffer;
        [SerializeField, Range(0, 5)] private float cameraToonShadingStep = 2;

        private LightToonBuffer lightToonBuffer;
        [Space(20), SerializeField] private LightEvent lightToonEvent = LightEvent.AfterShadowMap;
        [SerializeField, Range(0, 5)] private float lightToonShadingStep = 2;



        private void OnDestroy() {
            ClearCameraBuffer(cameraGrayScaleBuffer);
            ClearCameraBuffer(cameraBlurBuffer);
            ClearCameraBuffer(cameraEdgeDetectBuffer);
            ClearCameraBuffer(cameraToonBuffer);

            ClearLightBuffer(lightToonBuffer);
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
                if(cameraEdgeDetectBuffer == null) {
                    cameraEdgeDetectBuffer = new CameraEdgeDetectBuffer();
                    cameraEdgeDetectBuffer.Init(bufferCamera, cameraEdgeDetectEvent);
                }
                else {
                    cameraEdgeDetectBuffer.Clear();
                    cameraEdgeDetectBuffer = null;
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

            if(Input.GetKeyDown(KeyCode.F)) {
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
            if(cameraEdgeDetectBuffer != null) {
                cameraEdgeDetectBuffer.ChangeColor(cameraEdgeDetectEdgeColor);
                cameraEdgeDetectBuffer.ChangeFactor(cameraEdgeDetectFactor);
                cameraEdgeDetectBuffer.ChangeThickness(cameraEdgeDetectEdgeThickness);
            }
            if(cameraToonBuffer != null) {
                cameraToonBuffer.ChangeShadingStep(cameraToonShadingStep);
            }

            if(lightToonBuffer != null) {
                lightToonBuffer.ChangeShadingStep(lightToonShadingStep);
            }
        }

        private void ClearCameraBuffer(CameraBuffer buffer) {
            if(buffer != null) {
                buffer.Clear();
            }
        }

        private void ClearLightBuffer(LightBuffer buffer) {
            if(buffer != null) {
                buffer.Clear();
            }
        }
    }
}