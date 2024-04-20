using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public abstract class CharacterState {
        public CharacterStateType StateType { get; protected set; }



        public CharacterState(CharacterController character, CharacterStateType stateType) {

        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();

        public virtual void ReEnter() {
            Enter();
        }
    }
}