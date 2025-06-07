using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Sample.RenderingPipeline {
    /*
    [ 렌더링 파이프라인 순서 ]

    1. OnPreCull: 카메라의 시야에서 렌더링할 오브젝트를 결정하기 전에 호출.
    2. OnPreRender: 카메라 렌더링 직전에 호출.
    3. 실제 렌더링 작업 (GPU에서 처리).
    4. OnPostRender: 렌더링이 끝난 직후 호출.
    5. OnRenderImage: 카메라의 렌더 타겟에 대한 후처리가 필요한 경우 호출.
    */
    /*
    "OnRenderImage"는 카메라에 추가된 스크립트에서만 작동한다.
    */
    public class SampleSceneRenderingPipelineController : MonoBehaviour {
        [SerializeField] private GameObject[] cubes;

        [Title("Debug")]
        [SerializeField] private bool showGBuffer = false;

        private Material gbufferMat = null;
        [SerializeField, Range(0, 7)] private int _gbufferIndex;

        private Material toonMat = null;



        private void Start() {
            gbufferMat = new Material(Shader.Find("Mu3Library/GBufferVisualizer"));

            toonMat = new Material(Shader.Find("Mu3Library/PostEffect/RenderingPipeline/Toon"));
        }

        private void Update() {
            for(int i = 0; i < cubes.Length; i++) {
                cubes[i].transform.Rotate(Vector3.one * (i + 1) * 5 * Time.deltaTime);
            }

            int currentGBufferIdx = (int)gbufferMat.GetFloat("_GBufferIndex");
            if(currentGBufferIdx != _gbufferIndex) {
                gbufferMat.SetFloat("_GBufferIndex", _gbufferIndex);
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            if(showGBuffer) {
                Graphics.Blit(source, destination, gbufferMat);
            }
            else {
                Graphics.Blit(source, destination, toonMat);
            }
        }
    }
}