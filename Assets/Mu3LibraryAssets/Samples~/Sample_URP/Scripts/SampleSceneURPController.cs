using UnityEngine;

namespace Mu3Library.Demo.URP {
    public class SampleSceneURPController : MonoBehaviour {
        [SerializeField] private GameObject[] cubes;



        private void Update() {
            for(int i = 0; i < cubes.Length; i++) {
                cubes[i].transform.Rotate(Vector3.one * (i + 1) * 5 * Time.deltaTime);
            }
        }
    }
}