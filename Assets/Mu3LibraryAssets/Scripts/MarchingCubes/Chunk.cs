using UnityEngine;

namespace Mu3Library.MarchingCubes {
    public class Chunk : MonoBehaviour {
        private MeshFilter meshFilter = null;
        private MeshRenderer meshRenderer = null;
        private MeshCollider meshCollider = null;

        public int CoordX => coord.x;
        public int CoordY => coord.y;
        public int CoordZ => coord.z;
        public Vector3Int Coord => coord;
        private Vector3Int coord = new Vector3Int(-1, -1, -1);

        /// <summary>
        /// <br/> chunk 하나의 길이
        /// <br/> 가로 == 세로 == 높이
        /// </summary>
        public int Size => size;
        private int size = -1;

        /// <summary>
        /// 각 값의 범위를 (0 ~ 1)로 한다.
        /// </summary>
        private float[,,] pointsWeight = null;



        #region Utility
        public void ForceUpdatePointsWeight(float[] weights) {
            int pointCount = size + 1;
            int pointCountAll = pointCount * pointCount * pointCount;
            if(weights.Length != pointCountAll) {
                Debug.LogError($"Not same count. current: {pointCountAll}, change: {weights.Length}");

                return;
            }

            int tweDimensionPointCount = pointCount * pointCount;
            int x = 0;
            int y = 0;
            int z = 0;
            for(int i = 0; i < weights.Length; i++) {
                pointsWeight[x, y, z] = weights[i];

                x++;
                if(x >= pointCount) {
                    x = 0;
                    y++;
                    if(y >= pointCount) {
                        y = 0;
                        z++;
                    }
                }
            }
        }

        public void UpdateMesh(Vector3[] vertices, int[] triangles) {
            if(meshFilter == null || meshFilter.sharedMesh == null) {
                Debug.LogWarning($"Mesh not found.");

                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateNormals();

            Debug.Log($"Chunk Mesh Updated. Coord: {coord}, vertCount: {vertices.Length}");
        }

        public void Init(int coordX, int coordY, int coordZ, int size, bool useCollider = true, Material mat = null) {
            void GetOrAddComponent<T>(ref T component) where T : Component {
                if(component == null) {
                    component = GetComponent<T>();
                    if(component == null) {
                        component = gameObject.AddComponent<T>();
                    }
                }
            }
            GetOrAddComponent(ref meshFilter);
            GetOrAddComponent(ref meshRenderer);
            if(useCollider) GetOrAddComponent(ref meshCollider);

            if(meshFilter.sharedMesh == null) {
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                meshFilter.sharedMesh = mesh;
            }
            meshFilter.sharedMesh.name = $"Chunk_{coordX}_{coordY}_{coordZ}";

            if(meshCollider != null && meshCollider.sharedMesh == null) {
                meshCollider.sharedMesh = meshFilter.sharedMesh;

                // Force Update
                meshCollider.enabled = false;
                meshCollider.enabled = true;
            }

            if(mat != null) {
                meshRenderer.sharedMaterial = mat;
            }

            if(this.size != size) {
                int pointCount = size + 1;

                pointsWeight = new float[pointCount, pointCount, pointCount];
            }

            coord = new Vector3Int(coordX, coordY, coordZ);
            this.size = size;
        }
        #endregion
    }
}
