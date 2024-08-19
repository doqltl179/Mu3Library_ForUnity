using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public interface IMove {
        public void Move(Vector2 input, bool isRun = false);
    }
}