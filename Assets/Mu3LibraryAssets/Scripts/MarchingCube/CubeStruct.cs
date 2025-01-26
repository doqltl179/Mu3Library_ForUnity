using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.MarchingCube {
    /// <summary>
    /// 하나의 Cube에 대한 Struct
    /// </summary>
    public class CubeStruct {
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

        /// <summary>
        /// Height 캐싱
        /// </summary>
        private float[] cornerHeights = null;



        public CubeStruct(Vector3Int id, CubePoint[] cornerPoints) {
            this.id = id;

            corners = cornerPoints;
            cornerHeights = new float[cornerPoints.Length];
        }

        #region Utility
        public void UpdateTriangles(float threshold) {
            bool isHeightChanged = false;
            for(int i = 0; i < corners.Length; i++) {
                if(corners[i].Height != cornerHeights[i]) {
                    isHeightChanged = true;

                    break;
                }
            }

            if(isHeightChanged) {
                vertices.Clear();
                triangles.Clear();

                int triIdx = GetConfigIndex(threshold);

                for(int i = 0; MarchingTable.Triangles[triIdx, i] != -1; i++) {
                    int edgeIdx = MarchingTable.Triangles[triIdx, i];

                    int startCornerIdx = MarchingTable.Edges[edgeIdx, 0];
                    int endCornerIdx = MarchingTable.Edges[edgeIdx, 1];

                    CubePoint startPoint = corners[startCornerIdx];
                    CubePoint endPoint = corners[endCornerIdx];

                    Vector3 vertPos = InterpolateVertex(startPoint, endPoint, threshold);

                    vertices.Add(vertPos);
                    triangles.Add(vertices.Count - 1);
                }

                for(int i = 0; i < cornerHeights.Length; i++) {
                    cornerHeights[i] = corners[i].Height;
                }
            }
        }

        public bool IsPointInclude(CubePoint point) {
            return corners.Any(t => t == point);
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

        public void AddVertices(ref List<Vector3> vertexList) {
            if(vertices.Count > 0) {
                vertexList.AddRange(vertices);
            }
        }

        public void AddTriangles(ref List<int> triangleList, int offset = 0) {
            if(triangles.Count > 0) {
                if(offset == 0) {
                    triangleList.AddRange(triangles);
                }
                else {
                    triangleList.AddRange(triangles.Select(t => t + offset));
                }
            }
        }
        #endregion

        /// <summary>
        /// threshold를 활용해서 vertex의 위치값 계산
        /// </summary>
        private Vector3 InterpolateVertex(CubePoint startPoint, CubePoint endPoint, float threshold) {
            Vector3 startPosition = startPoint.ID;
            Vector3 endPosition = endPoint.ID;

            float startHeight = startPoint.Height;
            float endHeight = endPoint.Height;

            // height 값이 같으면
            if(Mathf.Abs(startHeight - endHeight) < Mathf.Epsilon)
                return (startPosition + endPosition) * 0.5f; // 중간 점 반환

            // threshold가 height와 정확히 일치하는 경우
            if(Mathf.Abs(threshold - startHeight) < Mathf.Epsilon)
                return startPosition;
            if(Mathf.Abs(threshold - endHeight) < Mathf.Epsilon)
                return endPosition;

            // 선형 보간 계산
            float t = (threshold - startHeight) / (endHeight - startHeight);
            return Vector3.Lerp(startPosition, endPosition, t); // Lerp를 활용해 보간
        }

        /// <summary>
        /// 'MarchingTable.Triangles'의 index 값을 반환
        /// </summary>
        private int GetConfigIndex(float threshold) {
            if(corners == null) {
                return 0;
            }

            int configIndex = 0;

            for(int i = 0; i < corners.Length; i++) {
                if(corners[i].Height >= threshold) {
                    configIndex |= 1 << i;
                }
            }

            return configIndex;
        }
    }
}
