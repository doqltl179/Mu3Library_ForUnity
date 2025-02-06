using Mu3Library.EditorOnly;
using UnityEngine;

namespace Mu3Library.CameraUtil {
    /*
     * 'height'는 'upDirection'을 기준으로 이동하고,
     * 'localPositionOffset.y'는 'target.up'을 기준으로 이동한다.
     */
    public class SimpleCameraThirdPersonView : MonoBehaviour {
        private Camera camera;
        private Transform target;

        public float Height {
            get => height;
            set => height = value;
        }
        [SerializeField] private float height = 0;

        public float RadiusMin {
            get => radiusMin;
            set {
                if(value < 0) {
                    value = 0;
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
                if(value < 0) {
                    value = 0;
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
        private Quaternion upRotation = Quaternion.identity;

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

                zoomWeight = value;
            }
        }
        [Space(20)]
        [SerializeField, Range(0, 1)] private float zoomWeight = 0.04f;
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

                rotateWeight = value;
            }
        }
        [SerializeField, Range(0, 1)] private float rotateWeight = 0.04f;
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

        public float MoveWeight {
            get => moveWeight;
            set {
                if(value < 0) {
                    value = 0;
                }

                moveWeight = value;
            }
        }
        [SerializeField, Range(0, 1)] private float moveWeight = 0.04f;
        public float MoveSpeed {
            get => moveSpeed;
            set {
                if(value < 0) {
                    value = 0;
                }

                moveSpeed = value;
            }
        }
        [SerializeField] private float moveSpeed = 5f;

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
            if(radiusMin != check_radiusMin) {
                radiusMin = Mathf.Max(radiusMin, 0);
                ClampToMinimum(radiusMin, ref radiusMax);
                check_radiusMin = radiusMin;
            }
            if(radiusMax != check_radiusMax) {
                radiusMax = Mathf.Max(radiusMax, 0);
                ClampToMaximum(radiusMax, ref radiusMin);
                check_radiusMax = radiusMax;
            }

            if(upDirection != check_upDirection) {
                upDirection = upDirection.normalized;
                check_upDirection = upDirection;
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

        private void Start() {
            Application.focusChanged += (focus) => {
                //if(!focus) {
                //    skipOneFrame = true;
                //}
                if(focus) {
                    skipOneFrame = true;
                }
            };
        }

        private void Update() {
            /*if(!Application.isFocused) {
                return;
            }
            else */if(skipOneFrame) {
                skipOneFrame = false;

                return;
            }

            if(camera == null || target == null) {
                return;
            }

            currentRadius += -Input.mouseScrollDelta.y * zoomSpeed;
            currentRadius = Mathf.Clamp(currentRadius, radiusMin, radiusMax);
            currentRadiusLerp = Mathf.Lerp(currentRadiusLerp, currentRadius, zoomWeight);

            if(Input.GetKey(KeyCode.Mouse1)) {
                float deltaX = -Input.mousePositionDelta.x / Screen.width;
                currentHorizontalAngleDeg += deltaX * rotateSpeed * 360;

                float deltaY = -Input.mousePositionDelta.y / Screen.height;
                currentVerticalAngleDeg += deltaY * rotateSpeed * 360;
                currentVerticalAngleDeg = Mathf.Clamp(currentVerticalAngleDeg, verticalAngleDegMin, verticalAngleDegMax);
            }

            currentHorizontalAngleDegLerp = Mathf.Lerp(currentHorizontalAngleDegLerp, currentHorizontalAngleDeg, rotateWeight);
            currentVerticalAngleDegLerp = Mathf.Lerp(currentVerticalAngleDegLerp, currentVerticalAngleDeg, rotateWeight);

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
            camera.transform.LookAt(newPivotPos, upDirection);

            DebugShape.DebugDrawSphere(newPivotPos, 0.2f, Vector3.forward, Vector3.right, Vector3.up, Color.red);
        }

        #region Utility
        /// <summary>
        /// 카메라가 즉시 옮겨진다.
        /// </summary>
        public void SetCameraBehindTargetImmediately(bool initVerticalAngle = true, bool initRadius = true) {
            if(camera == null || target == null) {
                return;
            }

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
            if(camera == null || target == null) {
                return;
            }

            Vector3 forward = target.forward;
            Vector3 back = new Vector3(-forward.x, forward.y, -forward.z);
            float horizontalAngleDeg = Mathf.Atan2(back.z, back.x);
            currentHorizontalAngleDeg = horizontalAngleDeg;

            if(initVerticalAngle) {
                currentVerticalAngleDeg = Mathf.Lerp(verticalAngleDegMin, verticalAngleDegMax, 0.75f);
            }
            if(initRadius) {
                currentRadius = Mathf.Lerp(radiusMin, radiusMax, 0.5f);
            }
        }

        public void Init(Camera camera, Transform target, bool initParams) {
            if(initParams) {
                height = 0;
                radiusMin = 1;
                radiusMax = 10;
                localPositionOffset = Vector3.zero;
                upDirection = Vector3.up;
                upRotation = Quaternion.identity;

                verticalAngleDegMin = -45f;
                verticalAngleDegMax = 60f;

                zoomWeight = 0.04f;
                zoomSpeed = 3.0f;

                rotateWeight = 0.04f;
                rotateSpeed = 2f;
                moveWeight = 0.04f;
                moveSpeed = 5f;
            }

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
