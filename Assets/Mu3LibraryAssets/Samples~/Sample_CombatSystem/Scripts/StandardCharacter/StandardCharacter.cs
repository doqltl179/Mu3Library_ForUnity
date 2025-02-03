using Mu3Library.CombatSystem;
using System.Collections.Generic;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardCharacter : CharacterController {




        protected override ICharacterStateAction GetDefinedStateAction(CharacterState s) {
            switch(s) {
                case CharacterState.Move: return new StandardMove(this);
                case CharacterState.Jump: return new StandardJump(this);

                default: return null;
            }
        }
    }
}