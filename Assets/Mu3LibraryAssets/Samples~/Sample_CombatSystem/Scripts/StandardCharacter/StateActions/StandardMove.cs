using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardMove : ICharacterStateAction {
        private StandardCharacter controller;

        private const string InputAxes_Horizontal = "Horizontal";
        private const string InputAxes_Vertical = "Vertical";

        private Vector2 moveAxis = Vector2.zero;
        private Vector3 moveDir = Vector3.zero;

        private float acceleration = 0;
        private float accelerationLerp = 0;

        private bool isEntered = false;



        public StandardMove(StandardCharacter controller) {
            this.controller = controller;
        }

        #region Utility
        public void Enter() {
            isEntered = true;
        }

        public bool EnterCheck() {
            return moveAxis.x != 0 || moveAxis.y != 0;
        }

        public void Exit() {
            isEntered = false;
        }

        public bool ExitCheck() {
            return moveAxis.x == 0 && moveAxis.y == 0;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        // property 값들은 우선 하드코딩한다.
        // 코드 확장 시, serialize class 혹은 scriptableobject로 관리하자.
        public void Update() {
            Rotate();
            MoveUpdate();
        }

        public void UpdateAlways() {
            moveAxis.x = Input.GetAxisRaw(InputAxes_Horizontal);
            moveAxis.y = Input.GetAxisRaw(InputAxes_Vertical);
            moveAxis = moveAxis.normalized;

            // 메인 카메라를 기준으로 움직인다.
            Vector3 mainCameraForward = CombatSystemManager.Instance.MainCameraForward;
            Vector3 forward = new Vector3(mainCameraForward.x, 0, mainCameraForward.z).normalized;

            Vector3 mainCameraRight = CombatSystemManager.Instance.MainCameraRight;
            Vector3 right = new Vector3(mainCameraRight.x, 0, mainCameraRight.z).normalized;

            moveDir = (forward * moveAxis.y + right * moveAxis.x).normalized;

            if(!isEntered) {
                MoveUpdateAlways();
            }
        }
        #endregion

        private void MoveUpdateAlways() {
            const float moveSpeed = 2.0f;

            acceleration -= Time.deltaTime * 4.0f;
            if(acceleration < 0) acceleration = 0;
            accelerationLerp = Mathf.Lerp(accelerationLerp, acceleration, Time.deltaTime * 3.0f);

            controller.Position += controller.Forward * Time.deltaTime * moveSpeed * accelerationLerp;
            controller.ChangeAnimatorParameter_MoveBlend(accelerationLerp);
        }

        private void MoveUpdate() {
            const float moveSpeed = 2.0f;

            float dotForward = Vector3.Dot(controller.Forward, moveDir);
            acceleration += dotForward * Time.deltaTime * 5.0f;

            // 달리기
            if(Input.GetKey(KeyCode.LeftShift)) {
                acceleration = Mathf.Clamp(acceleration, 0, 1);
            }
            else {
                acceleration = Mathf.Clamp(acceleration, 0, 0.5f);
            }
            accelerationLerp = Mathf.Lerp(accelerationLerp, acceleration, Time.deltaTime * 3.0f);

            controller.Position += moveDir * Time.deltaTime * moveSpeed * accelerationLerp;
            controller.ChangeAnimatorParameter_MoveBlend(accelerationLerp);
        }

        private void Rotate() {
            const float rotateSpeed = 10.0f;

            controller.Rotation = Quaternion.Lerp(controller.Rotation, Quaternion.LookRotation(moveDir, Vector3.up), Time.deltaTime * rotateSpeed);
        }
    }
}