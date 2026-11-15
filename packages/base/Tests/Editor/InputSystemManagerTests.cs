#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using System.Text.RegularExpressions;
using Mu3Library.IS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    public class InputSystemManagerTests
    {
        private const string AssetId = "Test";

        private InputSystemManager _manager;
        private InputActionAsset _asset;
        private InputAction _jump;
        private InputAction _move;

        [SetUp]
        public void SetUp()
        {
            _manager = new InputSystemManager();

            // An asset from outside the manager, so Dispose never destroys it and the test owns it.
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = _asset.AddActionMap("Player");
            _jump = map.AddAction("Jump", binding: "<Keyboard>/space");
            _move = map.AddAction("Move");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();

            _asset.Disable();
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void StartInteractiveRebind_CanceledOnADisabledAsset_LeavesTheActionDisabled()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: false);

            InputActionRebindingExtensions.RebindingOperation rebind = _manager.StartInteractiveRebind(_jump, 0);
            Assert.IsNotNull(rebind);
            rebind.Cancel();

            Assert.IsFalse(_jump.enabled, "A rebind gives the action back the state it had.");
        }

        [Test]
        public void StartInteractiveRebind_CompletedOnADisabledAsset_LeavesTheActionDisabled()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: false);
            bool completed = false;

            InputActionRebindingExtensions.RebindingOperation rebind = _manager.StartInteractiveRebind(_jump, 0, onComplete: () => completed = true);
            Assert.IsNotNull(rebind);
            rebind.Complete();

            Assert.IsTrue(completed);
            Assert.IsFalse(_jump.enabled, "A rebind gives the action back the state it had.");
        }

        [Test]
        public void StartInteractiveRebind_EndedOnAnEnabledAsset_EnablesTheActionAgain()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: true);

            InputActionRebindingExtensions.RebindingOperation rebind = _manager.StartInteractiveRebind(_jump, 0);
            Assert.IsNotNull(rebind);
            Assert.IsFalse(_jump.enabled, "The action is off while it is rebound.");
            rebind.Cancel();

            Assert.IsTrue(_jump.enabled);
        }

        [Test]
        public void StartInteractiveRebind_CompositeHeadIndex_IsRejectedWithoutTouchingTheAction()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: true);
            bool started = false;
            _manager.OnRebindStarted += (_, _) => started = true;

            LogAssert.Expect(LogType.Error, new Regex("composite"));
            InputActionRebindingExtensions.RebindingOperation rebind = _manager.StartInteractiveRebind(_move, 0);

            Assert.IsNull(rebind);
            Assert.IsFalse(started);
            Assert.IsTrue(_move.enabled, "A rejected rebind leaves the action as it was.");
        }

        [Test]
        public void StartInteractiveRebind_CompositePartIndex_StartsTheRebind()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: true);

            InputActionRebindingExtensions.RebindingOperation rebind = _manager.StartInteractiveRebind(_move, 1);
            Assert.IsNotNull(rebind);
            rebind.Cancel();

            Assert.IsTrue(_move.enabled);
        }

        [Test]
        public void Dispose_DisablesAnAssetFromOutsideAndKeepsIt()
        {
            _manager.AddInputActionAsset(_asset, AssetId, enable: true);
            Assert.IsTrue(_asset.enabled);

            _manager.Dispose();

            Assert.IsFalse(_asset.enabled, "An asset leaving the manager stops firing, as one replaced under its id does.");
            Assert.IsTrue(_asset != null, "An asset from outside belongs to its caller and is not destroyed.");
        }
    }
}
#endif
