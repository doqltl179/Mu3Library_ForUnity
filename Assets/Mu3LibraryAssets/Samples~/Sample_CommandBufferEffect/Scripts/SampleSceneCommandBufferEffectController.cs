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

        private CameraEdgeDetectBuffer caneraEdgeDetectBuffer;
        [Space(20), SerializeField] private CameraEvent cameraEdgeDetectEvent = CameraEvent.AfterEverything;
        [SerializeField] private Color cameraEdgeDetectEdgeColor = Color.black;
        [SerializeField, Range(0.0f, 1.0f)] private float cameraEdgeDetectFactor = 0.1f;
        [SerializeField, Range(0.0f, 10.0f)] private float cameraEdgeDetectEdgeThickness = 2.0f;



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
        }
    }
}