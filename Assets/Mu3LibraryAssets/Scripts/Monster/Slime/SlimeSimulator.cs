using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Monster {
    public class SlimeSimulator : MonoBehaviour {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        private Mesh mesh;
        private Material material;

        private float bodyRadius = 0.5f;
        // Mesh의 Pivot을 바닥에 둔다.
        private Vector3 bodyPivot = new Vector3(0.5f, 0.0f, 0.5f);




        // 스크립트 기본 세팅
        private void Start() {
            meshFilter = GetComponent<MeshFilter>();
            if(meshFilter == null) {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshRenderer = GetComponent<MeshRenderer>();
            if(meshRenderer == null) {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            meshCollider = GetComponent<MeshCollider>();
            if(meshCollider == null) {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            mesh = meshFilter.sharedMesh;
            if(mesh == null) {
                mesh = GenerateBodyMesh();

                meshFilter.sharedMesh = mesh;
                meshCollider.sharedMesh = mesh;
            }

            material = meshRenderer.sharedMaterial;
            if(material == null) {
                Shader shader = Shader.Find("Mu3Library/StandardSlimeBody");
                if(shader == null) {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);

                meshRenderer.sharedMaterial = material;
            }
        }

        public Mesh GenerateBodyMesh(int bodyDensityWidth = 10, int bodyDensityHeight = 10, float radius = 0.5f) {
            int vertCountW = bodyDensityWidth + 1;
            int vertCountH = bodyDensityHeight + 1;

            Vector3[] vertices = new Vector3[vertCountW * vertCountH];
            for(int h = 0; h < vertCountH; h++) {
                float theta = Mathf.PI * h / bodyDensityHeight;
                for(int w = 0; w < vertCountW; w++) {
                    float phi = Mathf.PI * 2 * w / bodyDensityWidth;

                    float x = radius * Mathf.Sin(theta) * Mathf.Cos(phi);
                    float y = radius * Mathf.Cos(theta);
                    float z = radius * Mathf.Sin(theta) * Mathf.Sin(phi);

                    int vertIdx = h * vertCountW + w;
                    vertices[vertIdx] = new Vector3(x, y, z);
                }
            }

            int[] triangles = new int[bodyDensityWidth * bodyDensityHeight * 2 * 3];
            int triIdx = 0;
            for(int h = 0; h < bodyDensityHeight; h++) {
                for(int w = 0; w < bodyDensityWidth; w++) {
                    int vertIdx = h * vertCountW + w;

                    triangles[triIdx + 0] = vertIdx;
                    triangles[triIdx + 1] = vertIdx + 1;
                    triangles[triIdx + 2] = vertIdx + vertCountW + 1;

                    triangles[triIdx + 3] = vertIdx;
                    triangles[triIdx + 4] = vertIdx + vertCountW + 1;
                    triangles[triIdx + 5] = vertIdx + vertCountW;

                    triIdx += 6;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "SlimeBody";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = vertices.Select(t => t.normalized).ToArray();

            bodyRadius = radius;

            // Debug
            List<int> indies = new List<int>();
            for(int i = 0; i < vertices.Length; i++) {
                indies.Add(i);
            }
            for(int i = 0; i < indies.Count - 1; i++) {
                Vector3 currentVert = vertices[indies[i]];
                int samePosCount = 0;
                for(int j = i + 1; j < indies.Count; j++) {
                    Vector3 compareVert = vertices[indies[j]];
                    if(Vector3.Distance(currentVert, compareVert) < 0.0001f) {
                        samePosCount++;

                        indies.RemoveAt(j);
                        j--;
                    }
                }

                if(samePosCount > 0) {
                    Debug.Log($"Found Same Verts. Vertex: {currentVert}, Count: {samePosCount}");
                }
            }

            return mesh;
        }
    }
}
