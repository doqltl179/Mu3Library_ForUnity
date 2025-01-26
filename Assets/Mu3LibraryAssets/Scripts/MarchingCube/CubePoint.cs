using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace Mu3Library.MarchingCube {
    public class CubePoint {
        /// <summary>
        /// 전체 point의 Index
        /// </summary>
        public Vector3Int ID => id;
        private Vector3Int id = Vector3Int.one * -1;

        public const float HeightMax = 1.0f;
        public const float HeightMin = -1.0f;

        /// <summary>
        /// <br/> 'pointThreshold'를 활용해 Point를 포함시킬지 결정하는 값
        /// <br/> -1.0 ~ 1.0
        /// </summary>
        public float Height => height;
        private float height = HeightMin;



        public CubePoint(Vector3Int id, float height = HeightMin) {
            this.id = id;
            this.height = height;
        }

        #region Utility
        public float GetHeightRatio() {
            return (height - HeightMin) / (HeightMax - HeightMin);
        }

        public void AddHeight(float addValue) {
            height = Mathf.Clamp(height + addValue, HeightMin, HeightMax);
        }

        public void SetRandomHeight() {
            height = Random.Range(HeightMin, HeightMax);
        }

        public void SetHeight(float value) {
            height = Mathf.Clamp(value, HeightMin, HeightMax);
        }

        public void HeightToMin() {
            height = HeightMin;
        }

        public void HeightToMax() {
            height = HeightMax;
        }

        /// <summary>
        /// height 반전
        /// </summary>
        public void TurnPointSelected() {
            height *= -1;
        }
        #endregion
    }
}
