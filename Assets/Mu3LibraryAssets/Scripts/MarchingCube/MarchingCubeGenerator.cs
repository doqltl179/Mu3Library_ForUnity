using UnityEngine;
using System.Collections.Generic;

namespace Mu3Library.MarchingCube {
    /// <summary>
    /// <br/> -
    /// <br/> [ 전제 조건 ]
    /// <br/> Cube를 생성할 때, Point는 반드시 거리가 1씩 배치된다.
    /// <br/> 만약 Point의 거리를 조절해서 Cube의 사이즈를 조절하고 싶다면 'transform.localScale'을 조절하자.
    /// <br/>
    /// <br/> [ ComponentSetting ]
    /// <br/> MarchingCube에 필요한 최소한의 컴포넌트를 불러온다.
    /// <br/>
    /// <br/> [ GenerateMarchingCube ]
    /// <br/> MarchingCube에 필요한 데이터를 생성한다.
    /// <br/>
    /// <br/> [ UpdateCubes ]
    /// <br/> 생성(혹은 수정)된 데이터를 바탕으로 MarchingCube를 재구성한다.
    /// <br/>
    /// <br/> [ Clear ]
    /// <br/> 생성된 MarchingCube의 데이터를 제거한다.
    /// <br/>
    /// <br/> -
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class MarchingCubeGenerator : MonoBehaviour {
        public const int CubeWidthMax = 255;
        public const int CubeHeightMax = 255;
        public const int CubeDepthMax = 255;
               
        public const int CubeWidthMin = 1;
        public const int CubeHeightMin = 1;
        public const int CubeDepthMin = 1;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private MeshCollider meshCollider;

        private List<Vector3> vertices = new List<Vector3>();
        private List<int> triangles = new List<int>();

        private CubePoint[,,] points = null;

        private CubeStruct[,,] cubes = null;

        /// <summary>
        /// x축의 길이
        /// </summary>
        public int CubeWidth => cubeWidth;
        private int cubeWidth = 0;
        /// <summary>
        /// y축의 길이
        /// </summary>
        public int CubeHeight => cubeHeight;
        private int cubeHeight = 0;
        /// <summary>
        /// z축의 길이
        /// </summary>
        public int CubeDepth => cubeDepth;
        private int cubeDepth = 0;

        /// <summary>
        /// x축 point의 개수
        /// </summary>
        public int PointCountWidth => pointCountWidth;
        private int pointCountWidth = 0;
        /// <summary>
        /// y축 point의 개수
        /// </summary>
        public int PointCountHeight => pointCountHeight;
        private int pointCountHeight = 0;
        /// <summary>
        /// z축 point의 개수
        /// </summary>
        public int PointCountDepth => pointCountDepth;
        private int pointCountDepth = 0;

        public int PointCount => pointCountWidth + pointCountHeight + pointCountDepth;

        public bool IsGenerated => points != null;

        [SerializeField, Range(0.01f, 1.0f)] private float pointThreshold = 0.1f;
        [SerializeField, Range(0.01f, 10.0f)] private float pointHeightSensitive = 1.0f;



#if UNITY_EDITOR
        //private void OnDrawGizmosSelected() {
        //    if(points == null) {
        //        return;
        //    }

        //    for(int d = 0; d < pointCountDepth; d++) {
        //        for(int h = 0; h < pointCountHeight; h++) {
        //            for(int w = 0; w < pointCountWidth; w++) {
        //                if(selectedPoints[w, h, d]) {
        //                    Gizmos.color = Color.green;
        //                }
        //                else {
        //                    Gizmos.color = Color.red;
        //                }
        //                Gizmos.DrawSphere(points[w, h, d], 0.15f);
        //            }
        //        }
        //    }
        //}
#endif

        private void Start() {
            ComponentSetting();
        }

        #region Utility
        public void Clear() {
            if(meshFilter == null || meshFilter.mesh == null) {
                return;
            }

            meshFilter.mesh.Clear();

            vertices.Clear();
            triangles.Clear();
            points = null;
            cubes = null;
        }

        /// <summary>
        /// 처음 초기화 할 때만 사용하며 빈번히 사용하지 않는다.
        /// </summary>
        public void GenerateMarchingCube(int width, int height, int depth) {
            ComponentSetting();

            if(width < CubeWidthMin) width = CubeWidthMin;
            else if(width > CubeWidthMax) width = CubeWidthMax;

            if(height < CubeHeightMin) height = CubeHeightMin;
            else if(height > CubeHeightMax) height = CubeHeightMax;

            if(depth < CubeDepthMin) depth = CubeDepthMin;
            else if(depth > CubeDepthMax) depth = CubeDepthMax;

            int pointCountW = width + 1;
            int pointCountH = height + 1;
            int pointCountD = depth + 1;
            points = new CubePoint[pointCountW, pointCountH, pointCountD];
            for(int d = 0; d < pointCountD; d++) {
                for(int h = 0; h < pointCountH; h++) {
                    for(int w =  0; w < pointCountW; w++) {
                        Vector3Int id = new Vector3Int(w, h, d);

                        points[w, h, d] = new CubePoint(id);
                    }
                }
            }

            cubes = new CubeStruct[width, height, depth];
            for(int d = 0; d < depth; d++) {
                for(int h = 0; h < height; h++) {
                    for(int w = 0; w < width; w++) {
                        Vector3Int id = new Vector3Int(w, h, d);

                        CubePoint[] corners = new CubePoint[MarchingTable.Corners.Length];
                        for(int i = 0; i < corners.Length; i++) {
                            Vector3Int pointID = id + MarchingTable.Corners[i];

                            corners[i] = points[pointID.x, pointID.y, pointID.z];
                        }

                        CubeStruct group = new CubeStruct(id, corners);

                        cubes[w, h, d] = group;
                    }
                }
            }

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
            for(int d = 0; d < pointCountDepth; d++) {
                for(int h = 0; h < pointCountHeight; h++) {
                    for(int w = 0; w < pointCountWidth; w++) {
                        if(w == 0 || h == 0 || d == 0 || w == cubeWidth || h == cubeHeight || d == cubeDepth) {
                            points[w, h, d].HeightToMax();
                        }
                        else {
                            points[w, h, d].HeightToMin();
                        }
                    }
                }
            }

            UpdateCubes();
        }

        /// <summary>
        /// 'position'을 중심으로 'radius'에 포함된 모든 CubePoint의 height값을 올린다.
        /// </summary>
        /// <param name="position"> World Position </param>
        public void IncreasePointHeight(Vector3 position, float radius) {
            List<CubePoint> points = GetPointsInArea(position, radius);

            if(points.Count > 0) {
                for(int i = 0; i < points.Count; i++) {
                    points[i].AddHeight(pointHeightSensitive * Time.deltaTime);
                }

                UpdateCubes();
            }
        }

        /// <summary>
        /// 'position'을 중심으로 'radius'에 포함된 모든 CubePoint의 height값을 내린다.
        /// </summary>
        /// <param name="position"> World Position </param>
        public void DecreasePointHeight(Vector3 position, float radius) {
            List<CubePoint> points = GetPointsInArea(position, radius);

            if(points.Count > 0) {
                for(int i = 0; i < points.Count; i++) {
                    points[i].AddHeight(-pointHeightSensitive * Time.deltaTime);
                }

                UpdateCubes();
            }
        }

        /// <summary>
        /// <br/> True -> False
        /// <br/> False -> True
        /// </summary>
        public bool TurnPointSelected(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return false;
            }

            points[w, h, d].TurnPointSelected();

            UpdateCubes();

            return points[w, h, d].Height >= pointThreshold;
        }

