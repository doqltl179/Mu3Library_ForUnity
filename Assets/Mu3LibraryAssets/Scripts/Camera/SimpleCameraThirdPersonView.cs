using UnityEngine;

namespace Mu3Library.CameraUtil {
    /*
     * 'height'는 'upDirection'을 기준으로 이동하고,
     * 'localPositionOffset.y'는 'target.up'을 기준으로 이동한다.
     */
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

        public float Height {
            get => height;
            set => height = value;
        }
        [Space(20)]
        [SerializeField] private float height = 0;

        /// <summary>
        /// "currentRadius"의 값이 0이 되면 카메라 회전에 되지 않는 현상이 있어 이를 막아주기 위해 사용한다.
        /// </summary>
        private const float radiusLimitMin = 0.0001f;
        public float RadiusMin {
            get => radiusMin;
            set {
                if(value < radiusLimitMin) {
                    value = radiusLimitMin;
                }
                else if(value > radiusMax) {
                    value = radiusMax;
                }

                radiusMin = value;
            }
        }
        [SerializeField] private float radiusMin = 1;
        public float RadiusMax {
            get => radiusMax;
            set {
                if(value < radiusLimitMin) {
                    value = radiusLimitMin;
                }
                else if(value < radiusMin) {
                    value = radiusMin;
                }

                radiusMax = value;
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

        public Vector3 UpDirection {
            get => upDirection;
            set {
                upDirection = value.normalized;
                upRotation = Quaternion.FromToRotation(Vector3.up, upDirection);
            }
        }
        [SerializeField] private Vector3 upDirection = Vector3.up;
        [SerializeField, HideInInspector] private Quaternion upRotation = Quaternion.identity;

        public float VerticalAngleDegMin {
            get => verticalAngleDegMin;
            set {
                if(value < -89) {
                    value = -89;
                }
                else if(value > verticalAngleDegMax) {
                    value = verticalAngleDegMax;
                }

                verticalAngleDegMin = value;
            }
        }
        [Space(20)]
        [SerializeField, Range(-89, 89)] private float verticalAngleDegMin = -45f;
        public float VerticlaAngleDegMax {
            get => verticalAngleDegMax;
            set {
                if(value > 89) {
                    value = 89;
                }
                else if(value < verticalAngleDegMin) {
                    value = verticalAngleDegMin;
                }

                verticalAngleDegMax = value;
            }
        }
        [SerializeField, Range(-89, 89)] private float verticalAngleDegMax = 60f;

        public float ZoomWeight {
            get => zoomWeight;
            set {
                if(value < 0) {
                    value = 0;
                }
                else if(value > 1) {
                    value = 1;
                }

                zoomWeight = value;
            }
        }
        [Space(20)]
        [SerializeField, Range(0, 1)] private float zoomWeight = 0.94f;
        public float ZoomSpeed {
            get => zoomSpeed;
            set {
                if(value < 0) {
                    value = 0;
                }

                zoomSpeed = value;
            }
        }
        [SerializeField] private float zoomSpeed = 3.0f;

        public float RotateWeight {
            get => rotateWeight;
            set {
                if(value < 0) {
                    value = 0;
                }
                else if(value > 1) {
                    value = 1;
                }

                rotateWeight = value;
            }
        }
        [SerializeField, Range(0, 1)] private float rotateWeight = 0.94f;
        public float RotateSpeed {
            get => rotateSpeed;
            set {
                if(value < 0) {
                    value = 0;
                }

                rotateSpeed = value;
            }
        }
        [SerializeField] private float rotateSpeed = 2f;

        public float LookWeight {
            get => lookWeight;
            set {
                if(value < 0) {
                    value = 0;
                }
                else if(value > 1) {
                    value = 1;
                }

                lookWeight = value;
            }
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
        [SerializeField, HideInInspector] private float check_radiusMin;
        [SerializeField, HideInInspector] private float check_radiusMax;

        [SerializeField, HideInInspector] private Vector3 check_upDirection;

        [SerializeField, HideInInspector] private float check_verticalAngleDegMin;
        [SerializeField, HideInInspector] private float check_verticalAngleDegMax;

        private void OnValidate() {
            if(updateExecution == UpdateFunc.FixedUpdate) {
                Debug.LogError("'Update Execution' can not be FixedUpdate.");

                updateExecution = UpdateFunc.Update;
            }

            if(radiusMin != check_radiusMin) {
                radiusMin = Mathf.Max(radiusMin, radiusLimitMin);
                ClampToMinimum(radiusMin, ref radiusMax);
                check_radiusMin = radiusMin;
            }
            if(radiusMax != check_radiusMax) {
                radiusMax = Mathf.Max(radiusMax, radiusLimitMin);
                ClampToMaximum(radiusMax, ref radiusMin);
                check_radiusMax = radiusMax;
            }

            if(upDirection != check_upDirection) {
                upDirection = upDirection.normalized;
                check_upDirection = upDirection;

                upRotation = Quaternion.FromToRotation(Vector3.up, upDirection);
            }

            if(verticalAngleDegMin != check_verticalAngleDegMin) {
                ClampToMinimum(verticalAngleDegMin, ref verticalAngleDegMax);
                check_verticalAngleDegMin = verticalAngleDegMin;
            }
            if(verticalAngleDegMax != check_verticalAngleDegMax) {
                ClampToMaximum(verticalAngleDegMax, ref verticalAngleDegMin);
                check_verticalAngleDegMax = verticalAngleDegMax;
            }
        }

        private void ClampToMaximum(float compare, ref float value) {
            if(value > compare) {
                value = compare;
            }
        }

        private void ClampToMinimum(float compare, ref float value) {
            if(value < compare) {
                value = compare;
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

            Vector3 worldPosOffset =
                transform.forward * localPositionOffset.z +
                transform.right * localPositionOffset.x +
                transform.up * localPositionOffset.y;
            Vector3 pivotPos = target.position + worldPosOffset;
            Vector3 anglePos = GetAnglePos(pivotPos, currentHorizontalAngleDegLerp, currentVerticalAngleDegLerp, currentRadiusLerp);

            Vector3 pivotToAnglePos = anglePos - pivotPos;
            Vector3 recalVec = upRotation * pivotToAnglePos;

            Vector3 newPivotPos = pivotPos + upDirection * height;
            Vector3 camPos = newPivotPos + recalVec;
            camera.transform.position = camPos;

            Quaternion lookRotation = Quaternion.LookRotation((newPivotPos - camPos).normalized);
            float lookRotationLerpT = 1.0f - Mathf.Pow(1.0f - lookWeight, Time.deltaTime * lerpSmoothing);
            camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, lookRotation, lookRotationLerpT);
        }

        #region Utility
        /// <summary>
        /// 카메라가 즉시 옮겨진다.
        /// </summary>
        public void SetCameraBehindTargetImmediately(bool initVerticalAngle = true, bool initRadius = true) {
            SetCameraBehindTarget(initVerticalAngle, initRadius);

            currentHorizontalAngleDegLerp = currentHorizontalAngleDeg;
            if(initVerticalAngle) {
                currentVerticalAngleDegLerp = currentVerticalAngleDeg;
            }
            if(initRadius) {
                currentRadiusLerp = currentRadius;
            }
        }

        /// <summary>
        /// 카메라가 자연스럽게 이동한다.
        /// </summary>
        public void SetCameraBehindTarget(bool initVerticalAngle = true, bool initRadius = true) {
            Vector3 forward = upRotation * Vector3.forward;
            Vector3 right = upRotation * Vector3.right;

            Vector3 worldPosOffset =
                transform.forward * localPositionOffset.z +
                transform.right * localPositionOffset.x +
                transform.up * localPositionOffset.y;
            Vector3 pivotPos = target.position + worldPosOffset;
            Vector3 pivotToCam = camera.transform.position - pivotPos;

            Vector3 proj = Vector3.ProjectOnPlane(pivotToCam, upDirection).normalized;

            float dotForward = Vector3.Dot(proj, forward);
            float dotRight = Vector3.Dot(proj, right);
            float horizontalAngleDeg = Mathf.Atan2(dotForward, dotRight) * Mathf.Rad2Deg;
            currentHorizontalAngleDeg = horizontalAngleDeg;

            if(initVerticalAngle) {
                currentVerticalAngleDeg = Mathf.Lerp(verticalAngleDegMin, verticalAngleDegMax, 0.75f);
            }
            else {
                //currentVerticalAngleDegLerp = 
            }

            if(initRadius) {
                currentRadius = Mathf.Lerp(radiusMin, radiusMax, 0.5f);
            }
            else {
                currentRadiusLerp = Vector3.Distance(camera.transform.position, target.position);
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

            Vector3 targetToCam = camera.transform.position - target.position;
            Vector3 targetToCamDir = targetToCam.normalized;
            currentHorizontalAngleDeg = Mathf.Atan2(targetToCamDir.z, targetToCamDir.x) * Mathf.Rad2Deg;
            currentHorizontalAngleDegLerp = currentHorizontalAngleDeg;

            currentRadiusLerp = targetToCam.magnitude;

            this.camera = camera;
            this.target = target;
        }
        #endregion

        private Vector3 GetAnglePos(Vector3 pivot, float horizontalAngleDeg, float verticalAngleDeg, float radius) {
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
