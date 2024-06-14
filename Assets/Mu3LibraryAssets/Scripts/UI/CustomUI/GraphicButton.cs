using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Mu3Library.UI.Custom {
    [RequireComponent(typeof(CanvasRenderer))]
    public class GraphicButton : Graphic, IUIRaycaster {
        public Vector3[] Corners { get; private set; } = new Vector3[4];

        [SerializeField] private Sprite uiImage;

        [Space(20)]
        [SerializeField, Range(2, 32)] private int quality = 8;
        [SerializeField, Range(0.0f, 1.0f)] private float radiusOffset = 1.0f;

        [Space(20)]
        public UnityEvent OnClick;



        protected override void OnPopulateMesh(VertexHelper vh) {
            //base.OnPopulateMesh(vh);
            vh.Clear();

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            rectTransform.GetLocalCorners(Corners);
            Vector3 lb = Corners[0];
            Vector3 lt = Corners[1];
            Vector3 rt = Corners[2];
            Vector3 rb = Corners[3];
            Vector3 center = (lb + rt) * 0.5f;

            Vector4 uv = Vector4.zero;
            if(uiImage != null) {
                DataUtility.GetOuterUV(uiImage);
                s_WhiteTexture = uiImage.texture;
            }
            else {
                s_WhiteTexture = null;
            }

            // Set Verticies
            float radiusX = (rb.x - lb.x) * 0.5f;
            float radiusY = (rt.y - lb.y) * 0.5f;
            float radius = Mathf.Min(radiusX, radiusY);
            float radiusOffsetLength = radius * radiusOffset;

            Vector3 posOffset = rectTransform.rect.size * (rectTransform.pivot - Vector2.one * 0.5f) * -1;
            float angleDegOffset;
            int triIndexOffset;
            for(int i = 0; i < 4; i++) {
                center = Vector3.zero;
                if(radiusX > radiusY) {
                    center.x += radiusX - radiusY;
                    center += new Vector3(1, 1, 0) * (radius - radiusOffsetLength);
                }
                else {
                    center.y += radiusY - radiusX;
                    center += new Vector3(1, 1, 0) * (radius - radiusOffsetLength);
                }

                if(i == 1) {
                    center.x *= -1;
                }
                else if(i == 2) {
                    center.x *= -1;
                    center.y *= -1;
                }
                else if(i == 3) {
                    center.y *= -1;
                }

                vertex.position = center + posOffset;
                vertex.uv0 = new Vector2(center.x / rectTransform.rect.width + 0.5f, center.y / rectTransform.rect.height + 0.5f);
                vh.AddVert(vertex);

                angleDegOffset = 90 * i;
                float angleDeg, angleRad;
                Vector3 dir, point;
                for(int j = 0; j < quality; j++) {
                    angleDeg = 90.0f / (quality - 1) * j + angleDegOffset;
                    angleRad = angleDeg * Mathf.Deg2Rad;
                    dir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                    //point = dir * radius;
                    point = dir * radiusOffsetLength;

                    vertex.position = center + point + posOffset;
                    vertex.uv0 = new Vector2((center.x + point.x) / rectTransform.rect.width + 0.5f, (center.y + point.y) / rectTransform.rect.height + 0.5f);
                    vh.AddVert(vertex);
                }

                triIndexOffset = (quality + 1) * i;
                for(int j = 1; j < quality; j++) {
                    vh.AddTriangle(triIndexOffset, triIndexOffset + j, triIndexOffset + j + 1);
                }
            }

            vh.AddTriangle((quality + 1) * 0, (quality + 1) * 1, (quality + 1) * 2);
            vh.AddTriangle((quality + 1) * 2, (quality + 1) * 3, (quality + 1) * 0);

            vh.AddTriangle((quality + 1) * 0, (quality + 1) * 1 - 1, (quality + 1) * 1 + 1);
            vh.AddTriangle((quality + 1) * 0, (quality + 1) * 1 + 1, (quality + 1) * 1);

            vh.AddTriangle((quality + 1) * 1, (quality + 1) * 2 - 1, (quality + 1) * 2 + 1);
            vh.AddTriangle((quality + 1) * 1, (quality + 1) * 2 + 1, (quality + 1) * 2);

            vh.AddTriangle((quality + 1) * 2, (quality + 1) * 3 - 1, (quality + 1) * 3 + 1);
            vh.AddTriangle((quality + 1) * 2, (quality + 1) * 3 + 1, (quality + 1) * 3);

            vh.AddTriangle((quality + 1) * 3, (quality + 1) * 4 - 1, (quality + 1) * 0 + 1);
            vh.AddTriangle((quality + 1) * 3, (quality + 1) * 0 + 1, (quality + 1) * 0);
        }

        public void OnEnter(PointerEventData point) {
            throw new System.NotImplementedException();
        }

        public void OnExit(PointerEventData point) {
            throw new System.NotImplementedException();
        }

        public void OnMove(PointerEventData point) {
            throw new System.NotImplementedException();
        }
    }
}