        public bool IsPointSelected(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return false;
            }

            return points[w, h, d].Height >= pointThreshold;
        }

        public float GetPointHeightRatio(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return -1;
            }

            return points[w, h, d].GetHeightRatio();
        }

        public float GetPointHeight(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return -1;
            }

            return points[w, h, d].Height;
        }

        public Vector3[,,] GetPointPositions() {
            Vector3[,,] result = new Vector3[pointCountWidth, pointCountHeight, pointCountDepth];

            for(int d = 0; d < pointCountDepth; d++) {
                for(int h = 0; h < pointCountHeight; h++) {
                    for(int w = 0; w < pointCountWidth; w++) {
                        result[w, h, d] = points[w, h, d].ID;
                    }
                }
            }

            return result;
        }
        #endregion

        private void UpdateCubes() {
            System.DateTime startTime = System.DateTime.Now;

            vertices.Clear();
            triangles.Clear();

            CubeStruct currentCube = null;
            for(int z = 0; z < cubeDepth; z++) {
                for(int y = 0; y < cubeHeight; y++) {
                    for(int x = 0; x < cubeWidth; x++) {
                        currentCube = cubes[x, y, z];

                        currentCube.UpdateTriangles(pointThreshold);

                        // triangle의 index값을 현재 vertex의 개수만큼 밀어서 추가한다.
                        // vertex를 먼저 추가하면 triangle의 index값에 문제가 생긴다.
                        currentCube.AddTriangles(ref triangles, vertices.Count);
                        currentCube.AddVertices(ref vertices);
                    }
                }
            }

            meshFilter.mesh.Clear();
            if(vertices.Count >= 3 && triangles.Count >= 3) {
                meshFilter.mesh.SetVertices(vertices);
                meshFilter.mesh.SetTriangles(triangles, 0);

                meshFilter.mesh.RecalculateNormals();
                meshFilter.mesh.RecalculateTangents();
                meshFilter.mesh.RecalculateBounds();

                // Mesh의 충돌 데이터가 바로 업데이트 되지 않음. (원인 파악 X)
                // 그렇기 때문에 임시조치로 아래의 코드를 작성함.
                meshCollider.enabled = false;
                meshCollider.enabled = true;
            }
            else {
                Debug.Log($"Mesh properties not enough. vertCount: {vertices.Count}, triCount: {triangles.Count}");
            }

            System.DateTime endTime = System.DateTime.Now;
            Debug.Log($"Updated Cubes. time: {(endTime - startTime).TotalMilliseconds / 1000f:F3}, vertCount: {vertices.Count}, triCount: {triangles.Count}");
        }

        /// <summary>
        /// 'position'을 중심으로 'radius'에 포함된 모든 CubePoint를 반환한다.
        /// </summary>
        /// <param name="position"> World Position </param>
        /// <returns></returns>
        private List<CubePoint> GetPointsInArea(Vector3 position, float radius) {
            List<CubePoint> boundaryPoints = new List<CubePoint>();

            // MarchingCube가 회전하고 있을 가능성이 있어 World 좌표를 Local 좌표로 변환하여 계산한다.
            Vector3 localPosition = transform.InverseTransformPoint(position);

            float scaleOffset = transform.localScale.x;

            Vector3 boundaryMin = localPosition - Vector3.one * (radius / scaleOffset);
            Vector3 boundaryMax = localPosition + Vector3.one * (radius / scaleOffset);

            // Min의 Index 값은 올리고, Max의 Index 값은 내린다.
            Vector3Int boundaryIdxMin = new Vector3Int(Mathf.CeilToInt(boundaryMin.x), Mathf.CeilToInt(boundaryMin.y), Mathf.CeilToInt(boundaryMin.z));
            Vector3Int boundaryIdxMax = new Vector3Int(Mathf.FloorToInt(boundaryMin.x), Mathf.FloorToInt(boundaryMin.y), Mathf.FloorToInt(boundaryMin.z));

            float calculatedCubeWidth = cubeWidth * scaleOffset;
            float calculatedCubeHeight = cubeHeight * scaleOffset;
            float calculatedCubeDepth = cubeDepth * scaleOffset;
            if(boundaryMin.x > calculatedCubeWidth || boundaryMin.y > calculatedCubeHeight || boundaryMin.z > calculatedCubeDepth) {
                return boundaryPoints;
            }
            else if(boundaryMax.x < 0 || boundaryMax.y < 0 || boundaryMax.z < 0) {
                return boundaryPoints;
            }

            for(int z = boundaryIdxMin.z; z <= boundaryMax.z; z++) {
                for(int y = boundaryIdxMin.y; y <= boundaryMax.y; y++) {
                    for(int x = boundaryIdxMin.x; x <= boundaryMax.x; x++) {
                        if(IsPointInRange(x, y, z)) {
                            boundaryPoints.Add(points[x, y, z]);
                        }
                    }
                }
            }

            return boundaryPoints;
        }

        private bool IsPointInRange(int x, int y, int z) {
            return x >= 0 && y >= 0 && z >= 0 && x < pointCountWidth && y < pointCountHeight && z < pointCountDepth;
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
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

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
