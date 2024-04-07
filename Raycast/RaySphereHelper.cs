using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Raycast {
    public class RaySphereHelper : RayHelper {



        // -1 => All Layers
        public RaySphereHelper(
            Coordinate coordinate,
            Direction direction,
            Transform origin,
            float radius,
            float distance,
            int mask = -1) {
            m_coordinate = coordinate;
            m_direction = direction;
            m_origin = origin;
            m_radius = radius;
            m_distance = distance;
            m_mask = mask;
        }

        protected override bool RaycastFunc() {
            hits = Physics.SphereCastAll(m_origin.position, m_radius, m_rayDirection, m_distance, m_mask);
            return hits.Length > 0;
        }
    }
}
