using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using CharacterController = Mu3Library.Character.CharacterController;

namespace Mu3Library.Demo.Sample_Character {
    public class SampleSceneCharacterController : MonoBehaviour {
        [SerializeField] private CharacterController character;



        private void Awake() {
            Time.fixedDeltaTime = 1.0f / 144;

            KeyCodeInputCollector.Instance.InitCollectKeys();
        }

        private void Start() {
            CameraManager.Instance.SetCameraToMainCamera();
            CameraManager.Instance.StartFollowing(
                character.transform, 
                Vector3.up * character.Height * 2.0f + Vector3.back * character.Height * 3.0f, 
                0.01f);
            CameraManager.Instance.StartLooking(
                character.transform,
                Vector3.up * character.Height * 1.2f, 
                0.04f);

            character.Play();
        }
    }
}