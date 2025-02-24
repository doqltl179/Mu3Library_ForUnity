/*
 * [ 참고 링크 ]
 * https://youtu.be/M3iI2l0ltbE?si=3iL8gFXowwWe3jxA
 */

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

    public enum PointWeightEditMode {
        None = 0,

        Increase = 1, 
        Decrease = 2,
    }

    /// <summary>
    /// <br/> 반드시 'chunkSize'를 기준으로 정 육면체의 Chunk를 만든다.
    /// </summary>
    public class MarchingCubesGenerator : MonoBehaviour {
        private const int numThreads = 8;

        [SerializeField] private ComputeShader marchingCubesCS;
        private int kernelIndex_March = -1;
        private int kernelIndex_EditPointsWeight = -1;
        private int kernelIndex_ShapeToCube = -1;
        private int kernelIndex_ShapeToSphere = -1;
        private int kernelIndex_ShapeToNoise = -1;

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
        private int chunkSize = -1;

        private bool useCollider = false;

        [Space(20)]
        [SerializeField, Range(-1.0f, 1.0f)] private float pointWeightThreshold = 0.1f;
        [SerializeField, Range(0.01f, 10.0f)] private float pointWeightSensitive = 1.0f;

        [Space(20)]
        [SerializeField] private Material chunkMaterial = null;

        [Space(20)]
        [SerializeField] private bool usePlayerBoundary = false;

        [ConditionalHide(nameof(usePlayerBoundary), true)]
        [SerializeField, Range(1, 5)] private int playerBoundary = 2;

        private Chunk[,,] chunks = null;
        private Queue<Chunk> chunkPool = new Queue<Chunk>();

        private ComputeBuffer trianglesBuffer = null;
        /// <summary>
        /// 'trianglesBuffer'에서 추가된 triangle의 개수를 구하기 위함.
        /// </summary>
        private ComputeBuffer triangleCountBuffer = null;



        private void OnDestroy() {
            Clear();
        }

        #region Utility
        public void EditChunkPointWeight(PointWeightEditMode editMode, Vector3 worldPoint, float radius) {
            if(editMode == PointWeightEditMode.None) {
                return;
            }

            List<Chunk> areaChunks = GetChunksInArea(worldPoint, radius);
            if(areaChunks.Count == 0) {
                return;
            }

            int pointCount = chunkSize + 1;
            int threadGroup = Mathf.CeilToInt(pointCount / (float)numThreads);

            float editValue = pointWeightSensitive * Time.deltaTime;
            if(editMode == PointWeightEditMode.Decrease) {
                editValue *= -1;
            }

            marchingCubesCS.SetFloat("pointWeightEditValue", editValue);
            marchingCubesCS.SetFloats("editPoint", worldPoint.x, worldPoint.y, worldPoint.z);
            marchingCubesCS.SetFloat("editRadius", radius);

            Chunk currentChunk = null;
            for(int i = 0; i < areaChunks.Count; i++) {
                currentChunk = areaChunks[i];

                marchingCubesCS.SetInts("chunkIndex", currentChunk.CoordX, currentChunk.CoordY, currentChunk.CoordZ);

                EditChunkPointsWeight(currentChunk, threadGroup);

                SetChunkTriangles(currentChunk, threadGroup);
            }
        }

        public void ForceUpdateMarchingCubes(ForceUpdateType type) {
            if(chunks == null) {
                return;
            }

            System.DateTime startTime = System.DateTime.Now;

            switch(type) {
                case ForceUpdateType.Cube: ForceUpdateMarchingCubesToCube(); break;
                case ForceUpdateType.Sphere: ForceUpdateMarchingCubesToSphere(); break;
                case ForceUpdateType.Noise: ForceUpdateMarchingCubesToNoise(); break;

                default: {
                        Debug.LogWarning($"Undefined Type. type: {type}");
                    }
                    break;
            }

            System.DateTime endTime = System.DateTime.Now;
            Debug.Log($"Force Updated. time: {(endTime - startTime).TotalMilliseconds / 1000:F4}");
        }

        /// <summary>
        /// <br/> chunkCount: 한 변에 들어가는 chunk의 개수
        /// </summary>
        public void CreateChunks(int chunkCount, int chunkSize, bool useCollider) {
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

                        chunk.Init(x, y, z, chunkSize, useCollider, chunkMaterial);
                        chunk.transform.position = new Vector3(x, y, z) * chunkSize;

                        chunks[x, y, z] = chunk;
                    }
                }
            }

            // kernel index 초기화
            FindKernels();

            marchingCubesCS.SetInt("chunkCount", chunkCount);
            marchingCubesCS.SetInt("chunkSize", chunkSize);
            marchingCubesCS.SetFloat("pointWeightThreshold", pointWeightThreshold);

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
            this.chunkSize = chunkSize;
            this.useCollider = useCollider;
        }

        public void Clear() {
            // 모든 chunk 제거
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

            // pool에 있는 모든 chunk 제거
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

            // 프로퍼티 초기화
            chunkCount = -1;
            chunkSize = -1;
            useCollider = false;

            // Buffer 제거
            if(trianglesBuffer != null) {
                trianglesBuffer.Release();
                trianglesBuffer = null;
            }
            if(triangleCountBuffer != null) {
                triangleCountBuffer.Release();
                triangleCountBuffer = null;
            }
        }
        #endregion

        private void EditChunkPointsWeight(Chunk chunk, int threadGroup) {
            // weight 세팅 후에는 'SetChunkTriangles'가 불리기 때문에 
            // 'kernelIndex_March'에도 Buffer를 세팅해서 사용 가능하게 만들어준다.
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_March, "pointsWeight");
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_EditPointsWeight, "pointsWeight");

            marchingCubesCS.Dispatch(kernelIndex_EditPointsWeight, threadGroup, threadGroup, threadGroup);

            chunk.ForceUpdatePointsWeight();
        }

        private List<Chunk> GetChunksInArea(Vector3 worldPoint, float radius) {
            List<Chunk> result = new List<Chunk>();

            if(!useCollider) {
                Debug.LogWarning("Collider setting is 'False'");
            }
            else {
                // Raycast를 사용하면 Mesh가 생성되지 않은 Chunk가 잡히지 않게 된다.
                //RaycastHit[] hits = Physics.SphereCastAll(worldPoint, radius, Vector3.forward, 0);
                //for(int i = 0; i < hits.Length; i++) {
                //    Chunk hitChunk = hits[i].collider.GetComponent<Chunk>();
                //    if(hitChunk != null) {
                //        result.Add(hitChunk);
                //    }
                //}

                Vector3 pointBoundaryMin = worldPoint - Vector3.one * radius;
                Vector3 pointBoundaryMax = worldPoint + Vector3.one * radius;

                Vector3Int chunkIdxMin = new Vector3Int(
                    Mathf.Max(0, Mathf.FloorToInt(pointBoundaryMin.x / chunkSize)),
                    Mathf.Max(0, Mathf.FloorToInt(pointBoundaryMin.y / chunkSize)),
                    Mathf.Max(0, Mathf.FloorToInt(pointBoundaryMin.z / chunkSize)));
                Vector3Int chunkIdxMax = new Vector3Int(
                    Mathf.Min(chunkCount - 1, Mathf.FloorToInt(pointBoundaryMax.x / chunkSize)),
                    Mathf.Min(chunkCount - 1, Mathf.FloorToInt(pointBoundaryMax.y / chunkSize)),
                    Mathf.Min(chunkCount - 1, Mathf.FloorToInt(pointBoundaryMax.z / chunkSize)));

                for(int z = chunkIdxMin.z; z <= chunkIdxMax.z; z++) {
                    for(int y = chunkIdxMin.y; y <= chunkIdxMax.y; y++) {
                        for(int x = chunkIdxMin.x; x <= chunkIdxMax.x; x++) {
                            result.Add(chunks[x, y, z]);
                        }
                    }
                }
            }

            return result;
        }

        private void ForceUpdateMarchingCubesToNoise() {
            int pointCount = chunkSize + 1;
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

                        // chunk의 weight만을 세팅
                        SetPointsWeightShapeToNoise(currentChunk, threadGroup);

                        // 위에서 계산된 weight로 triangle 생성
                        SetChunkTriangles(currentChunk, threadGroup);
                    }
                }
            }
        }

        private void ForceUpdateMarchingCubesToSphere() {
            int pointCount = chunkSize + 1;
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

                        // chunk의 weight만을 세팅
                        SetPointsWeightShapeToSphere(currentChunk, threadGroup);

                        // 위에서 계산된 weight로 triangle 생성
                        SetChunkTriangles(currentChunk, threadGroup);
                    }
                }
            }
        }

        private void ForceUpdateMarchingCubesToCube() {
            int pointCount = chunkSize + 1;
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

                        // chunk의 weight만을 세팅
                        SetPointsWeightShapeToCube(currentChunk, threadGroup);

                        // 위에서 계산된 weight로 triangle 생성
                        SetChunkTriangles(currentChunk, threadGroup);
                    }
                }
            }
        }

        /// <summary>
        /// MarchingCubes의 weight 값에 noise 값을 넣는다.
        /// </summary>
        private void SetPointsWeightShapeToNoise(Chunk chunk, int threadGroup) {
            // weight 세팅 후에는 'SetChunkTriangles'가 불리기 때문에 
            // 'kernelIndex_March'에도 Buffer를 세팅해서 사용 가능하게 만들어준다.
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_March, "pointsWeight");
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_ShapeToNoise, "pointsWeight");

            marchingCubesCS.Dispatch(kernelIndex_ShapeToNoise, threadGroup, threadGroup, threadGroup);

            chunk.ForceUpdatePointsWeight();
        }

        /// <summary>
        /// MarchingCubes의 모양을 Sphere 형태로 만들기 위한 weight 세팅을 해준다.
        /// </summary>
        private void SetPointsWeightShapeToSphere(Chunk chunk, int threadGroup) {
            // weight 세팅 후에는 'SetChunkTriangles'가 불리기 때문에 
            // 'kernelIndex_March'에도 Buffer를 세팅해서 사용 가능하게 만들어준다.
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_March, "pointsWeight");
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_ShapeToSphere, "pointsWeight");

            marchingCubesCS.Dispatch(kernelIndex_ShapeToSphere, threadGroup, threadGroup, threadGroup);

            chunk.ForceUpdatePointsWeight();
        }

        /// <summary>
        /// MarchingCubes의 모양을 Cube 형태로 만들기 위한 weight 세팅을 해준다.
        /// </summary>
        private void SetPointsWeightShapeToCube(Chunk chunk, int threadGroup) {
            // weight 세팅 후에는 'SetChunkTriangles'가 불리기 때문에 
            // 'kernelIndex_March'에도 Buffer를 세팅해서 사용 가능하게 만들어준다.
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_March, "pointsWeight");
            chunk.SetPointsWeightBuffer(marchingCubesCS, kernelIndex_ShapeToCube, "pointsWeight");

            marchingCubesCS.Dispatch(kernelIndex_ShapeToCube, threadGroup, threadGroup, threadGroup);

            chunk.ForceUpdatePointsWeight();
        }

        /// <summary>
        /// "MarchingCubes.compute"의 "pointsWeight" 변수가 계산되었다는 것을 전제로 실행한다.
        /// </summary>
        private void SetChunkTriangles(Chunk chunk, int threadGroup) {
            // Append Buffer이기 때문에 매 chunk 마다 triangles의 계산 시작 index를 0으로 한다.
            trianglesBuffer.SetCounterValue(0);

            marchingCubesCS.Dispatch(kernelIndex_March, threadGroup, threadGroup, threadGroup);

            ComputeBuffer.CopyCount(trianglesBuffer, triangleCountBuffer, 0);
            int[] triangleCountArray = new int[1];
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

            chunk.UpdateMesh(verts, tris);
        }

        private void FindKernels() {
            const string kernelName_March = "March";
            kernelIndex_March = marchingCubesCS.FindKernel(kernelName_March);
            if(kernelIndex_March < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: {kernelName_March}");
            }

            const string kernelName_EditPointsWeight = "EditPointsWeight";
            kernelIndex_EditPointsWeight = marchingCubesCS.FindKernel(kernelName_EditPointsWeight);
            if(kernelIndex_EditPointsWeight < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: {kernelIndex_EditPointsWeight}");
            }

            const string kernelName_ShapeToCube = "ShapeToCube";
            kernelIndex_ShapeToCube = marchingCubesCS.FindKernel(kernelName_ShapeToCube);
            if(kernelIndex_ShapeToCube < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: {kernelName_ShapeToCube}");
            }

            const string kernelName_ShapeToSphere = "ShapeToSphere";
            kernelIndex_ShapeToSphere = marchingCubesCS.FindKernel(kernelName_ShapeToSphere);
            if(kernelIndex_ShapeToSphere < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: {kernelName_ShapeToSphere}");
            }

            const string kernelName_ShapeToNoise = "ShapeToNoise";
            kernelIndex_ShapeToNoise = marchingCubesCS.FindKernel(kernelName_ShapeToNoise);
            if(kernelIndex_ShapeToNoise < 0) {
                Debug.LogError($"Kernel not found. CS: {marchingCubesCS.name}, kernel: {kernelName_ShapeToNoise}");
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
