using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public abstract class CharacterState {
        protected CharacterController character;



        public virtual void Init(CharacterController controller) {
            character = controller;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}