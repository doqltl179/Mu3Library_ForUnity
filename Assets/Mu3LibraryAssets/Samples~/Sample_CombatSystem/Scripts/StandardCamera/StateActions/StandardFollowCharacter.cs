using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    /// <summary>
    /// 마우스로만 제어한다.
    /// </summary>
    public class StandardFollowCharacter : ICameraStateAction {
        private Camera camera;
        private CharacterController controller;

        private float controlRadiusMin = -1;
        private float controlRadiusMax = -1;
        private float controlRadius = -1;
        private float controlRadiusLerp = -1;

        private const float verticalAngleDegMin = -45f;
        private const float verticalAngleDegMax = 60f;
        private float verticalAngleDeg = 25f;
        private float verticalAngleDegLerp = 25f;

        private float horizontalAngleDeg = -1;
        private float horizontalAngleDegLerp = -1;

        // 화면 밖에 나갔다가 다시 포커싱이 되면 'Input.mousePositionDelta' 값이 이상하게 나오기 때문에
        // 한 프레임을 건너 뛰어 'Input.mousePositionDelta' 값을 정상화 한다.
        private bool skipOneFrame = false;



        public StandardFollowCharacter(Camera camera, CharacterController controller) {
            this.camera = camera;
            this.controller = controller;

            //controlRadiusMin = controller.Height * 0.5f;
            controlRadiusMin = 1.0f;
            controlRadiusMax = controller.Height * 5.0f;
            controlRadius = (controlRadiusMin + controlRadiusMax) * 0.4f;
            controlRadiusLerp = controlRadius;

            // 캐릭터의 뒷편에 자리잡도록 초기화.
            Vector3 forward = controller.Forward;
            Vector3 back = new Vector3(-forward.x, 0, -forward.z).normalized;
            float backAngleDeg = Mathf.Atan2(back.z, back.x) * Mathf.Rad2Deg;
            horizontalAngleDeg = backAngleDeg;
            horizontalAngleDegLerp = horizontalAngleDeg;
        }

        #region Utility
        public void Enter() {
            Application.focusChanged += OnFocusChanged;
        }

        public void Exit() {
            Application.focusChanged -= OnFocusChanged;
        }

        // property 값들은 우선 하드코딩한다.
        // 코드 확장 시, serialize class 혹은 scriptableobject로 관리하자.
        public void Update() {
            const float zoomWeight = 0.3f;
            const float zoomSpeed = 3.0f;
            const float rotateSpeed = 2.0f;
            const float moveSpeed = 5.0f;

            controlRadius += -Input.mouseScrollDelta.y * zoomWeight;
            controlRadius = Mathf.Clamp(controlRadius, controlRadiusMin, controlRadiusMax);
            controlRadiusLerp = Mathf.Lerp(controlRadiusLerp, controlRadius, Time.deltaTime * zoomSpeed);

            if(Application.isFocused) {
                if(skipOneFrame) {
                    skipOneFrame = false;
                }
                else if(Input.GetKey(KeyCode.Mouse1)) {
                    float normalizedX = -Input.mousePositionDelta.x / Screen.width;
                    horizontalAngleDeg += normalizedX * rotateSpeed * 360;

                    float normalizedY = -Input.mousePositionDelta.y / Screen.height;
                    verticalAngleDeg += normalizedY * rotateSpeed * 360;
                    verticalAngleDeg = Mathf.Clamp(verticalAngleDeg, verticalAngleDegMin, verticalAngleDegMax);
                }
            }

            horizontalAngleDegLerp = Mathf.Lerp(horizontalAngleDegLerp, horizontalAngleDeg, Time.deltaTime * moveSpeed);
            verticalAngleDegLerp = Mathf.Lerp(verticalAngleDegLerp, verticalAngleDeg, Time.deltaTime * moveSpeed);

            Vector3 pivotPos = controller.Position + Vector3.up * controller.Height * 0.9f;
            Vector3 camPos = GetRotatePos(pivotPos, horizontalAngleDegLerp, verticalAngleDegLerp, controlRadiusLerp);

            // 만약 오브젝트를 뚫어버리는 문제를 해결하고 싶다면 이곳에 추가 코드를 작성하자.
            Vector3 pivotToCamPos = camPos - pivotPos;
            int castLayerMask = ~(1 << CharacterController.Layer);
            RaycastHit castHit;
            if(Physics.Raycast(pivotPos, pivotToCamPos.normalized, out castHit, pivotToCamPos.magnitude, castLayerMask)) {
                camPos = castHit.point;
            }

            camera.transform.position = camPos;
            camera.transform.LookAt(pivotPos, Vector3.up);
            //camera.transform.forward = (pivotPos - camPos).normalized;
        }
        #endregion

        private void OnFocusChanged(bool isFocused) {
            if(!isFocused) {
                skipOneFrame = true;
            }
        }

        private Vector3 GetRotatePos(Vector3 pivot, float horizontalAngleDeg, float verticalAngleDeg, float radius) {
            // 도(degree) → 라디안(radian) 변환
            float theta = horizontalAngleDeg * Mathf.Deg2Rad; // 수평 각도
            float phi = verticalAngleDeg * Mathf.Deg2Rad;     // 수직 각도

            // 구면 좌표계 변환 공식
            float x = radius * Mathf.Cos(theta) * Mathf.Cos(phi);
            float y = radius * Mathf.Sin(phi);
            float z = radius * Mathf.Sin(theta) * Mathf.Cos(phi);

            // 회전 중심점(pivot)에 좌표를 더함
            return pivot + new Vector3(x, y, z);
        }
    }
}