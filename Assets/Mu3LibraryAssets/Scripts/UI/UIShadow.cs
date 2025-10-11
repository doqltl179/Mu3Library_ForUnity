// Original is "UnityEngine.UI.Shadow"

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Mu3Library.UI
{
    /// <summary>
    /// Adds an outline to a graphic using IVertexModifier.
    /// </summary>
    public class UIShadow : BaseMeshEffect
    {
        [SerializeField] private Color _effectColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Vector2 _effectDirection = Vector2.zero;
        [SerializeField] private float _effectDistance = 0f;
        [SerializeField] private bool _useGraphicAlpha = true;

        [Header("Blur")]
        [SerializeField] private bool _useBlur = false;
        [SerializeField] private bool _drawInnerShadow = false;
        [SerializeField, Range(0, 100)] private int _blurDensity = 50;
        [SerializeField, Range(0, 1000)] private int _blurDistance = 100;
        [SerializeField, Range(0, 8)] private float _blurWeight = 4;


        protected UIShadow()
        { }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if(_effectDirection.sqrMagnitude != 0)
            {
                _effectDirection = _effectDirection.normalized;
            }
        }
#endif

        /// <summary>
        /// Color for the effect
        /// </summary>
        public Color EffectColor
        {
            get { return _effectColor; }
            set
            {
                _effectColor = value;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Should the shadow inherit the alpha from the graphic?
        /// </summary>
        public bool UseGraphicAlpha
        {
            get { return _useGraphicAlpha; }
            set
            {
                _useGraphicAlpha = value;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        protected List<UIVertex> GetShadowZeroAlloc(List<UIVertex> verts, Color32 color, Vector2 direction, float distance)
        {
            List<UIVertex> result = new List<UIVertex>();

            UIVertex vt;

            for (int i = 0; i < verts.Count; i++)
            {
                vt = verts[i];

                Vector3 v = vt.position;
                v += (Vector3)direction * distance;
                vt.position = v;

                Color32 newColor = color;
                if (_useGraphicAlpha)
                {
                    newColor.a = (byte)((newColor.a * vt.color.a) / 255);
                }
                vt.color = newColor;

                result.Add(vt);
            }

            return result;
        }

        protected List<UIVertex> GetSimpleBlurShadowZeroAlloc(List<UIVertex> verts, Color32 color, Vector2 direction, float distance)
        {
            List<UIVertex> result = new List<UIVertex>();

            List<UIVertex> shadowVerts = GetShadowZeroAlloc(verts, color, direction, distance);

            UIVertex[] corners = new UIVertex[4];
            corners[0] = shadowVerts[0]; // LB
            corners[1] = shadowVerts[1]; // LT
            corners[2] = shadowVerts[2]; // RT
            corners[3] = shadowVerts[4]; // RB

            Vector3 centerPos = Vector3.zero;
            for (int i = 0; i < corners.Length; i++)
            {
                centerPos += corners[i].position;
            }
            centerPos /= corners.Length;

            UIVertex vt;

            float alphaOffset = _drawInnerShadow ? 0.667f : 1.0f;

            for (int d = 0; d < _blurDensity; d++)
            {
                float blurStrength = (float)(d + 1) / _blurDensity;
                float scaleDistance = _blurDistance * (1.0f - blurStrength);

                for (int i = 0; i < shadowVerts.Count; i++)
                {
                    vt = shadowVerts[i];

                    Vector3 v = vt.position;
                    Vector3 dir = (v - centerPos).normalized;
                    v += dir * scaleDistance;
                    vt.position = v;

                    Color32 newColor = vt.color;
                    newColor.a = (byte)(newColor.a * Mathf.Pow(blurStrength, _blurWeight) * alphaOffset);
                    vt.color = newColor;

                    result.Add(vt);
                }

                if (_drawInnerShadow)
                {
                    for (int i = 0; i < shadowVerts.Count; i++)
                    {
                        vt = shadowVerts[i];

                        Vector3 v = vt.position;
                        Vector3 dir = (v - centerPos).normalized;
                        v += -dir * (scaleDistance * (blurStrength + 1.0f));
                        vt.position = v;

                        Color32 newColor = vt.color;
                        newColor.a = (byte)(newColor.a * Mathf.Pow(blurStrength, _blurWeight) * alphaOffset);
                        vt.color = newColor;

                        result.Add(vt);
                    }
                }
            }

            return result;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            List<UIVertex> output = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(output);

            List<UIVertex> verts = output.ToList(); // output의 변형 방지

            List<UIVertex> shadow = new List<UIVertex>();
            if (_useBlur)
            {
                shadow = GetSimpleBlurShadowZeroAlloc(verts, _effectColor, _effectDirection, _effectDistance);
            }
            else
            {
                shadow = GetShadowZeroAlloc(verts, _effectColor, _effectDirection, _effectDistance);
            }

            int neededCapacity = verts.Count + shadow.Count;
            if (output.Capacity < neededCapacity)
            {
                output.Capacity = neededCapacity;
            }
            output.InsertRange(0, shadow);

            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
            
            ListPool<UIVertex>.Release(output);
        }
    }
}
