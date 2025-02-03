using Mu3Library.CombatSystem;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardCameraSystem : CameraSystemManager {




        #region Utility
        public override void ChangeCameraStateToFollowCharacter(Camera camera, Transform target) {
            if(currentStateAction != null) {
                currentStateAction.Exit();
            }


        }

        public override void ChangeCameraStateToFollowCharacter(Camera camera, CharacterController controller) {
            if(currentStateAction != null) {
                currentStateAction.Exit();
            }

            StandardFollowCharacter stateAction = new StandardFollowCharacter(camera, controller);
            stateAction.Enter();

            currentStateAction = stateAction;
            currentState = CameraState.FollowCharacter;

            mainCamera = camera;
        }
        #endregion
    }
}