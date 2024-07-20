using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class StandardCharacterState_Movement : CharacterState {




        public override void Enter() {

        }

        public override void Exit() {

        }

        public override void Update() {
            character.Move();
        }
    }
}