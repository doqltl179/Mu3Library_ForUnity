using UnityEngine;

namespace Mu3Library.CameraUtil {
    public class SimpleCameraThirdPersonView : MonoBehaviour {
        public UpdateFunc UpdateExecution {
            get => updateExecution;
            set => updateExecution = value;
        }
        [SerializeField] private UpdateFunc updateExecution;

        private Camera camera;
        private Transform target;

        [Space(20)]
        [SerializeField] private KeyCode keyRotate = KeyCode.Mouse1;
        public bool InverseRotate {
            get => inverseRotate;
            set => inverseRotate = value;
        }
        [SerializeField] private bool inverseRotate = true;

        /// <summary>
        /// "currentRadius"의 값이 0이 되면 카메라가 회전하지 않는 현상이 있어 이를 막아주기 위해 사용한다.
        /// </summary>
        private const float radiusLimitMin = 0.0001f;
        public float RadiusMin {
            get => radiusMin;
            set {
                radiusMin = Mathf.Max(radiusLimitMin, value);
                radiusMax = Mathf.Max(radiusMin, radiusMax);
            }
        }
        [SerializeField] private float radiusMin = 1;
        public float RadiusMax {
            get => radiusMax;
            set {
                radiusMax = Mathf.Max(radiusLimitMin, value);
                radiusMin = Mathf.Min(radiusMin, radiusMax);
            }
        }
        [SerializeField] private float radiusMax = 10;

        private float currentRadius;
        private float currentRadiusLerp;

        public Vector3 LocalPositionOffset {
            get => localPositionOffset;
            set => localPositionOffset = value;
        }
        [SerializeField] private Vector3 localPositionOffset = Vector3.zero;

        public float VerticalAngleDegMin {
            get => verticalAngleDegMin;
            set {
                verticalAngleDegMin = Mathf.Max(-89, value);
                verticalAngleDegMax = Mathf.Max(verticalAngleDegMin, verticalAngleDegMax);
            }
        }
        [Space(20)]
        [SerializeField, Range(-89, 89)] private float verticalAngleDegMin = -45f;
        public float VerticalAngleDegMax {
            get => verticalAngleDegMax;
            set {
                verticalAngleDegMax = Mathf.Min(89, value);
                verticalAngleDegMin = Mathf.Min(verticalAngleDegMin, verticalAngleDegMax);
            }
        }
        [SerializeField, Range(-89, 89)] private float verticalAngleDegMax = 60f;

        public float ZoomWeight {
            get => zoomWeight;
            set => zoomWeight = Mathf.Clamp01(value);
        }
        [Space(20)]
        [SerializeField, Range(0, 1)] private float zoomWeight = 0.94f;
        public float ZoomSpeed {
            get => zoomSpeed;
            set => zoomSpeed = Mathf.Max(0, value);
        }
        [SerializeField] private float zoomSpeed = 3.0f;

        public float RotateWeight {
            get => rotateWeight;
            set => rotateWeight = Mathf.Clamp01(value);
        }
        [SerializeField, Range(0, 1)] private float rotateWeight = 0.94f;
        public float RotateSpeed {
            get => rotateSpeed;
            set => rotateSpeed = Mathf.Max(0, value);
        }
        [SerializeField] private float rotateSpeed = 2f;

        public float LookWeight {
            get => lookWeight;
            set => lookWeight = Mathf.Clamp01(value);
        }
        [SerializeField, Range(0, 1)] private float lookWeight = 1f;

        /// <summary>
        /// target.right를 기준으로 반시계방향 회전하도록 한다.
        /// </summary>
        private float currentHorizontalAngleDeg;
        private float currentHorizontalAngleDegLerp;
        private float currentVerticalAngleDeg;
        private float currentVerticalAngleDegLerp;

        // 화면 밖에 나갔다가 다시 포커싱이 되면 'Input.mousePositionDelta' 값이 이상하게 나오기 때문에
        // 한 프레임을 건너 뛰어 'Input.mousePositionDelta' 값을 정상화 한다.
        private bool skipOneFrame = false;



#if UNITY_EDITOR
        [SerializeField, HideInInspector] private float check_radiusMin;
        [SerializeField, HideInInspector] private float check_radiusMax;

