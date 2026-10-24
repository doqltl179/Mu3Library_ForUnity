using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.UI.MVP;
using Mu3Library.UI.MVP.Animation;
using Mu3Library.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AnimationState = Mu3Library.UI.MVP.Animation.AnimationState;
using Object = UnityEngine.Object;

#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mu3Library.Tests
{
    /// <summary>
    /// Window lifecycles driven in edit mode. Each frame queues a player loop update, which moves every
    /// view coroutine one step, and then runs the manager's Update the way <c>CoreRoot</c> does.
    /// </summary>
    public class MVPManagerTests
    {
        private const int MaxFrames = 60;

        // An animation advances on real time here, about 0.2 ms a frame, so its waits are bounded in
        // seconds instead of frames.
        private const float AnimationTime = 0.05f;
        private const double MaxAnimationSeconds = 2.0;

        // [ExecuteAlways] lets edit mode call OnDisable as play mode does, so a view hidden along with
        // its host gets the same callbacks it gets in a player.
        [ExecuteAlways]
        private sealed class OwnerView : View { }

        [ExecuteAlways]
        private sealed class ChildView : View
        {
            // Stands in for a close transition that outlasts the owner's, such as an AnimationView's.
            public bool HoldClose;

            protected override bool WaitClosingUntil() => !HoldClose;
        }

        [ExecuteAlways]
        private sealed class ToastView : View
        {
            public bool HoldOpen;

            protected override bool WaitOpeningUntil() => !HoldOpen;
        }

        // Overrides OnDisable without base.OnDisable(), which is what ended a close that hiding the
        // view cut short.
        [ExecuteAlways]
        private sealed class NoBaseDisableChildView : View
        {
            public bool HoldClose;
            public int DisableCount;

            protected override bool WaitClosingUntil() => !HoldClose;

            protected override void OnDisable()
            {
                DisableCount++;
            }
        }

        [ExecuteAlways]
        private sealed class FadeView : AnimationView { }

        private sealed class EmptyModel : Model<Arguments> { }

        private sealed class OwnerArgs : Arguments
        {
            public bool OpenChildOnLoad;
        }

        private sealed class OwnerModel : Model<OwnerArgs> { }

        private sealed class OwnerPresenter : Presenter<OwnerView, OwnerModel, OwnerArgs>
        {
            public OwnerView View => _view;
            public ChildPresenter Child { get; private set; }
            public NoBaseDisableChildPresenter NoBaseDisableChild { get; private set; }

            public void OpenChild() => Child = OpenAsChild<ChildPresenter>() as ChildPresenter;

            public void OpenNoBaseDisableChild()
                => NoBaseDisableChild = OpenAsChild<NoBaseDisableChildPresenter>() as NoBaseDisableChildPresenter;

            protected override void LoadFunc()
            {
                if (_args != null && _args.OpenChildOnLoad)
                {
                    OpenChild();
                }
            }
        }

        private sealed class ChildPresenter : Presenter<ChildView, EmptyModel, Arguments>
        {
            public ChildView View => _view;
        }

        private sealed class ToastPresenter : Presenter<ToastView, EmptyModel, Arguments>
        {
            public ToastView View => _view;
        }

        private sealed class NoBaseDisableChildPresenter : Presenter<NoBaseDisableChildView, EmptyModel, Arguments>
        {
            public NoBaseDisableChildView View => _view;
        }

        private sealed class FadePresenter : Presenter<FadeView, EmptyModel, Arguments>
        {
            public FadeView View => _view;
            public AnimationHandler Handler => _view.GetComponent<AnimationHandler>();
        }

        private MVPManager _manager;
        private HashSet<GameObject> _sceneRoots;
        private readonly List<string> _events = new();
        private TestAlphaAnimation _alphaAnimation;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _sceneRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            _events.Clear();
            _timeScale = Time.timeScale;

            _manager = new MVPManager();
            _manager.RegisterViewResource(CreateViewResource<OwnerView>(0));
            // A child left behind in the close list would take the focus with this higher order.
            _manager.RegisterViewResource(CreateViewResource<ChildView>(10));
            _manager.RegisterViewResource(CreateViewResource<ToastView>(0));
            _manager.RegisterViewResource(CreateViewResource<NoBaseDisableChildView>(10));

            _alphaAnimation = ScriptableObject.CreateInstance<TestAlphaAnimation>();
            _manager.RegisterViewResource(CreateFadeViewResource(_alphaAnimation));

            _manager.OnWindowOpened += presenter => _events.Add($"Opened {presenter.GetType().Name}");
            _manager.OnWindowClosed += presenter => _events.Add($"Closed {presenter.GetType().Name}");
            _manager.OnWindowUnloaded += presenter => _events.Add($"Unloaded {presenter.GetType().Name}");
        }

        [TearDown]
        public void TearDown()
        {
            // Everything the test created goes first: Dispose destroys the manager root with
            // Object.Destroy, which edit mode refuses.
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!_sceneRoots.Contains(root))
                {
                    Object.DestroyImmediate(root);
                }
            }

            _manager.Dispose();

            Object.DestroyImmediate(_alphaAnimation);
            TestTimeScale.Restore(_timeScale);
        }

        private static TView CreateViewResource<TView>(int sortingOrder) where TView : View
        {
            GameObject go = new GameObject(typeof(TView).Name);
            go.SetActive(false);

            TView view = go.AddComponent<TView>();
            view.SetSortingOrder(sortingOrder);

            return view;
        }

        private static FadeView CreateFadeViewResource(AnimationConfig animation)
        {
            FadeView view = CreateViewResource<FadeView>(0);

            SerializedObject handler = new SerializedObject(view.GetComponent<AnimationHandler>());
            handler.FindProperty("_animationTime").floatValue = AnimationTime;

            SerializedProperty configs = handler.FindProperty("_animationConfigs");
            configs.arraySize = 1;
            configs.GetArrayElementAtIndex(0).objectReferenceValue = animation;

            handler.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static RectTransform CreateHiddenHost()
        {
            GameObject host = new GameObject("HiddenHost", typeof(RectTransform));
            host.SetActive(false);

            return (RectTransform)host.transform;
        }

        private IEnumerator RunUntil(Func<bool> condition, string description)
        {
            for (int i = 0; i < MaxFrames && !condition(); i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _manager.Update();
            }

            Assert.IsTrue(condition(), $"Waited {MaxFrames} frames until {description}.");
        }

        private IEnumerator RunUntilWithin(Func<bool> condition, string description)
        {
            double start = EditorApplication.timeSinceStartup;
            while (!condition() && EditorApplication.timeSinceStartup - start < MaxAnimationSeconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _manager.Update();
            }

            Assert.IsTrue(condition(), $"Waited {MaxAnimationSeconds} seconds until {description}.");
        }

        private IEnumerator RunFor(double seconds)
        {
            double start = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - start < seconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _manager.Update();
            }
        }

        private IEnumerator OpenOwnerWithOpenedChild(Action<OwnerPresenter> onOpened)
        {
            OwnerPresenter owner = (OwnerPresenter)_manager.Open<OwnerPresenter>();
            yield return RunUntil(() => owner.ViewState == ViewState.Opened, "the owner opened");

            owner.OpenChild();
            yield return RunUntil(() => owner.Child.ViewState == ViewState.Opened, "the child opened");

            Assert.AreSame(owner.View.transform, owner.Child.View.transform.parent, "The child is hosted under its owner.");

            onOpened(owner);
        }

        [UnityTest]
        public IEnumerator Close_OwnerAndChildClosingAlike_KeepTheirEventOrder()
        {
            OwnerPresenter owner = null;
            yield return OpenOwnerWithOpenedChild(opened => owner = opened);

            _events.Clear();
            Assert.IsTrue(_manager.Close(owner));

            yield return RunUntil(() => _events.Count == 4, "both windows unloaded");

            CollectionAssert.AreEqual(
                new[] { "Closed ChildPresenter", "Closed OwnerPresenter", "Unloaded ChildPresenter", "Unloaded OwnerPresenter" },
                _events);
            CollectionAssert.IsEmpty(_manager.GetPresenterDiagnostics());
        }

        [UnityTest]
        public IEnumerator Close_OwnerThatFinishesClosingFirst_UnloadsItsHostedChild()
        {
            OwnerPresenter owner = null;
            yield return OpenOwnerWithOpenedChild(opened => owner = opened);
            ChildPresenter child = owner.Child;

            child.View.HoldClose = true;
            Assert.IsTrue(_manager.Close(owner));

            yield return RunUntil(() => _events.Contains("Unloaded OwnerPresenter"), "the owner unloaded");
            Assert.That(_events, Has.No.Member("Closed ChildPresenter"), "The owner finished closing before its child.");

            yield return RunUntil(() => _events.Contains("Unloaded ChildPresenter"), "the child unloaded");
            Assert.That(_events, Has.Member("Closed ChildPresenter"));
            CollectionAssert.IsEmpty(_manager.GetPresenterDiagnostics());

            // With no child left in the close list, focus goes to the next window, so CloseFocused closes it.
            ToastPresenter toast = (ToastPresenter)_manager.Open<ToastPresenter>();
            yield return RunUntil(() => toast.ViewState == ViewState.Opened, "the next window opened");

            _manager.CloseFocused();
            Assert.AreEqual(ViewState.Closing, toast.ViewState);
        }

        [UnityTest]
        public IEnumerator CloseAll_ForcedOnLoadingOwnerInSameFrame_UnloadsItsHostedChild()
        {
            OwnerPresenter owner = (OwnerPresenter)_manager.Open<OwnerPresenter>(new OwnerArgs { OpenChildOnLoad = true });
            ChildPresenter child = owner.Child;

            Assert.IsNotNull(child, "The owner opened its child from LoadFunc.");
            Assert.AreSame(owner.View.transform, child.View.transform.parent, "The child is hosted under its owner.");
            Assert.IsFalse(owner.View.gameObject.activeInHierarchy, "The owner is still loading.");

            child.View.HoldClose = true;
            _manager.CloseAll(forceClose: true);

            // The owner is shown before its children close, so the child runs its own close transition.
            Assert.AreEqual(ViewState.Closing, child.ViewState);

            yield return RunUntil(
                () => _events.Contains("Unloaded OwnerPresenter") && _events.Contains("Unloaded ChildPresenter"),
                "both windows unloaded");

            CollectionAssert.IsEmpty(_manager.GetPresenterDiagnostics());
        }

        [UnityTest]
        public IEnumerator Close_ForcedOnChildOfLoadingOwner_UnloadsTheChild()
        {
            OwnerPresenter owner = (OwnerPresenter)_manager.Open<OwnerPresenter>(new OwnerArgs { OpenChildOnLoad = true });
            ChildPresenter child = owner.Child;

            // The child's host, the owner's view, stays inactive until the owner finishes loading.
            Assert.IsTrue(_manager.Close(child, forceClose: true));
            Assert.AreEqual(ViewState.Closed, child.ViewState, "A view that cannot run its close transition skips it.");

            yield return RunUntil(() => _events.Contains("Unloaded ChildPresenter"), "the child unloaded");
            yield return RunUntil(() => owner.ViewState == ViewState.Opened, "the owner opened");

            Assert.AreEqual(1, _manager.GetPresenterDiagnostics().Count, "Only the owner is left.");
        }

        [UnityTest]
        public IEnumerator Close_OwnerThatFinishesClosingFirst_UnloadsChildWhoseOnDisableSkipsBase()
        {
            OwnerPresenter owner = (OwnerPresenter)_manager.Open<OwnerPresenter>();
            yield return RunUntil(() => owner.ViewState == ViewState.Opened, "the owner opened");

            owner.OpenNoBaseDisableChild();
            NoBaseDisableChildPresenter child = owner.NoBaseDisableChild;
            yield return RunUntil(() => child.ViewState == ViewState.Opened, "the child opened");

            Assert.AreSame(owner.View.transform, child.View.transform.parent, "The child is hosted under its owner.");

            child.View.HoldClose = true;
            Assert.IsTrue(_manager.Close(owner));

            yield return RunUntil(() => _events.Contains("Unloaded OwnerPresenter"), "the owner unloaded");
            Assert.That(_events, Has.No.Member("Closed NoBaseDisableChildPresenter"), "The owner finished closing before its child.");
            Assert.That(child.View.DisableCount, Is.GreaterThan(0), "Hiding the owner reached the child's own OnDisable.");

            yield return RunUntil(() => _events.Contains("Unloaded NoBaseDisableChildPresenter"), "the child unloaded");
            Assert.That(_events, Has.Member("Closed NoBaseDisableChildPresenter"));
            CollectionAssert.IsEmpty(_manager.GetPresenterDiagnostics());
        }

        [UnityTest]
        public IEnumerator Open_UnderHiddenHost_OpensAtOnceAndClosesWithoutForce()
        {
            ToastPresenter toast = (ToastPresenter)_manager.Open<ToastPresenter>(new HostOptions { Host = CreateHiddenHost() });

            // The view cannot run its open transition under the hidden host, so it skips the wait
            // instead of holding there.
            toast.View.HoldOpen = true;

            yield return RunUntil(() => _events.Contains("Opened ToastPresenter"), "the toast opened");
            Assert.AreEqual(ViewState.Opened, toast.ViewState);
            Assert.IsTrue(toast.Interactable);

            Assert.IsTrue(_manager.Close(toast), "A close that is not forced reaches the opened toast.");
            yield return RunUntil(() => _events.Contains("Unloaded ToastPresenter"), "the toast unloaded");

            CollectionAssert.IsEmpty(_manager.GetPresenterDiagnostics());
        }

        [UnityTest]
        public IEnumerator Open_AnimationViewUnderHiddenHost_TakesItsOpenedPose()
        {
            FadePresenter fade = (FadePresenter)_manager.Open<FadePresenter>(new HostOptions { Host = CreateHiddenHost() });

            // As a pooled view comes back from its last close.
            fade.View.Alpha = 0.0f;

            yield return RunUntil(() => _events.Contains("Opened FadePresenter"), "the view opened");
            Assert.AreEqual(1.0f, fade.View.Alpha);
            Assert.AreEqual(AnimationState.Opened, fade.Handler.State);
        }

        [UnityTest]
        public IEnumerator Open_AnimationViewClosedUnderHiddenHost_AnimatesWhenReopened()
        {
            // Game time stands still, so the reopened view stays where its open animation starts.
            Time.timeScale = 0.0f;

            FadePresenter fade = (FadePresenter)_manager.Open<FadePresenter>(new HostOptions { Host = CreateHiddenHost() });
            fade.Handler.TimeMode = TimeMode.Scaled;

            yield return RunUntil(() => _events.Contains("Opened FadePresenter"), "the view opened");
            Assert.IsTrue(_manager.Close(fade));
            yield return RunUntil(() => _events.Contains("Unloaded FadePresenter"), "the view unloaded");
            Assert.AreEqual(0.0f, fade.View.Alpha, "The view closed under the hidden host took its closed pose.");

            Assert.AreSame(fade, _manager.Open<FadePresenter>(), "The pool reopened the same presenter.");
            yield return RunFor(AnimationTime * 4);

            Assert.AreEqual(ViewState.Opening, fade.ViewState, "The reopened view runs its open animation from the start.");
            Assert.AreEqual(0.0f, fade.View.Alpha);
        }

        [UnityTest]
        public IEnumerator AnimationView_AtTimeScaleZero_OpensAndClosesOnUnscaledTime()
        {
            Time.timeScale = 0.0f;

            FadePresenter fade = (FadePresenter)_manager.Open<FadePresenter>();
            Assert.AreEqual(TimeMode.Unscaled, fade.Handler.TimeMode, "Unscaled time is the default.");

            yield return RunUntilWithin(() => fade.ViewState == ViewState.Opened, "the view opened");
            Assert.AreEqual(1.0f, fade.View.Alpha);

            Assert.IsTrue(_manager.Close(fade), "A close that is not forced reaches the opened view.");
            yield return RunUntilWithin(() => _events.Contains("Unloaded FadePresenter"), "the view unloaded");
            Assert.AreEqual(0.0f, fade.View.Alpha);
        }

        [UnityTest]
        public IEnumerator AnimationView_OnScaledTime_WaitsWhileTimeScaleIsZero()
        {
            Time.timeScale = 0.0f;

            FadePresenter fade = (FadePresenter)_manager.Open<FadePresenter>();
            fade.Handler.TimeMode = TimeMode.Scaled;

            yield return RunFor(AnimationTime * 10);
            Assert.AreEqual(ViewState.Opening, fade.ViewState, "Game time does not move while the time scale is 0.");

            Time.timeScale = 1.0f;
            yield return RunUntilWithin(() => fade.ViewState == ViewState.Opened, "the view opened");
        }

        [Test]
        public void EventSystem_CreatedByTheManager_UsesTheInputModuleOfTheActiveBackend()
        {
            EventSystem eventSystem = _manager.EventSystem;
            Assert.IsFalse(_sceneRoots.Contains(eventSystem.gameObject), "The manager created the EventSystem.");

            BaseInputModule inputModule = eventSystem.GetComponent<BaseInputModule>();
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            Assert.IsInstanceOf<InputSystemUIInputModule>(inputModule);
#else
            // Without the package, or with the package installed under "Input Manager (Old)".
            Assert.IsInstanceOf<StandaloneInputModule>(inputModule);
#endif
        }

