using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Raycast {
    public class RayCapsuleHelper : RayHelper {
        private Vector3 p1, p2;



        // -1 => All Layers
        public RayCapsuleHelper(
            Coordinate coordinate, 
            Direction direction, 
            Transform origin, 
            float radius, 
            float height, 
            float distance, 
            int mask = -1) {
            m_coordinate = coordinate;
            m_direction = direction;
            m_origin = origin;
            m_radius = radius;
            m_height = height;
            m_distance = distance;
            m_mask = mask;
        }

        protected override bool RaycastFunc() {
            p1 = m_origin.position;
            p2 = p1 + (m_coordinate == Coordinate.Local ? m_origin.up : Vector3.up) * m_height;
            hits = Physics.CapsuleCastAll(p1, p2, m_radius, m_rayDirection, m_distance, m_mask);
            return hits.Length > 0;
        }
    }
}