        [SerializeField, HideInInspector] private float check_verticalAngleDegMin;
        [SerializeField, HideInInspector] private float check_verticalAngleDegMax;

        private void OnValidate() {
            if(updateExecution == UpdateFunc.FixedUpdate) {
                Debug.LogError("'Update Execution' can not be FixedUpdate.");

                updateExecution = UpdateFunc.Update;
            }

            if(radiusMin != check_radiusMin) {
                radiusMin = Mathf.Max(radiusMin, radiusLimitMin);
                check_radiusMin = radiusMin;

                radiusMax = Mathf.Max(radiusMin, radiusMax);
            }
            if(radiusMax != check_radiusMax) {
                radiusMax = Mathf.Max(radiusMax, radiusLimitMin);
                check_radiusMax = radiusMax;

                radiusMin = Mathf.Min(radiusMin, radiusMax);
            }

            if(verticalAngleDegMin != check_verticalAngleDegMin) {
                check_verticalAngleDegMin = verticalAngleDegMin;
                verticalAngleDegMax = Mathf.Max(verticalAngleDegMin, verticalAngleDegMax);
            }
            if(verticalAngleDegMax != check_verticalAngleDegMax) {
                check_verticalAngleDegMax = verticalAngleDegMax;
                verticalAngleDegMin = Mathf.Min(verticalAngleDegMin, verticalAngleDegMax);
            }
        }
#endif

        private void OnEnable() {
            Application.focusChanged += OnFocusChanged;
        }

        private void OnDestroy() {
            Application.focusChanged -= OnFocusChanged;
        }

        private void OnFocusChanged(bool focus) {
            if(focus) {
                skipOneFrame = true;
            }
        }

        // "FixedUpdate"는 무시한다.
        //private void FixedUpdate() {

        //}

        private void Update() {
            if(updateExecution == UpdateFunc.Update) {
                UpdateFunction();
            }
        }

        private void LateUpdate() {
            if(updateExecution == UpdateFunc.LateUpdate) {
                UpdateFunction();
            }
        }

        private void UpdateFunction() {
            /*if(!Application.isFocused) {
                return;
            }
            else */
            if(skipOneFrame) {
                skipOneFrame = false;

                return;
            }

            if(camera == null || target == null) {
                return;
            }

            if(Input.GetKeyDown(keyRotate)) {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else if(Input.GetKeyUp(keyRotate)) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            const float lerpSmoothing = Mathf.PI;

            currentRadius += -Input.mouseScrollDelta.y * zoomSpeed;
            currentRadius = Mathf.Clamp(currentRadius, radiusMin, radiusMax);
            float radiusLerpT = 1.0f - Mathf.Pow(1.0f - zoomWeight, Time.deltaTime * lerpSmoothing);
            currentRadiusLerp = Mathf.Lerp(currentRadiusLerp, currentRadius, radiusLerpT);

            if(Input.GetKey(keyRotate)) {
                float deltaX = Input.mousePositionDelta.x / Screen.width;
                float deltaY = Input.mousePositionDelta.y / Screen.height;
                if(inverseRotate) {
                    deltaX *= -1;
                    deltaY *= -1;
                }

                currentHorizontalAngleDeg += deltaX * rotateSpeed * 360;

                currentVerticalAngleDeg += deltaY * rotateSpeed * 360;
                currentVerticalAngleDeg = Mathf.Clamp(currentVerticalAngleDeg, verticalAngleDegMin, verticalAngleDegMax);
            }

            float rotateLerpT = 1.0f - Mathf.Pow(1.0f - rotateWeight, Time.deltaTime * lerpSmoothing);
            currentHorizontalAngleDegLerp = Mathf.Lerp(currentHorizontalAngleDegLerp, currentHorizontalAngleDeg, rotateLerpT);
            currentVerticalAngleDegLerp = Mathf.Lerp(currentVerticalAngleDegLerp, currentVerticalAngleDeg, rotateLerpT);

            Vector3 pivotPosOffset = 
                target.right * localPositionOffset.x +
                target.up * localPositionOffset.y +
                target.forward * localPositionOffset.z;
            Vector3 pivotPos = target.position + pivotPosOffset;
            Vector3 anglePos = GetAnglePos(currentHorizontalAngleDegLerp, currentVerticalAngleDegLerp) * currentRadiusLerp;
            // 이 부분에 의해 Camera의 위치가 target을 기준으로 계산된다.
            Vector3 camPos = pivotPos + Quaternion.FromToRotation(Vector3.up, target.up) * anglePos;
            camera.transform.position = camPos;

            Quaternion lookRotation = Quaternion.LookRotation((pivotPos - camPos).normalized, target.up);
            float lookRotationLerpT = 1.0f - Mathf.Pow(1.0f - lookWeight, Time.deltaTime * lerpSmoothing);
            camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, lookRotation, lookRotationLerpT);
        }