#if MU3LIBRARY_UNITASK_SUPPORT
        [UnityTest]
        public IEnumerator OpenAsync_UnderHiddenHost_Completes()
        {
            UniTask<IPresenter> open = _manager.OpenAsync<ToastPresenter>(
                null,
                null,
                OutPanelSettings.Disabled,
                new HostOptions { Host = CreateHiddenHost() });

            yield return RunUntil(() => open.Status != UniTaskStatus.Pending, "OpenAsync completed");

            Assert.AreEqual(UniTaskStatus.Succeeded, open.Status);
            Assert.AreEqual(ViewState.Opened, open.GetAwaiter().GetResult().ViewState);
        }

        [UnityTest]
        public IEnumerator CloseAsync_PresenterReopenedFromUnloadedHandler_CompletesForItsOwnOpening()
        {
            ToastPresenter toast = (ToastPresenter)_manager.Open<ToastPresenter>();
            yield return RunUntil(() => toast.ViewState == ViewState.Opened, "the toast opened");

            // A toast queue opens the next toast as soon as one leaves. The pool hands back the same
            // instance, and the next toast stays on its way in.
            IPresenter reopened = null;
            _manager.OnWindowUnloaded += presenter =>
            {
                if (reopened == null)
                {
                    toast.View.HoldOpen = true;
                    reopened = _manager.Open<ToastPresenter>();
                }
            };

            UniTask<bool> close = _manager.CloseAsync(toast);
            yield return RunUntil(() => close.Status != UniTaskStatus.Pending, "CloseAsync completed");

            Assert.AreEqual(UniTaskStatus.Succeeded, close.Status);
            Assert.IsTrue(close.GetAwaiter().GetResult());
            Assert.AreSame(toast, reopened, "The pool reopened the same presenter instance.");
            Assert.AreEqual(1, _manager.GetPresenterDiagnostics().Count, "The reopened toast is still in the manager.");
        }

        [UnityTest]
        public IEnumerator OpenAsync_PresenterReopenedFromUnloadedHandler_CompletesForItsOwnOpening()
        {
            IPresenter reopened = null;
            _manager.OnWindowUnloaded += presenter =>
            {
                if (reopened == null)
                {
                    ((ToastPresenter)presenter).View.HoldOpen = true;
                    reopened = _manager.Open<ToastPresenter>();
                }
            };

            // Under a hidden host the toast skips its close transition, so one manager update releases
            // it and the handler reopens it before the wait checks again.
            GameObject hiddenHost = new GameObject("HiddenHost", typeof(RectTransform));
            hiddenHost.SetActive(false);

            UniTask<IPresenter> open = _manager.OpenAsync<ToastPresenter>(
                null,
                null,
                OutPanelSettings.Disabled,
                new HostOptions { Host = (RectTransform)hiddenHost.transform });

            _manager.CloseAll(forceClose: true);
            _manager.Update();

            yield return RunUntil(() => open.Status != UniTaskStatus.Pending, "OpenAsync completed");

            Assert.AreEqual(UniTaskStatus.Succeeded, open.Status);
            IPresenter opened = open.GetAwaiter().GetResult();
            Assert.AreSame(opened, reopened, "The pool reopened the same presenter instance.");
            Assert.AreEqual(ViewState.Opening, opened.ViewState, "The reopened toast is still on its way in.");
        }
#endif
    }
}
