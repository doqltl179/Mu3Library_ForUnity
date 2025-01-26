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
        [SerializeField] private bool inverseRotate;

        [Space(20)]
        [SerializeField, Range(0.1f, 10.0f)] private float moveSpeed = 1.0f;
        [SerializeField, Range(0.1f, 360.0f)] private float rotateSpeed = 60.0f;

        private readonly Vector3 CameraDirUp = Vector3.up;



        /// <summary>
        /// 카메라 위치 파악용
        /// </summary>
        private void OnDrawGizmos() {
            if(camera == null) {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(camera.transform.position, 0.25f);
        }

        private void Start() {
            if(camera == null) {
                camera = Camera.main;
                if(camera == null) {
                    Debug.Log("Camera not found.");

                    enabled = false;
                }
            }
        }

        private void Update() {
            if(camera == null) {
                return;
            }

            Rotate();
            Move();
        }

        private void Rotate() {
            if(!Input.GetKey(KeyCode.Mouse1)) {
                return;
            }

            float mouseX = Mathf.Clamp(Input.GetAxis("Mouse X"), -1, 1);
            float mouseY = Mathf.Clamp(Input.GetAxis("Mouse Y"), -1, 1);
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
