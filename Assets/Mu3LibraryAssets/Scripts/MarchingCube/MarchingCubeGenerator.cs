using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Mu3Library.MarchingCube {
    public class MarchingCubeGenerator : MonoBehaviour {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private MeshCollider meshCollider;

        private Mesh mesh = null;
        private Vector3[] vertices = null;
        private int[] triangles = null;

        private CubePoint[,,] points = null;

        private CubeGroup[,,] cubes = null;

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
        public void GenerateMarchingCube(int width, int height, int depth) {
            ComponentSetting();

            if(width < 1 || height < 1 || depth < 1) {
                Debug.Log($"Length not enough.");

                return;
            }

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

            cubes = new CubeGroup[width, height, depth];
            for(int d = 0; d < depth; d++) {
                for(int h = 0; h < height; h++) {
                    for(int w = 0; w < width; w++) {
                        Vector3Int id = new Vector3Int(w, h, d);

                        CubePoint[] corners = new CubePoint[MarchingTable.Corners.Length];
                        for(int i = 0; i < corners.Length; i++) {
                            Vector3Int pointID = id + MarchingTable.Corners[i];

                            corners[i] = points[pointID.x, pointID.y, pointID.z];
                        }

                        CubeGroup group = new CubeGroup(id, corners);

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
        /// <br/> True -> False
        /// <br/> False -> True
        /// </summary>
        public bool TurnPointSelected(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return false;
            }

            points[w, h, d].TurnPointSelected();

            List<Vector3> newVertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();

            for(int z = 0; z < cubeDepth; z++) {
                for(int y = 0; y < cubeHeight; y++) {
                    for(int x = 0; x < cubeWidth; x++) {
                        if(cubes[x, y, z].IsPointInclude(w, h, d)) {
                            cubes[x, y, z].UpdateTriangles();
                        }

                        // triangle의 index값을 현재 vertex의 개수만큼 밀어서 추가한다.
                        // vertex를 먼저 추가하면 triangle의 index값에 문제가 생긴다.
                        newTriangles.AddRange(cubes[x, y, z].GetTriangles(newVertices.Count));
                        newVertices.AddRange(cubes[x, y, z].GetVertices());
                    }
                }
            }

            vertices = newVertices.ToArray();
            triangles = newTriangles.ToArray();
            mesh.Clear();
            if(vertices.Length >= 3 && triangles.Length >= 3) {
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
            }
            else {
                Debug.Log($"Mesh properties not enough. vertCount: {vertices.Length}, triCount: {triangles.Length}");
            }

            return points[w, h, d].IsSelected;
        }

        public bool IsPointSelected(int w, int h, int d) {
            if(w < 0 || h < 0 || d < 0 || w >= pointCountWidth || h >= pointCountHeight || d >= pointCountDepth) {
                return false;
            }

            return points[w, h, d].IsSelected;
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

            vertices = new Vector3[0];
            triangles = new int[0];

            mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;

            meshFilter.sharedMesh = mesh;

            if(meshCollider == null) {
                meshCollider = GetComponent<MeshCollider>();
                if(meshCollider == null) {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                }
            }
            meshCollider.sharedMesh = mesh;
        }



        /// <summary>
        /// 하나의 Cube에 대한 Struct
        /// </summary>
        private class CubeGroup {
            /// <summary>
            /// Cube의 Index
            /// </summary>
            public Vector3Int ID => id;
            private Vector3Int id = Vector3Int.one * -1;

            /// <summary>
            /// Cube에 포함된 point 그룹
            /// </summary>
            private CubePoint[] corners = null;

            /// <summary>
            /// 실제 Mesh에 그려질 Vertex의 위치
            /// </summary>
            private List<Vector3> vertices = new List<Vector3>();
            /// <summary>
            /// 실제 Mesh에 그려질 Triangle의 Vertex Index
            /// </summary>
            private List<int> triangles = new List<int>();



            public CubeGroup(Vector3Int id, CubePoint[] cornerPoints) {
                this.id = id;

                corners = cornerPoints;

                UpdateTriangles();
            }

            #region Utility
            public void UpdateTriangles() {
                vertices.Clear();
                triangles.Clear();

                int triIdx = GetConfigIndex();

                for(int i = 0; MarchingTable.Triangles[triIdx, i] != -1; i++) {
                    int edgeIdx = MarchingTable.Triangles[triIdx, i];

                    int startCornerIdx = MarchingTable.Edges[edgeIdx, 0];
                    int endCornerIdx = MarchingTable.Edges[edgeIdx, 1];

                    Vector3 startPoint = corners[startCornerIdx].ID;
                    Vector3 endPoint = corners[endCornerIdx].ID;

                    Vector3 vertPos = (startPoint + endPoint) * 0.5f;

                    vertices.Add(vertPos);
                    triangles.Add(vertices.Count - 1);
                }
            }

            public bool IsPointInclude(int w, int h, int d) {
                if(corners == null) {
                    return false;
                }

                return corners.Any(t => t.ID.x == w && t.ID.y == h && t.ID.z == d);
            }

            public bool IsPointInclude(Vector3Int point) {
                if(corners == null) {
                    return false;
                }

                return corners.Any(t => t.ID.x == point.x && t.ID.y == point.y && t.ID.z == point.z);
            }

            public Vector3[] GetVertices() {
                return vertices.ToArray();
            }

            public int[] GetTriangles(int offset = 0) {
                if(offset == 0) {
                    return triangles.ToArray();
                }
                else {
                    return triangles.Select(t => t + offset).ToArray();
                }
            }
            #endregion

            private int GetConfigIndex() {
                if(corners == null) {
                    return 0;
                }

                int configIndex = 0;

                for(int i = 0; i < corners.Length; i++) {
                    if(corners[i].IsSelected) {
                        configIndex |= 1 << i;
                    }
                }

                return configIndex;
            }
        }

        private class CubePoint {
            /// <summary>
            /// 전체 point의 Index
            /// </summary>
            public Vector3Int ID => id;
            private Vector3Int id = Vector3Int.one * -1;

            public bool IsSelected => isSelected;
            private bool isSelected = false;



            public CubePoint(Vector3Int id) {
                this.id = id;
            }

            #region Utility
            public void TurnPointSelected() {
                isSelected = !isSelected;


            }
            #endregion
        }
    }
}
