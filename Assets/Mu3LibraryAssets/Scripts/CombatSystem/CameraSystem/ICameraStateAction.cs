using UnityEngine;

namespace Mu3Library {
    public interface ICameraStateAction {
        /// <summary>
        /// 상태에 진입했을 때 불리는 함수
        /// </summary>
        public void Enter();
        /// <summary>
        /// 상태에서 빠져나올 때 불리는 함수
        /// </summary>
        public void Exit();
        /// <summary>
        /// "Update"에서 불리는 함수
        /// </summary>
        public void Update();
    }
}
