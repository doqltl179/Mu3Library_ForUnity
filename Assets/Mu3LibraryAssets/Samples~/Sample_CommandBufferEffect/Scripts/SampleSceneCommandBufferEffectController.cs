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

        private CameraBuffer grayScaleBuffer;
        [Space(20), SerializeField] private CameraEvent grayScaleCameraEvent = CameraEvent.AfterEverything;
        [SerializeField, Range(0.0f, 1.0f)] private float grayScaleStrength = 1.0f;

        private CameraBuffer blurBuffer;
        [Space(20), SerializeField] private CameraEvent blurCameraEvent = CameraEvent.AfterEverything;
        [SerializeField, Range(0.0f, 1.0f)] private float blurStrength = 1.0f;
        [SerializeField, Range(0.0f, 10.0f)] private float blurAmount = 5.0f;
        [SerializeField, Range(0, 10)] private int blurKernelSize = 2;

        private CameraBuffer edgeDetectBuffer;
        [Space(20), SerializeField] private CameraEvent edgeDetectCameraEvent = CameraEvent.AfterEverything;
        [SerializeField] private Color edgeDetectEdgeColor = Color.black;
        [SerializeField, Range(0.0f, 1.0f)] private float edgeDetectFactor = 0.1f;
        [SerializeField, Range(0.0f, 10.0f)] private float edgeDetectEdgeThickness = 2.0f;



        private void OnDestroy() {
            if(grayScaleBuffer != null) {
                grayScaleBuffer.Clear();
            }
            if(blurBuffer != null) {
                blurBuffer.Clear();
            }
            if(edgeDetectBuffer != null) {
                edgeDetectBuffer.Clear();
            }
        }

        private void Update() {
            for(int i = 0; i < cubes.Length; i++) {
                cubes[i].transform.Rotate(Vector3.one * (i + 1) * 5 * Time.deltaTime);
            }

            if(Input.GetKeyDown(KeyCode.Q)) {
                if(grayScaleBuffer == null) {
                    grayScaleBuffer = new CameraBuffer("GrayScale", bufferCamera, grayScaleCameraEvent, Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/GrayScale"));
                }
                else {
                    grayScaleBuffer.Clear();
                    grayScaleBuffer = null;
                }
            }
            if(Input.GetKeyDown(KeyCode.W)) {
                if(blurBuffer == null) {
                    blurBuffer = new CameraBuffer("Blur", bufferCamera, blurCameraEvent, Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/Blur"));
                }
                else {
                    blurBuffer.Clear();
                    blurBuffer = null;
                }
            }
            if(Input.GetKeyDown(KeyCode.E)) {
                if(edgeDetectBuffer == null) {
                    edgeDetectBuffer = new CameraBuffer("Edge Detect", bufferCamera, edgeDetectCameraEvent, Shader.Find("Mu3Library/PostEffect/CommandBufferEffect/EdgeDetect"));
                }
                else {
                    edgeDetectBuffer.Clear();
                    edgeDetectBuffer = null;
                }
            }

            if(grayScaleBuffer != null) {
                grayScaleBuffer.ChangeProperty("_Strength", grayScaleStrength);
            }
            if(blurBuffer != null) {
                blurBuffer.ChangeProperty("_Strength", blurStrength);
                blurBuffer.ChangeProperty("_BlurAmount", blurAmount);
                blurBuffer.ChangeProperty("_KernelSize", blurKernelSize);
            }
            if(edgeDetectBuffer != null) {
                edgeDetectBuffer.ChangeProperty("_EdgeColor", edgeDetectEdgeColor);
                edgeDetectBuffer.ChangeProperty("_EdgeFactor", edgeDetectFactor);
                edgeDetectBuffer.ChangeProperty("_EdgeThickness", edgeDetectEdgeThickness);
            }
        }
    }
}