using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class SampleSceneCombatSystemController : MonoBehaviour {




        private void Start() {
            CombatSystemManager.Instance.InitSystem();
        }
    }
}