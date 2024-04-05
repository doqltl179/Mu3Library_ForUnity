namespace Mu3Library.Raycast {
    public struct HitPointWithComponent<T> where T : UnityEngine.Object {
        public T Component;
        public UnityEngine.Vector3 HitPoint;
    }
}