        #region Utility
        public void SetCameraBehindTarget(bool immediately = false) {
            currentHorizontalAngleDeg = -90;

            if(immediately) {
                currentHorizontalAngleDegLerp = currentHorizontalAngleDeg;
                currentVerticalAngleDegLerp = currentVerticalAngleDeg;

                currentRadiusLerp = currentRadius;
            }
        }

        public void Init(Camera camera, Transform target) {
            if(camera == null) {
                Debug.LogError($"Camera is NULL.");

                return;
            }
            else if(target == null) {
                Debug.LogError($"Target is NULL.");

                return;
            }

            this.camera = camera;
            this.target = target;

            CalculateCurrentProperties(target.up);
        }
        #endregion

        /// <summary>
        /// <br/> Camera의 현재 위치를 반영한다.
        /// </summary>
        private void CalculateCurrentProperties(Vector3 upDir) {
            Vector3 pivotPosOffset =
                target.right * localPositionOffset.x +
                target.up * localPositionOffset.y +
                target.forward * localPositionOffset.z;
            Vector3 pivotPos = target.position + pivotPosOffset;
            Vector3 pivotToCam = camera.transform.position - pivotPos;
            Vector3 projectPivotToCamWithUpDir = Vector3.ProjectOnPlane(pivotToCam, upDir);

            Quaternion upRot = Quaternion.FromToRotation(Vector3.up, upDir);
            Vector3 rightDir = upRot * Vector3.right;
            float rightDotPivotToCam = Vector3.Dot(rightDir, projectPivotToCamWithUpDir.normalized);
            currentHorizontalAngleDeg = Mathf.Acos(rightDotPivotToCam) * Mathf.Rad2Deg;

            Vector3 cross = Vector3.Cross(rightDir, projectPivotToCamWithUpDir.normalized);
            float crossDotUpDir = Vector3.Dot(cross, upDir);
            if(crossDotUpDir > 0) {
                currentHorizontalAngleDeg *= -1;
            }

            float upDotPivotToCam = Vector3.Dot(upDir, pivotToCam.normalized);
            currentVerticalAngleDeg = -(Mathf.Acos(upDotPivotToCam) * Mathf.Rad2Deg - 90);
            currentVerticalAngleDeg = Mathf.Clamp(currentVerticalAngleDeg, verticalAngleDegMin, verticalAngleDegMax);

            currentRadius = pivotToCam.magnitude;
        }

        private Vector3 GetAnglePos(float horizontalAngleDeg, float verticalAngleDeg) {
            // 도(degree) → 라디안(radian) 변환
            float theta = horizontalAngleDeg * Mathf.Deg2Rad; // 수평 각도
            float phi = verticalAngleDeg * Mathf.Deg2Rad;     // 수직 각도

            // 구면 좌표계 변환 공식
            float x = Mathf.Cos(theta) * Mathf.Cos(phi);
            float y = Mathf.Sin(phi);
            float z = Mathf.Sin(theta) * Mathf.Cos(phi);

            return new Vector3(x, y, z);
        }
    }
}
