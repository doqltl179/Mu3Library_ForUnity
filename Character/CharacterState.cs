using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public abstract class CharacterState {
        public CharacterState(CharacterController character) {

        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();

        public virtual void ReEnter() {
            Enter();
        }
    }
}