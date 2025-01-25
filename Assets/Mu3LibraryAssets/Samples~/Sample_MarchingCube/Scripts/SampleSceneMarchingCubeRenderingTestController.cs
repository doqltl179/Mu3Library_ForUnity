using Mu3Library.MarchingCube;
using UnityEngine;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mu3Library.Demo.MarchingCube {
    public class SampleSceneMarchingCubeRenderingTestController : MonoBehaviour {
        [SerializeField] private MarchingCubeGenerator generator;

        [SerializeField, Range(1, 10)] private int cubeWidth = 1;
        [SerializeField, Range(1, 10)] private int cubeHeight = 1;
        [SerializeField, Range(1, 10)] private int cubeDepth = 1;

        private int pointCountWidth = 0;
        private int pointCountHeight = 0;
        private int pointCountDepth = 0;

        private GameObject[,,] spheres = null;
        private Camera camera = null;



#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if(spheres != null) {
                GUIStyle textStyle = new GUIStyle() {
                    fontSize = 16, 
                    fontStyle = FontStyle.Bold,
                };

                for(int d = 0; d < pointCountDepth; d++) {
                    for(int h = 0; h < pointCountHeight; h++) {
                        for(int w = 0; w < pointCountWidth; w++) {
                            //int cornerIdxW = w % 2;
                            //int cornerIdxH = h % 2;
                            //int cornerIdxD = d % 2;
                            //int cornerIdx = System.Array.FindIndex(MarchingTable.Corners, t => t.x == cornerIdxW && t.y == cornerIdxH && t.z == cornerIdxD);



                            Handles.Label(spheres[w, h, d].transform.position + Vector3.right * 0.15f, $"({w}, {h}, {d})", textStyle);
                        }
                    }
                }
            }
        }
#endif

        private void Start() {
            if(generator == null) {
                GameObject go = new GameObject(typeof(MarchingCubeGenerator).Name);
                go.transform.position = Vector3.zero;

                generator = go.AddComponent<MarchingCubeGenerator>();
            }

            generator.GenerateMarchingCube(cubeWidth, cubeHeight, cubeDepth);

            Vector3[,,] spherePositions = generator.GetPointPositions();

            int pointCountW = cubeWidth + 1;
            int pointCountH = cubeHeight + 1;
            int pointCountD = cubeDepth + 1;
            spheres = new GameObject[pointCountW, pointCountH, pointCountD];
            for(int d = 0; d < pointCountD; d++) {
                for(int h = 0; h < pointCountH; h++) {
                    for(int w = 0; w < pointCountW; w++) {
                        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        go.name = $"Sphere_{w}_{h}_{d}";

                        go.transform.position = spherePositions[w, h, d];
                        go.transform.localScale = Vector3.one * 0.15f;

                        MeshRenderer mr = go.GetComponent<MeshRenderer>();
                        if(mr != null) {
                            //mr.enabled = false;

                            Shader shader = Shader.Find("Mu3Library/UnlitColor");
                            if(shader != null) {
                                Material material = new Material(shader);

                                mr.sharedMaterial = material;
                            }
                        }

                        spheres[w, h, d] = go;
                    }
                }
            }

            pointCountWidth = pointCountW;
            pointCountHeight = pointCountH;
            pointCountDepth = pointCountD;

            SetSphereColor();

            if(camera == null) {
                camera = Camera.main;
                if(camera == null) {
                    Debug.LogError("Camera not found.");

                    enabled = false;
                }
            }

            camera.transform.position = new Vector3(cubeWidth * 1.35f, cubeHeight * 1.25f, cubeDepth * -1.05f);
            camera.transform.LookAt(new Vector3(cubeWidth, cubeHeight * 0.9f, cubeDepth) * 0.5f);
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Mouse0)) {
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if(Physics.Raycast(ray, out hit, 1000f)) {
                    string name = hit.collider.gameObject.name;
                    int w = -1;
                    int h = -1;
                    int d = -1;
                    SphereNameToIndex(name, ref w, ref h, ref d);
                    if(w < 0 || h < 0 || d < 0) {
                        return;
                    }

                    Debug.Log($"Clicked Sphere. ({w}, {h}, {d})");

                    bool isSelected = generator.TurnPointSelected(w, h, d);
                    hit.collider.GetComponent<MeshRenderer>().sharedMaterial.color = isSelected ? Color.green : Color.red;
                }
            }
        }

        private void SetSphereColor() {
            if(generator == null || spheres == null || !generator.IsGenerated) {
                return;
            }

            for(int d = 0; d < pointCountDepth; d++) {
                for(int h = 0; h < pointCountHeight; h++) {
                    for(int w = 0; w < pointCountWidth; w++) {
                        bool isSelected = generator.IsPointSelected(w, h, d);

                        spheres[w, h, d].GetComponent<MeshRenderer>().sharedMaterial.color = isSelected ? Color.green : Color.red;
                    }
                }
            }
        }

        private void SphereNameToIndex(string name, ref int w, ref int h, ref int d) {
            string[] nameSplit = name.Split('_');
            if(nameSplit.Length < 4) {
                Debug.LogError($"Sphere Name Changed. name: {name}");

                return;
            }

            w = -1;
            if(!int.TryParse(nameSplit[1], out w)) {
                Debug.LogError($"Index parse failed. w: {nameSplit[1]}");

                return;
            }
            h = -1;
            if(!int.TryParse(nameSplit[2], out h)) {
                Debug.LogError($"Index parse failed. h: {nameSplit[2]}");

                return;
            }
            d = -1;
            if(!int.TryParse(nameSplit[3], out d)) {
                Debug.LogError($"Index parse failed. d: {nameSplit[3]}");

                return;
            }
        }
    }
}