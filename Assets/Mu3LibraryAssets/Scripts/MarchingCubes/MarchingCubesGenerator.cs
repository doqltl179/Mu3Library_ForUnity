using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.MarchingCubes {
    public enum ForceUpdateType {
        Cube = 0, 

        Sphere = 1, 
        Noise = 2, 
    }

    /// <summary>
    /// <br/> 반드시 'chunkSize'를 기준으로 정 육면체의 Chunk를 만든다.
    /// </summary>
    public class MarchingCubesGenerator : MonoBehaviour {
        private const int numThreads = 8;

        [SerializeField] private ComputeShader marchingCubesCS;
        private int kernelIndex_March = -1;
        private int kernelIndex_ShapeToCube = -1;

        /// <summary>
        /// <br/> MarchingCubes 한 변에 들어가는 chunk의 개수
        /// <br/> 가로 == 세로 == 높이
        /// </summary>
        public int ChunkCount => chunkCount;
        private int chunkCount = -1;

        /// <summary>
        /// <br/> chunk 하나의 길이
        /// <br/> 가로 == 세로 == 높이
        /// </summary>
        [Space(20)]
        [SerializeField, Range(2, 64)] private int chunkSize = 8;

        [Space(20)]
        [SerializeField, Range(0.01f, 1.0f)] private float pointWeightThreshold = 0.1f;
        [SerializeField, Range(0.01f, 10.0f)] private float pointWeightSensitive = 1.0f;

        [Space(20)]
        [SerializeField] private bool usePlayerBoundary = false;

        [ConditionalHide(nameof(usePlayerBoundary), true)]
        [SerializeField, Range(1, 5)] private int playerBoundary = 2;

        private Chunk[,,] chunks = null;
        private Queue<Chunk> chunkPool = new Queue<Chunk>();

        private ComputeBuffer pointsWeightBuffer = null;
        private ComputeBuffer trianglesBuffer = null;
        /// <summary>
        /// 'trianglesBuffer'에서 추가된 triangle의 개수를 구하기 위함.
        /// </summary>
        private ComputeBuffer triangleCountBuffer = null;



        private void OnDestroy() {
            Clear();
        }

        #region Utility
        public void ForceUpdateMarchingCubes(ForceUpdateType type) {
            if(chunks == null) {
                return;
            }

            switch(type) {
                case ForceUpdateType.Cube: ForceUpdateMarchingCubesToCube(); break;

            }
        }

        /// <summary>
        /// <br/> chunkCount: 한 변에 들어가는 chunk의 개수
        /// </summary>
        public void CreateChunks(int chunkCount) {
            if(this.chunkCount == chunkCount) {
                return;
            }

            EnqueueAllChunks();

            chunks = new Chunk[chunkCount, chunkCount, chunkCount];
            for(int z = 0; z < chunkCount; z++) {
                for(int y = 0; y < chunkCount; y++) {
                    for(int x = 0; x < chunkCount; x++) {
                        Chunk chunk = null;
                        if(chunkPool.Count > 0) {
                            chunk = chunkPool.Dequeue();
                        }
                        else {
                            GameObject go = new GameObject();
                            go.transform.SetParent(transform);

                            chunk = go.AddComponent<Chunk>();
                        }
                        chunk.gameObject.name = $"Chunk_{x}_{y}_{z}";

                        chunk.Init(x, y, z, chunkSize);
                        chunk.transform.position = new Vector3(x, y, z) * chunkSize;

                        chunks[x, y, z] = chunk;
                    }
                }
            }

            // kernel index 초기화
            kernelIndex_March = marchingCubesCS.FindKernel("March");
            if(kernelIndex_March < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: March");

                return;
            }
            kernelIndex_ShapeToCube = marchingCubesCS.FindKernel("ShapeToCube");
            if(kernelIndex_ShapeToCube < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: ShapeToCube");

                return;
            }

            marchingCubesCS.SetInt("chunkCount", chunkCount);
            marchingCubesCS.SetInt("chunkSize", chunkSize);
            marchingCubesCS.SetFloat("pointWeightThreshold", pointWeightThreshold);

            // 처음에만 Buffer를 추가한다.
            if(pointsWeightBuffer == null) {
                int pointCount = chunkSize + 1;
                int pointCountAll = pointCount * pointCount * pointCount;
                pointsWeightBuffer = new ComputeBuffer(pointCountAll, sizeof(float));

                marchingCubesCS.SetBuffer(kernelIndex_March, "pointsWeight", pointsWeightBuffer); // 읽기 전용
                marchingCubesCS.SetBuffer(kernelIndex_ShapeToCube, "pointsWeight", pointsWeightBuffer);
            }
            if(trianglesBuffer == null) {
                // 만들 수 있는 Triangle의 최대 개수
                // 하나의 cube에서 최대 5개의 triangle을 만든다.
                int triangleCountMax = chunkSize * chunkSize * chunkSize * 5;
                // Vector3(= float * 3) * 3
                trianglesBuffer = new ComputeBuffer(triangleCountMax, sizeof(float) * 3 * 3, ComputeBufferType.Append);

                marchingCubesCS.SetBuffer(kernelIndex_March, "triangles", trianglesBuffer);
            }
            if(triangleCountBuffer == null) {
                triangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            }

            this.chunkCount = chunkCount;
        }

        public void Clear() {
            if(chunks != null) {
                Chunk currentChunk = null;
                for(int z = 0; z < chunkCount; z++) {
                    for(int y = 0; y < chunkCount; y++) {
                        for(int x = 0; x < chunkCount; x++) {
                            currentChunk = chunks[x, y, z];
                            if(currentChunk != null) {
                                Destroy(currentChunk.gameObject);
                            }
                        }
                    }
                }
            }

            if(chunkPool.Count > 0) {
                Chunk currentChunk = null;
                while(chunkPool.Count == 0) {
                    currentChunk = chunkPool.Dequeue();
                    if(currentChunk != null) {
                        Destroy(currentChunk.gameObject);
                    }
                }

                chunkPool.Clear();
            }

            chunkCount = -1;

            if(pointsWeightBuffer != null) {
                pointsWeightBuffer.Release();
                pointsWeightBuffer = null;
            }
            if(trianglesBuffer != null) {
                trianglesBuffer.Release();
                trianglesBuffer = null;
            }
        }
        #endregion

        private void ForceUpdateMarchingCubesToCube() {
            int pointCount = chunkSize + 1;
            int pointCountAll = pointCount * pointCount * pointCount;

            float[] pointsWeight = new float[pointCountAll];
            int[] triangleCountArray = new int[1];

            int threadGroup = Mathf.CeilToInt(pointCount / (float)numThreads);

            Chunk currentChunk = null;
            for(int z = 0; z < chunkCount; z++) {
                for(int y = 0; y < chunkCount; y++) {
                    for(int x = 0; x < chunkCount; x++) {
                        currentChunk = chunks[x, y, z];
                        if(currentChunk == null) {
                            continue;
                        }

                        marchingCubesCS.SetInts("chunkIndex", x, y, z);

                        #region Calculate And Update Points Weight
                        marchingCubesCS.Dispatch(kernelIndex_ShapeToCube, threadGroup, threadGroup, threadGroup);

                        pointsWeightBuffer.GetData(pointsWeight);

                        currentChunk.ForceUpdatePointsWeight(pointsWeight);

                        #endregion

                        #region Calculate And Update Mesh

                        // Append Buffer이기 때문에 매 chunk 마다 triangles의 계산 시작 index를 0으로 한다.
                        trianglesBuffer.SetCounterValue(0);

                        marchingCubesCS.Dispatch(kernelIndex_March, threadGroup, threadGroup, threadGroup);

                        ComputeBuffer.CopyCount(trianglesBuffer, triangleCountBuffer, 0);
                        triangleCountBuffer.GetData(triangleCountArray);
                        int triangleCount = triangleCountArray[0];

                        Triangle[] triangles = new Triangle[triangleCount];
                        trianglesBuffer.GetData(triangles);

                        Vector3[] verts = new Vector3[triangleCount * 3];
                        int[] tris = new int[verts.Length];
                        for(int i = 0; i < triangles.Length; i++) {
                            int idx = i * 3;

                            verts[idx + 0] = triangles[i].a;
                            verts[idx + 1] = triangles[i].b;
                            verts[idx + 2] = triangles[i].c;

                            tris[idx + 0] = idx + 0;
                            tris[idx + 1] = idx + 1;
                            tris[idx + 2] = idx + 2;
                        }

                        currentChunk.UpdateMesh(verts, tris);

                        #endregion
                    }
                }
            }
        }

        private void EnqueueAllChunks() {
            if(chunks != null) {
                for(int z = 0; z < chunkCount; z++) {
                    for(int y = 0; y < chunkCount; y++) {
                        for(int x = 0; x < chunkCount; x++) {
                            Chunk chunk = chunks[x, y, z];
                            if(chunk != null) {
                                chunkPool.Enqueue(chunk);
                            }
                        }
                    }
                }

                chunks = null;
            }
        }



        struct Triangle {
#pragma warning disable 649 // disable unassigned variable warning
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;

            public Vector3 this[int i] {
                get {
                    switch(i) {
                        case 0:
                            return a;
                        case 1:
                            return b;
                        default:
                            return c;
                    }
                }
            }
        }
    }
}
