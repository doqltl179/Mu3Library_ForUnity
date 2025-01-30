using Mu3Library.MarchingCubes;
using UnityEngine;

public class SampleSceneMarchingCubesController : MonoBehaviour {
    [SerializeField] private MarchingCubesGenerator marchingCubes;
    [SerializeField, Range(1, 10)] private int chunkCount = 1;



    private void Start() {
        marchingCubes.CreateChunks(chunkCount);

        marchingCubes.ForceUpdateMarchingCubes(ForceUpdateType.Cube);
    }

    private void Update() {
        if(Input.GetKeyDown(KeyCode.Space)) {
            marchingCubes.Clear();

            marchingCubes.CreateChunks(chunkCount);

            marchingCubes.ForceUpdateMarchingCubes(ForceUpdateType.Cube);
        }
    }
}
