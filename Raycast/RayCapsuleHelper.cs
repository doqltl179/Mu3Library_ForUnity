using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Raycast {
    public class RayCapsuleHelper {
        public Coordinate Coordinate { get; private set; }
        public Direction Direction { get; private set; }
        public Transform Origin { get; private set; }
        public float Radius { get; private set; }
        public float Height { get; private set; }
        public float Distance { get; private set; }
        public int Mask { get; private set; }

        public RaycastHit[] Hits { get; private set; }

        private Vector3 p1, p2;
        private Vector3 m_direction;



        // -1 => All Layers
        public RayCapsuleHelper(
            Coordinate coordinate, 
            Direction direction, 
            Transform origin, 
            float radius, 
            float height, 
            float distance, 
            int mask = -1) {
            Coordinate = coordinate;
            Direction = direction;
            Origin = origin;
            Radius = radius;
            Height = height;
            Distance = distance;
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
            p1 = Origin.position;

            if(Coordinate == Coordinate.Local) {
                p2 = p1 + Origin.up * Height;

                switch(Direction) {
                    case Direction.None:
                    case Direction.F: m_direction = Origin.forward; break;
                    case Direction.B: m_direction = -Origin.forward; break;
                    case Direction.R: m_direction = Origin.right; break;
                    case Direction.L: m_direction = -Origin.right; break;
                }
            }
            else {
                p2 = p1 + Vector3.up * Height;

                switch(Direction) {
                    case Direction.None:
                    case Direction.F: m_direction = Vector3.forward; break;
                    case Direction.B: m_direction = Vector3.back; break;
                    case Direction.R: m_direction = Vector3.right; break;
                    case Direction.L: m_direction = Vector3.left; break;
                }
            }

            Hits = Physics.CapsuleCastAll(p1, p2, Radius, m_direction, Distance, Mask);
            return Hits.Length > 0;
        }
        #endregion
    }
}