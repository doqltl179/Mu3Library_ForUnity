using UnityEngine;

namespace Mu3Library.CameraUtil {
    public class SimpleCameraFreeView : MonoBehaviour {
        [SerializeField] private Camera camera;

        [Space(20)]
        [SerializeField] private KeyCode keyMoveL = KeyCode.A;
        [SerializeField] private KeyCode keyMoveR = KeyCode.D;
        [SerializeField] private KeyCode keyMoveF = KeyCode.W;
        [SerializeField] private KeyCode keyMoveB = KeyCode.S;
        [SerializeField] private KeyCode keyRun = KeyCode.LeftShift;

        [Space(20)]
        [SerializeField] private KeyCode keyRotate = KeyCode.Mouse1;

        [Space(20)]
        [SerializeField] private bool inverseRotate;

        [Space(20)]
        [SerializeField, Range(0.1f, 100.0f)] private float moveSpeed = 1.0f;
        [SerializeField, Range(0.1f, 360.0f)] private float rotateSpeed = 30.0f;

        private readonly Vector3 CameraDirUp = Vector3.up;

        // 화면 밖에 나갔다가 다시 포커싱이 되면 'Input.mousePositionDelta' 값이 이상하게 나오기 때문에
        // 한 프레임을 건너 뛰어 'Input.mousePositionDelta' 값을 정상화 한다.
        private bool skipOneFrame = false;



        private void Start() {
            if(camera == null) {
                camera = Camera.main;
                if(camera == null) {
                    Debug.Log("Camera not found.");

                    enabled = false;
                }
            }

            Application.focusChanged += (focus) => {
                if(!focus) {
                    skipOneFrame = true;
                }
            };
        }

        private void Update() {
            if(!Application.isFocused) {
                return;
            }
            else {
                if(skipOneFrame) {
                    skipOneFrame = false;

                    return;
                }
            }

            if(camera == null) {
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

            Rotate();
            Move();
        }

        private void Rotate() {
            if(!Input.GetKey(keyRotate)) {
                return;
            }

            float mouseX = Input.mousePositionDelta.x / Screen.width * 1000f;
            float mouseY = Input.mousePositionDelta.y / Screen.height * 1000f;
            if(inverseRotate) {
                mouseX *= -1;
                mouseY *= -1;
            }

            float rotateAngleDegHorizontal = mouseY * rotateSpeed * Time.deltaTime;
            float rotateAngleDegVertical = -mouseX * rotateSpeed * Time.deltaTime;

            // 새로운 회전값 계산
            Quaternion horizontalRotation = Quaternion.AngleAxis(rotateAngleDegHorizontal, camera.transform.right);
            Quaternion verticalRotation = Quaternion.AngleAxis(rotateAngleDegVertical, CameraDirUp);

            // 회전값 적용 (수평 및 수직 회전 결합)
            Quaternion currentRotation = camera.transform.rotation;
            camera.transform.rotation = verticalRotation * horizontalRotation * currentRotation;
        }

        private void Move() {
            Vector3 moveDir = Vector3.zero;

            if(Input.GetKey(keyMoveL)) moveDir += -camera.transform.right;
            if(Input.GetKey(keyMoveR)) moveDir += camera.transform.right;
            if(Input.GetKey(keyMoveF)) moveDir += camera.transform.forward;
            if(Input.GetKey(keyMoveB)) moveDir += -camera.transform.forward;

            if(moveDir.x == 0 && moveDir.y == 0 && moveDir.z == 0) {
                return;
            }

            moveDir = moveDir.normalized;

            float runSpeed = Input.GetKey(keyRun) ? 2.0f : 1.0f;

            camera.transform.position += moveDir * moveSpeed * runSpeed * Time.deltaTime;
        }
    }
}
