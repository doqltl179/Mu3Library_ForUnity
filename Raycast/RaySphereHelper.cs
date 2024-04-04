using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Raycast {
    public class RaySphereHelper {
        public Coordinate Coordinate { get; private set; }
        public Direction Direction { get; private set; }
        public Transform Origin { get; private set; }
        public float Radius { get; private set; }
        public float Distance { get; private set; }
        public int Mask { get; private set; }

        public RaycastHit[] Hits { get; private set; }

        private Vector3 m_direction;



        // -1 => All Layers
        public RaySphereHelper(
            Coordinate coordinate,
            Direction direction,
            Transform origin,
            float radius,
            float distance,
            int mask = -1) {
            Coordinate = coordinate;
            Direction = direction;
            Origin = origin;
            Radius = radius;
            Distance = distance;
            Mask = mask;
        }

        #region Utility
        public T[] GetComponentsOnRigidbody<T>() where T : UnityEngine.Object {
            return Hits.Where(t => t.rigidbody != null)
                .Select(t => t.rigidbody.GetComponent<T>())
                .Where(t => t != null)
                .ToArray();
        }

        public T[] GetComponentsOnCollider<T>() where T : UnityEngine.Object {
            return Hits.Select(t => t.collider.GetComponent<T>())
                .Where(t => t != null)
                .ToArray();
        }

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
            if(Coordinate == Coordinate.Local) {
                switch(Direction) {
                    case Direction.None:
                    case Direction.F: m_direction = Origin.forward; break;
                    case Direction.B: m_direction = -Origin.forward; break;
                    case Direction.R: m_direction = Origin.right; break;
                    case Direction.L: m_direction = -Origin.right; break;
                }
            }
            else {
                switch(Direction) {
                    case Direction.None:
                    case Direction.F: m_direction = Vector3.forward; break;
                    case Direction.B: m_direction = Vector3.back; break;
                    case Direction.R: m_direction = Vector3.right; break;
                    case Direction.L: m_direction = Vector3.left; break;
                }
            }

            Hits = Physics.SphereCastAll(Origin.position, Radius, m_direction, Distance, Mask);
            return Hits.Length > 0;
        }
        #endregion
    }
}
