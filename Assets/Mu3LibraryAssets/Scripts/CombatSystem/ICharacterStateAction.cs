using UnityEngine;

namespace Mu3Library.CombatSystem {
    public interface ICharacterStateAction {
        /// <summary>
        /// "Enter" 상태에 진입하기 위한 조건 함수
        /// </summary>
        public bool EnterCheck();
        /// <summary>
        /// 상태에 진입했을 때 불리는 함수
        /// </summary>
        public void Enter();

        /// <summary>
        /// "Exit" 상태에 진입하기 위한 조건 함수
        /// </summary>
        public bool ExitCheck();
        /// <summary>
        /// 상태에서 빠져나올 때 불리는 함수
        /// </summary>
        public void Exit();

        /// <summary>
        /// "FixedUpdate"에서 불리는 함수
        /// </summary>
        public void FixedUpdate();
        /// <summary>
        /// 상태 진입과 관계없이 항상 "Update"에서 불리는 함수 
        /// </summary>
        public void UpdateAlways();
        /// <summary>
        /// "Update"에서 불리는 함수
        /// </summary>
        public void Update();
        /// <summary>
        /// "LateUpdate"에서 불리는 함수
        /// </summary>
        public void LateUpdate();
    }
}
