using UnityEngine;
using UnityEngine.InputSystem;

namespace Mu3Library.Utility {
    public class InputSystemHelper : GenericSingleton<InputSystemHelper> {
        public InputSystem_Actions Controls => controls;
        private InputSystem_Actions controls;



        private void Awake() {
            controls = new InputSystem_Actions();

            // 시작하자마자 모든 action을 사용 가능하게 만든다.
            foreach(InputActionMap map in controls.asset.actionMaps) {
                foreach(InputAction action in map.actions) {
                    action.Enable();
                }
            }
        }
    }
}