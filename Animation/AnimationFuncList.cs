using Mu3Library.Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using CharacterController = Mu3Library.Character.CharacterController;

namespace Mu3Library.Animation {
    public class AnimationFuncList {



        public AnimationFuncList(CharacterController character) {
            
        }

        public virtual void Start() { }
        public virtual void End() { }

        public virtual void MiddleStart() { }
        public virtual void MiddleEnd() { }

        public virtual void UndecidedEvent() { }
    }
}