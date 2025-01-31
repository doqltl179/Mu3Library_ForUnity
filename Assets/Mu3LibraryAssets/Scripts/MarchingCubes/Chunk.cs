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
        /// <br/> 각 값의 범위를 (0 ~ 1)로 한다.
        /// <br/> Buffer에서 가져오는 배열을 그대로 사용하기 위해 1차원 배열로 선언한다.
        /// </summary>
        private float[] pointsWeight = null;

        private ComputeBuffer pointsWeightBuffer = null;



        private void OnDestroy() {
            pointsWeightBuffer?.Release();
        }

        #region Utility
        public void ForceUpdatePointsWeight() {
            if(pointsWeight == null || pointsWeightBuffer == null) {
                Debug.LogError("Force Update Failed.");

                return;
            }

            if(pointsWeightBuffer.count != pointsWeight.Length) {
                Debug.LogError("Force Update Failed.");

                return;
            }

            // Set Weight
            pointsWeightBuffer.GetData(pointsWeight);
        }

        /// <summary>
        /// <br/> 'pointsWeightBuffer'는 'MarchingCubesGenerator.cs'에서 계산해준다.
        /// <br/> 그렇기 때문에 이 함수에서는 현재 chunk가 가지고 있는 'pointsWeightBuffer'를 'CS'에 세팅해준다.
        /// </summary>
        public void SetPointsWeightBuffer(ComputeShader CS, int kernelIndex, string name) {
            if(pointsWeightBuffer == null) {
                Debug.LogError("Buffer is NULL.");

                return;
            }

            CS.SetBuffer(kernelIndex, name, pointsWeightBuffer);
        }

        public void UpdateMesh(Vector3[] vertices, int[] triangles) {
            if(meshFilter == null || meshFilter.sharedMesh == null) {
                Debug.LogWarning($"Mesh not found.");

                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            mesh.Clear();

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateNormals();

            if(meshCollider != null) {
                // Force Update
                meshCollider.enabled = false;
                meshCollider.enabled = true;
            }

            Debug.Log($"Chunk Mesh Updated. Coord: {coord}, vertCount: {vertices.Length}");
        }

        public void Init(int coordX, int coordY, int coordZ, int size, bool useCollider = false, Material mat = null) {
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
            }

            if(mat != null) {
                meshRenderer.sharedMaterial = mat;
            }

            if(this.size != size) {
                int pointCount = size + 1;

                pointsWeight = new float[pointCount * pointCount * pointCount];

                // size가 달라졌을 때에만 Buffer를 다시 만들어준다.
                if(pointsWeightBuffer != null) {
                    pointsWeightBuffer.Release();
                }
                pointsWeightBuffer = new ComputeBuffer(pointsWeight.Length, sizeof(float));
            }

            coord = new Vector3Int(coordX, coordY, coordZ);
            this.size = size;
        }
        #endregion
    }
}
