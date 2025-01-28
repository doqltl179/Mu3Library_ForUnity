using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.MarchingCube {
    public class MarchingCubeGeneratorWithCS : MonoBehaviour {
        public const int CubeWidthMax = 255;
        public const int CubeHeightMax = 255;
        public const int CubeDepthMax = 255;

        public const int CubeWidthMin = 1;
        public const int CubeHeightMin = 1;
        public const int CubeDepthMin = 1;

        [SerializeField] private ComputeShader marchingCubeCS;

        private ComputeBuffer tableCornersBuffer = null;
        private ComputeBuffer tableEdgesBuffer = null;
        private ComputeBuffer tableTrianglesBuffer = null;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private MeshCollider meshCollider;

        private CubePointWithCS[] points = null;

        [SerializeField] private Vector3[] vertices = null;
        [SerializeField] private int[] verticesCountArray = null;

        /// <summary>
        /// x축의 길이
        /// </summary>
        public int CubeWidth => cubeWidth;
        private int cubeWidth = -1;
        /// <summary>
        /// y축의 길이
        /// </summary>
        public int CubeHeight => cubeHeight;
        private int cubeHeight = -1;
        /// <summary>
        /// z축의 길이
        /// </summary>
        public int CubeDepth => cubeDepth;
        private int cubeDepth = -1;

        /// <summary>
        /// x축 point의 개수
        /// </summary>
        public int PointCountWidth => pointCountWidth;
        private int pointCountWidth = -1;
        /// <summary>
        /// y축 point의 개수
        /// </summary>
        public int PointCountHeight => pointCountHeight;
        private int pointCountHeight = -1;
        /// <summary>
        /// z축 point의 개수
        /// </summary>
        public int PointCountDepth => pointCountDepth;
        private int pointCountDepth = -1;

        public int PointCount => pointCountWidth + pointCountHeight + pointCountDepth;

        [SerializeField, Range(0.01f, 1.0f)] private float pointThreshold = 0.1f;
        [SerializeField, Range(0.01f, 10.0f)] private float pointHeightSensitive = 1.0f;

        [Title("Debug")]
        [SerializeField] private float[] debugFloatArray;



        private void OnDestroy() {
            tableCornersBuffer?.Release();
            tableEdgesBuffer?.Release();
            tableTrianglesBuffer?.Release();

            debugFloatArray?.Reverse();
        }

        #region Utility
        public void Clear() {
            if(meshFilter == null || meshFilter.mesh == null) {
                return;
            }

            meshFilter.mesh.Clear();

            tableCornersBuffer?.Release();
            tableEdgesBuffer?.Release();
            tableTrianglesBuffer?.Release();

            cubeWidth = -1;
            cubeHeight = -1;
            cubeDepth = -1;

            pointCountWidth = -1;
            pointCountHeight = -1;
            pointCountDepth = -1;
        }

        public void GenerateMarchingCube(int width, int height, int depth) {
            // MarchingCube의 사이즈가 변하지 않았기 때문에 초기화하지 않는다.
            if(width == cubeWidth && height == cubeHeight && depth == cubeDepth) {
                return;
            }

            ComponentSetting();
            Clear();

            if(width < CubeWidthMin) width = CubeWidthMin;
            else if(width > CubeWidthMax) width = CubeWidthMax;

            if(height < CubeHeightMin) height = CubeHeightMin;
            else if(height > CubeHeightMax) height = CubeHeightMax;

            if(depth < CubeDepthMin) depth = CubeDepthMin;
            else if(depth > CubeDepthMax) depth = CubeDepthMax;

            int pointCountW = width + 1;
            int pointCountH = height + 1;
            int pointCountD = depth + 1;

            points = new CubePointWithCS[pointCountW * pointCountH * pointCountD];
            for(int d = 0; d < pointCountD; d++) {
                for(int h = 0; h < pointCountH; h++) {
                    for(int w = 0; w < pointCountW; w++) {
                        int idx = (d * pointCountH * pointCountW) + (h * pointCountW) + w;

                        points[idx] = new CubePointWithCS(idx);
                    }
                }
            }

            SetTableBuffer();

            // 계산용 데이터 추가
            marchingCubeCS.SetInt("CubeWidth", width);
            marchingCubeCS.SetInt("CubeHeight", height);
            marchingCubeCS.SetInt("CubeDepth", depth);
            marchingCubeCS.SetInt("PointCountWidth", pointCountW);
            marchingCubeCS.SetInt("PointCountHeight", pointCountH);
            marchingCubeCS.SetInt("PointCountDepth", pointCountD);

            // 계산 가능한 최대 길이로 배열 생성
            const int tableTrianglesLengthX = 16;
            vertices = new Vector3[width * height * depth * tableTrianglesLengthX];

            cubeWidth = width;
            cubeHeight = height;
            cubeDepth = depth;
            pointCountWidth = pointCountW;
            pointCountHeight = pointCountH;
            pointCountDepth = pointCountD;
        }

        /// <summary>
        /// MarchingCube를 Cube 형태로 만들기 위해 CubePoint의 Height 값을 조정한다.
        /// </summary>
        public void SetShapeToCube() {
            int pointIdx = -1;
            for(int d = 0; d < pointCountDepth; d++) {
                for(int h = 0; h < pointCountHeight; h++) {
                    for(int w = 0; w < pointCountWidth; w++) {
                        pointIdx = (d * pointCountHeight * pointCountWidth) + (h * pointCountWidth) + w;

                        if(w == 0 || h == 0 || d == 0 || w == cubeWidth || h == cubeHeight || d == cubeDepth) {
                            points[pointIdx].HeightToMax();
                        }
                        else {
                            points[pointIdx].HeightToMin();
                        }
                    }
                }
            }

            UpdateCubes();
        }
        #endregion

        private void UpdateCubes() {
            System.DateTime startTime = System.DateTime.Now;

            int kernelIdx = marchingCubeCS.FindKernel("CSMain");
            if(kernelIdx < 0) {
                Debug.LogError("Kernel not found. name: CSMain");

                return;
            }

            marchingCubeCS.SetFloat("PointThreshold", pointThreshold);

            // 각 point의 height값 전송
            float[] pointHeights = points.Select(t => t.Height).ToArray();
            ComputeBuffer pointHeightsBuffer = new ComputeBuffer(pointHeights.Length, sizeof(float));
            pointHeightsBuffer.SetData(pointHeights);
            marchingCubeCS.SetBuffer(kernelIdx, "PointHeights", pointHeightsBuffer);

            // vertices를 읽어올 버퍼
            const int tableTrianglesLengthX = 16;
            int cubeSize = cubeWidth * cubeHeight * cubeDepth;
            int vertMaxCount = cubeSize * tableTrianglesLengthX;
            ComputeBuffer verticesBuffer = new ComputeBuffer(vertMaxCount, sizeof(float) * 3);
            marchingCubeCS.SetBuffer(kernelIdx, "Vertices", verticesBuffer);

            ComputeBuffer verticesCountBuffer = new ComputeBuffer(cubeSize, sizeof(int));
            marchingCubeCS.SetBuffer(kernelIdx, "VerticesCount", verticesCountBuffer);

            const int debugFloatBufferSize = 100;
            ComputeBuffer debugFloatBuffer = new ComputeBuffer(debugFloatBufferSize, sizeof(float));
            marchingCubeCS.SetBuffer(kernelIdx, "FloatDebugBuffer", debugFloatBuffer);

            int threadGroupX = Mathf.CeilToInt(pointCountWidth / 8.0f);
            int threadGroupY = Mathf.CeilToInt(pointCountHeight / 8.0f);
            int threadGroupZ = Mathf.CeilToInt(pointCountDepth / 8.0f);
            marchingCubeCS.Dispatch(kernelIdx, threadGroupX, threadGroupY, threadGroupZ);

            // -----------------------------------------------------------------------

            debugFloatArray = new float[debugFloatBufferSize];
            debugFloatBuffer.GetData(debugFloatArray);

            verticesBuffer.GetData(vertices);

            verticesCountArray = new int[cubeSize];
            verticesCountBuffer.GetData(verticesCountArray);

            List<Vector3> usingVertices = new List<Vector3>();
            for(int i = 0; i < verticesCountArray.Length; i++) {
                if(verticesCountArray[i] == 0 || verticesCountArray[i] < 0) {
                    continue;
                }

                Vector3[] copyArray = new Vector3[verticesCountArray[i]];
                System.Array.Copy(vertices, tableTrianglesLengthX * i, copyArray, 0, copyArray.Length);
                usingVertices.AddRange(copyArray);
            }

            Debug.Log($"Vert Sorting. ({usingVertices.Count}/{vertices.Length})");

            int[] triangles = new int[usingVertices.Count];
            for(int i = 0; i < triangles.Length; i++) {
                triangles[i] = i;
            }

            // -----------------------------------------------------------------------

            // 버퍼 해제
            pointHeightsBuffer?.Release();
            verticesBuffer?.Release();
            verticesCountBuffer?.Release();

            debugFloatBuffer?.Release();

            // -----------------------------------------------------------------------

            meshFilter.mesh.Clear();
            if(usingVertices.Count >= 3 && triangles.Length >= 3) {
                meshFilter.mesh.SetVertices(usingVertices);
                meshFilter.mesh.SetTriangles(triangles, 0);

                meshFilter.mesh.RecalculateNormals();
                meshFilter.mesh.RecalculateTangents();
                meshFilter.mesh.RecalculateBounds();

                // Mesh의 충돌 데이터가 바로 업데이트 되지 않음. (원인 파악 X)
                // 그렇기 때문에 임시조치로 아래의 코드를 작성함.
                //meshCollider.enabled = false;
                //meshCollider.enabled = true;
            }
            else {
                Debug.Log($"Mesh properties not enough. vertCount: {usingVertices.Count}, triCount: {triangles.Length}");
            }

            System.DateTime endTime = System.DateTime.Now;
            Debug.Log($"Updated Cubes. time: {(endTime - startTime).TotalMilliseconds / 1000f:F3}, vertCount: {usingVertices.Count}, triCount: {triangles.Length}");
        }

        /// <summary>
        /// 'MarchingTable.cs'의 static 데이터를 'marchingCubeCS'에 추가한다.
        /// </summary>
        private void SetTableBuffer() {
            int kernelIdx = marchingCubeCS.FindKernel("CSMain");

            if(tableCornersBuffer == null) {
                // 버퍼 데이터 설정
                tableCornersBuffer = new ComputeBuffer(MarchingTable.Corners.Length, sizeof(uint) * 3);
                tableCornersBuffer.SetData(MarchingTable.Corners);

                // 버퍼 데이터 추가
                marchingCubeCS.SetBuffer(kernelIdx, "TableCorners", tableCornersBuffer);
            }

            if(tableEdgesBuffer == null) {
                int lengthY = -1;
                int lengthX = -1;
                int[] edges = MarchingTableEdgesToOneDimensionalArray(out lengthY, out lengthX);

                // 버퍼 데이터 설정
                tableEdgesBuffer = new ComputeBuffer(edges.Length, sizeof(uint));
                tableEdgesBuffer.SetData(edges);

                // 버퍼 데이터 추가
                marchingCubeCS.SetBuffer(kernelIdx, "TableEdges", tableEdgesBuffer);
            }

            if(tableTrianglesBuffer == null) {
                int lengthY = -1;
                int lengthX = -1;
                int[] triangles = MarchingTableTrianglesToOneDimensionalArray(out lengthY, out lengthX);

                // 버퍼 데이터 설정
                tableTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(uint));
                tableTrianglesBuffer.SetData(triangles);

                // 버퍼 데이터 추가
                marchingCubeCS.SetBuffer(kernelIdx, "TableTriangles", tableTrianglesBuffer);
            }
        }

        private int[] MarchingTableEdgesToOneDimensionalArray(out int lengthY, out int lengthX) {
            lengthY = MarchingTable.Edges.GetLength(0);
            lengthX = MarchingTable.Edges.GetLength(1);

            int[] result = new int[lengthX * lengthY];
            for(int y = 0; y < lengthY; y++) {
                for(int x = 0; x < lengthX; x++) {
                    result[y * lengthX + x] = MarchingTable.Edges[y, x];
                }
            }

            return result;
        }

        private int[] MarchingTableTrianglesToOneDimensionalArray(out int lengthY, out int lengthX) {
            lengthY = MarchingTable.Triangles.GetLength(0);
            lengthX = MarchingTable.Triangles.GetLength(1);

            int[] result = new int[lengthX * lengthY];
            for(int y = 0; y < lengthY; y++) {
                for(int x = 0; x < lengthX; x++) {
                    result[y * lengthX + x] = MarchingTable.Triangles[y, x];
                }
            }

            return result;
        }

        private void ComponentSetting() {
            if(meshFilter == null) {
                meshFilter = GetComponent<MeshFilter>();
                if(meshFilter == null) {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if(meshRenderer == null) {
                meshRenderer = GetComponent<MeshRenderer>();
                if(meshRenderer == null) {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            if(meshRenderer.sharedMaterial == null) {
                const string shaderName = "Standard";

                Shader shader = Shader.Find(shaderName);
                if(shader == null) {
                    Debug.LogError($"Shader not found. shaderName: {shaderName}");

                    return;
                }

                meshRenderer.sharedMaterial = new Material(shader);
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.mesh = mesh;

            if(meshCollider == null) {
                meshCollider = GetComponent<MeshCollider>();
                if(meshCollider == null) {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                }
            }
            meshCollider.sharedMesh = meshFilter.mesh;
        }
    }
}
