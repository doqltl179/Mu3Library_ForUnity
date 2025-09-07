using UnityEngine;

namespace Mu3Library.Base.CameraUtil {
    public class SimpleCameraFirstPersonView : MonoBehaviour {
        public UpdateFunc UpdateExecution {
            get => updateExecution;
            set => updateExecution = value;
        }
        [SerializeField] private UpdateFunc updateExecution;

        private Camera camera;
        private Transform target;

        public bool InverseRotate {
            get => inverseRotate;
            set => inverseRotate = value;
        }
        [Space(20)]
        [SerializeField] private bool inverseRotate = true;

        public float Height {
            get => height;
            set => height = value;
        }
        [Space(20)]
        [SerializeField] private float height = 0;

        public float Radius {
            get => radius;
            set => radius = Mathf.Max(0, value);
        }
        [SerializeField] private float radius = 0;

        private float currentRadius;

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

        private float currentHorizontalAngleDeg;
        private float currentHorizontalAngleDegLerp;
        private float currentVerticalAngleDeg;
        private float currentVerticalAngleDegLerp;

        // 화면 밖에 나갔다가 다시 포커싱이 되면 'Input.mousePositionDelta' 값이 이상하게 나오기 때문에
        // 한 프레임을 건너 뛰어 'Input.mousePositionDelta' 값을 정상화 한다.
        private bool skipOneFrame = false;



#if UNITY_EDITOR
        [SerializeField, HideInInspector] private float check_radius;

        [SerializeField, HideInInspector] private float check_verticalAngleDegMin;
        [SerializeField, HideInInspector] private float check_verticalAngleDegMax;

        private void OnValidate() {
            if(updateExecution == UpdateFunc.FixedUpdate) {
                Debug.LogError("'Update Execution' can not be FixedUpdate.");

                updateExecution = UpdateFunc.Update;
            }

            if(radius != check_radius) {
                radius = Mathf.Max(0, radius);
                check_radius = radius;
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

            //if(Input.GetKeyDown(keyRotate)) {
            //    Cursor.visible = false;
            //    Cursor.lockState = CursorLockMode.Locked;
            //}
            //else if(Input.GetKeyUp(keyRotate)) {
            //    Cursor.visible = true;
            //    Cursor.lockState = CursorLockMode.None;
            //}

            const float lerpSmoothing = Mathf.PI;

            float radiusLerpT = 1.0f - Mathf.Pow(1.0f - zoomWeight, Time.deltaTime * lerpSmoothing);
            currentRadius = Mathf.Lerp(currentRadius, radius, radiusLerpT);

            float deltaX = Input.mousePositionDelta.x / Screen.width;
            float deltaY = -Input.mousePositionDelta.y / Screen.height;
            if(inverseRotate) {
                deltaX *= -1;
                deltaY *= -1;
            }

            currentHorizontalAngleDeg += deltaX * rotateSpeed * 360;

            currentVerticalAngleDeg += deltaY * rotateSpeed * 360;
            currentVerticalAngleDeg = Mathf.Clamp(currentVerticalAngleDeg, verticalAngleDegMin, verticalAngleDegMax);

            float rotateLerpT = 1.0f - Mathf.Pow(1.0f - rotateWeight, Time.deltaTime * lerpSmoothing);
            currentHorizontalAngleDegLerp = Mathf.Lerp(currentHorizontalAngleDegLerp, currentHorizontalAngleDeg, rotateLerpT);
            currentVerticalAngleDegLerp = Mathf.Lerp(currentVerticalAngleDegLerp, currentVerticalAngleDeg, rotateLerpT);

            Vector3 pivotPos = target.position + target.up * height;
            Vector3 anglePos = GetAnglePos(currentHorizontalAngleDegLerp, currentVerticalAngleDegLerp) * currentRadius;
            Vector3 camPos = pivotPos + Quaternion.FromToRotation(Vector3.up, target.up) * anglePos;
            camera.transform.position = camPos;

            Quaternion lookRotation = Quaternion.LookRotation((camPos - pivotPos).normalized, target.up);
            float lookRotationLerpT = 1.0f - Mathf.Pow(1.0f - lookWeight, Time.deltaTime * lerpSmoothing);
            camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, lookRotation, lookRotationLerpT);
        }

        #region Utility
        public void SetCameraForwardTarget(bool immediately) {
            currentHorizontalAngleDeg = 90;
            currentVerticalAngleDeg = 0;

            if(immediately) {
                currentHorizontalAngleDegLerp = currentHorizontalAngleDeg;
                currentVerticalAngleDegLerp = currentVerticalAngleDeg;

                currentRadius = radius;
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

            CalculateCurrentProperties();
        }
        #endregion

        /// <summary>
        /// <br/> Camera의 현재 위치를 반영한다.
        /// </summary>
        private void CalculateCurrentProperties() {
            Vector3 pivotPos = target.position + target.up * height;
            Vector3 pivotToCam = camera.transform.position - pivotPos;
            Vector3 projectPivotToCamWithUpDir = Vector3.ProjectOnPlane(pivotToCam, target.up);

            float rightDotPivotToCam = Vector3.Dot(target.right, projectPivotToCamWithUpDir.normalized);
            currentHorizontalAngleDeg = Mathf.Acos(rightDotPivotToCam) * Mathf.Rad2Deg;

            Vector3 cross = Vector3.Cross(target.right, projectPivotToCamWithUpDir.normalized);
            float crossDotUpDir = Vector3.Dot(cross, target.up);
            if(crossDotUpDir > 0) {
                currentHorizontalAngleDeg *= -1;
            }

            float upDotPivotToCam = Vector3.Dot(target.up, pivotToCam.normalized);
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
