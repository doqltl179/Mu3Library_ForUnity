using UnityEngine;

namespace Mu3Library.Raycast {
    public class ZeroDistanceRayCapsuleHelper {
        public Transform Origin { get; private set; }
        public float Radius { get; private set; }
        public float Height { get; private set; }
        public int Mask { get; private set; }

        public RaycastHit[] Hits { get; private set; }



        // -1 => All Layers
        public ZeroDistanceRayCapsuleHelper(Transform origin, float radius, float height, int mask = -1) {
            Origin = origin;
            Radius = radius;
            Height = height;
            Mask = mask;
        }

        #region Utility
        public bool HasEqualOnRigidbody<T>(T component) where T : UnityEngine.Object {
            foreach(RaycastHit hit in Hits) {
                if(hit.rigidbody != null && hit.rigidbody.GetComponent<T>() == component) {
                    return true;
                }
            }
            return false;
        }

        public bool HasEqualOnCollider<T>(T component) where T : UnityEngine.Object {
            foreach(RaycastHit hit in Hits) {
                if(hit.collider.GetComponent<T>() == component) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Raycast
        public bool Raycast() {
            Hits = Physics.CapsuleCastAll(
                Origin.transform.position,
                Origin.transform.position + Vector3.up * Height,
                Radius,
                Vector3.forward,
                0.0f,
                Mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * Height,
                Radius,
                Vector3.forward,
                0.0f,
                Mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1, int mask) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * Height,
                Radius,
                Vector3.forward,
                0.0f,
                mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1, float height) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * height,
                Radius,
                Vector3.forward,
                0.0f,
                Mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1, float height, int mask) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * height,
                Radius,
                Vector3.forward,
                0.0f,
                mask);
            return Hits.Length > 0;
        }

        public bool Raycast(float radius) {
            Hits = Physics.CapsuleCastAll(
                Origin.transform.position,
                Origin.transform.position + Vector3.up * Height,
                radius,
                Vector3.forward,
                0.0f,
                Mask);
            return Hits.Length > 0;
        }

        public bool Raycast(float radius, int mask) {
            Hits = Physics.CapsuleCastAll(
                Origin.transform.position,
                Origin.transform.position + Vector3.up * Height,
                radius,
                Vector3.forward,
                0.0f,
                mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1, float height, float radius) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * height,
                radius,
                Vector3.forward,
                0.0f,
                Mask);
            return Hits.Length > 0;
        }

        public bool Raycast(Vector3 p1, float height, float radius, int mask) {
            Hits = Physics.CapsuleCastAll(
                p1,
                p1 + Vector3.up * height,
                radius,
                Vector3.forward,
                0.0f,
                mask);
            return Hits.Length > 0;
        }
        #endregion
    }
}