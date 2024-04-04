using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public abstract class CharacterState {
        protected CharacterController controller;

        public CharacterState(CharacterController character) {
            controller = character;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}