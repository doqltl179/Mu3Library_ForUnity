using Mu3Library.CombatSystem;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardMove : ICharacterStateAction {
        private StandardCharacter controller;
        private Camera playerCamera;

        private const string InputAxes_Horizontal = "Horizontal";
        private const string InputAxes_Vertical = "Vertical";

        private Vector2 moveAxis = Vector2.zero;



        public StandardMove(StandardCharacter controller, Camera playerCamera) {
            this.controller = controller;
            this.playerCamera = playerCamera;
        }

        public void Enter() {
            
        }

        public bool EnterCheck() {
            return moveAxis.x != 0 || moveAxis.y != 0;
        }

        public void Exit() {

        }

        public bool ExitCheck() {
            return moveAxis.x == 0 && moveAxis.y == 0;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        public void Update() {

        }

        public void UpdateAlways() {
            moveAxis.x = Input.GetAxisRaw(InputAxes_Horizontal);
            moveAxis.y = Input.GetAxisRaw(InputAxes_Vertical);
        }
    }
}