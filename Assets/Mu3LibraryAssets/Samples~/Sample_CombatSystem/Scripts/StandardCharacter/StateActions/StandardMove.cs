using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardMove : ICharacterStateAction {
        private StandardCharacter controller;
        private StandardCharacterProperties properties;

        private float acceleration = 0;
        private float accelerationLerp = 0;

        private bool isEntered = false;



        public StandardMove(StandardCharacter controller) {
            this.controller = controller;
            properties = controller.Properties;
        }

        #region Utility
        public void Enter() {
            isEntered = true;
        }

        public bool EnterCheck() {
            return (properties.MoveAxis.x != 0 || properties.MoveAxis.y != 0) &&
                controller.GetAnimatorParameter_AttackMotionIndex() < 0;
        }

        public void Exit() {
            isEntered = false;
        }

        public bool ExitCheck() {
            return (properties.MoveAxis.x == 0 && properties.MoveAxis.y == 0) ||
                controller.GetAnimatorParameter_AttackMotionIndex() >= 0;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        // property 값들은 우선 하드코딩한다.
        // 코드 확장 시, serialize class 혹은 scriptableobject로 관리하자.
        public void Update() {
            RotateUpdate();
            MoveUpdate();
        }

        public void UpdateAlways() {
            if(!isEntered) {
                RotateUpdateAlways();
                MoveUpdateAlways();
            }
        }
        #endregion

        private void MoveUpdateAlways() {
            acceleration -= Time.deltaTime * properties.AccelerationLevel;
            if(acceleration < 0) acceleration = 0;
            accelerationLerp = Mathf.Lerp(accelerationLerp, acceleration, Time.deltaTime * properties.AccelerationLevel * 2);

            controller.Position += controller.Forward * Time.deltaTime * properties.MoveSpeed * accelerationLerp;
            controller.SetAnimatorParameter_MoveBlend(accelerationLerp);
        }

        private void MoveUpdate() {
            float dotForward = Vector3.Dot(controller.Forward, properties.MoveDir);
            acceleration += dotForward * Time.deltaTime * properties.AccelerationLevel;

            // 달리기
            if(Input.GetKey(KeyCode.LeftShift) && properties.IsGrounded) {
                acceleration = Mathf.Clamp(acceleration, 0, 1);
            }
            else {
                acceleration = Mathf.Clamp(acceleration, 0, 0.5f);
            }
            accelerationLerp = Mathf.Lerp(accelerationLerp, acceleration, Time.deltaTime * properties.AccelerationLevel);

            controller.Position += properties.MoveDir * Time.deltaTime * properties.MoveSpeed * accelerationLerp;
            controller.SetAnimatorParameter_MoveBlend(accelerationLerp);
        }

        private void RotateUpdateAlways() {
            if(controller.GetAnimatorParameter_AttackMotionIndex() >= 0 && controller.IsAnimatorInTransition()) {
                controller.Rotation = Quaternion.Lerp(controller.Rotation, Quaternion.LookRotation(properties.MoveDir, Vector3.up), Time.deltaTime * properties.RotateSpeed);
            }
        }

        private void RotateUpdate() {
            controller.Rotation = Quaternion.Lerp(controller.Rotation, Quaternion.LookRotation(properties.MoveDir, Vector3.up), Time.deltaTime * properties.RotateSpeed);
        }
    }
}