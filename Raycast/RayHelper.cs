using RPGCharacterAnims.Actions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Raycast {
    public abstract class RayHelper {
        protected Coordinate m_coordinate;
        protected Direction m_direction;
        protected Transform m_origin;
        protected float m_radius;
        protected float m_height;
        protected float m_distance;
        protected int m_mask;

        protected RaycastHit[] hits;
        protected Vector3 m_rayDirection;



        #region Utility
        public T[] GetComponentsOnRigidbody<T>() where T : UnityEngine.Object {
            return hits.Where(t => t.rigidbody != null)
                .Select(t => t.rigidbody.GetComponent<T>())
                .Where(t => t != null)
                .ToArray();
        }

        public HitPointWithComponent<T>[] GetHitPointWithComponentOnRigidbody<T>() where T : UnityEngine.Object {
            List<HitPointWithComponent<T>> hitList = new List<HitPointWithComponent<T>>();

            for(int i = 0; i < hits.Length; i++) {
                if(hits[i].rigidbody != null && hits[i].rigidbody.GetComponent<T>() != null) {
                    hitList.Add(new HitPointWithComponent<T> {
                        Component = hits[i].rigidbody.GetComponent<T>(),
                        HitPoint = hits[i].point
                    });
                }
            }

            return hitList.ToArray();
        }

        public T[] GetComponentsOnCollider<T>() where T : UnityEngine.Object {
            return hits.Select(t => t.collider.GetComponent<T>())
                .Where(t => t != null)
                .ToArray();
        }

        public HitPointWithComponent<T>[] GetHitPointWithComponentOnCollider<T>() where T : UnityEngine.Object {
            List<HitPointWithComponent<T>> hitList = new List<HitPointWithComponent<T>>();

            for(int i = 0; i < hits.Length; i++) {
                if(hits[i].collider.GetComponent<T>() != null) {
                    hitList.Add(new HitPointWithComponent<T> {
                        Component = hits[i].collider.GetComponent<T>(),
                        HitPoint = hits[i].point
                    });
                }
            }

            return hitList.ToArray();
        }

        public bool HasEqualOnRigidbody<T>(T component) where T : UnityEngine.Object {
            foreach(RaycastHit hit in hits) {
                if(hit.rigidbody != null && hit.rigidbody.GetComponent<T>() == component) {
                    return true;
                }
            }
            return false;
        }

        public bool HasEqualOnCollider<T>(T component) where T : UnityEngine.Object {
            foreach(RaycastHit hit in hits) {
                if(hit.collider.GetComponent<T>() == component) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Raycast
        public bool Raycast() {
            SetRayDirection();

            return RaycastFunc();
        }

        protected abstract bool RaycastFunc();
        #endregion

        private void SetRayDirection() {
            if(m_coordinate == Coordinate.Local) {
                switch(m_direction) {
                    case Direction.None:
                    case Direction.F: m_rayDirection = m_origin.forward; break;
                    case Direction.B: m_rayDirection = -m_origin.forward; break;
                    case Direction.R: m_rayDirection = m_origin.right; break;
                    case Direction.L: m_rayDirection = -m_origin.right; break;
                }
            }
            else {
                switch(m_direction) {
                    case Direction.None:
                    case Direction.F: m_rayDirection = Vector3.forward; break;
                    case Direction.B: m_rayDirection = Vector3.back; break;
                    case Direction.R: m_rayDirection = Vector3.right; break;
                    case Direction.L: m_rayDirection = Vector3.left; break;
                }
            }
        }
    }
}