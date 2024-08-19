using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class StandardCharacterState_Movement : CharacterState {
        private StandardCharacterController player;

        private Dictionary<KeyCode, KeyData> keyInfos = null;

        private Vector2 inputVec2;



        public override void Init(CharacterController controller, object[] param = null) {
            player = (StandardCharacterController)controller;
        }

        public override void Enter() {
            keyInfos = new Dictionary<KeyCode, KeyData>();
            keyInfos.Add(player.KeyCode_MoveB, new KeyData(player.KeyCode_MoveB));
            keyInfos.Add(player.KeyCode_MoveF, new KeyData(player.KeyCode_MoveF));
            keyInfos.Add(player.KeyCode_MoveL, new KeyData(player.KeyCode_MoveL));
            keyInfos.Add(player.KeyCode_MoveR, new KeyData(player.KeyCode_MoveR));
            keyInfos.Add(player.KeyCode_Run, new KeyData(player.KeyCode_Run));
        }

        public override void Exit() {
            if(keyInfos != null) keyInfos.Clear();
        }

        public override void FixedUpdate() {
            player.Move(inputVec2, keyInfos[player.KeyCode_Run].KeyPressing);
        }

        public override void Update() {
            inputVec2 = Vector3.zero;

            foreach(var key in keyInfos.Keys) {
                keyInfos[key].UpdateKey();
            }
            if(keyInfos[player.KeyCode_MoveB].KeyPressing) inputVec2 += Vector2.down;
            if(keyInfos[player.KeyCode_MoveF].KeyPressing) inputVec2 += Vector2.up;
            if(keyInfos[player.KeyCode_MoveL].KeyPressing) inputVec2 += Vector2.left;
            if(keyInfos[player.KeyCode_MoveR].KeyPressing) inputVec2 += Vector2.right;
        }

        public override void LateUpdate() {

        }
    }
}