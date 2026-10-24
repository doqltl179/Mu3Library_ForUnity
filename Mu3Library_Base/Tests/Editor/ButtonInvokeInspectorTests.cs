using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Attribute;
using Mu3Library.Editor.Attribute;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    /// <summary>A value whose only property drawer implements CreatePropertyGUI.</summary>
    [Serializable]
    public class UIToolkitOnlyValue
    {
        public int Number;
    }

    /// <summary>A property drawer with no IMGUI implementation; the IMGUI inspector shows "No GUI Implemented" for it.</summary>
    [CustomPropertyDrawer(typeof(UIToolkitOnlyValue))]
    public class UIToolkitOnlyValueDrawer : PropertyDrawer
    {
        public const string ElementName = "UIToolkitOnlyValue-Drawer";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new Label("UI Toolkit only") { name = ElementName };
        }
    }

    public class ButtonInvokeInspectorTests
    {
        private const string DrawerElementName = UIToolkitOnlyValueDrawer.ElementName;
        private const int MaxFrames = 200;

        // Components declared inside the test class have no MonoScript, so Unity lets AddComponent attach
        // them although this assembly sits in an Editor folder. Neither has a custom editor.
        private sealed class ButtonInvokeTestComponent : MonoBehaviour
        {
            [SerializeField] private UIToolkitOnlyValue _value = new UIToolkitOnlyValue();
            [SerializeField] private int _count;

            public int Count => _count;

            [ButtonInvoke("Increment", ButtonHeight = 30f)]
            private void Increment()
            {
                _count++;
            }

            [ButtonInvoke]
            private int InvalidReturn()
            {
                return _value.Number;
            }
        }

        private sealed class InspectorPlainTestComponent : MonoBehaviour
        {
            [SerializeField] private int _number;

            public int Number => _number;
        }

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        [Test]
        public void MonoBehaviourFallbackEditor_BuildsUIToolkitDefaultInspectorAndButtons()
        {
            ButtonInvokeTestComponent first = CreateComponent();
            ButtonInvokeTestComponent second = CreateComponent();
            UnityEditor.Editor editor = CreateEditor(first, second);

            Assert.That(editor, Is.InstanceOf<ButtonInvokeMonoBehaviourEditor>());
            Assert.That(editor.serializedObject.isEditingMultipleObjects, Is.True);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(root, Is.Not.Null, "The fallback editor must build a UI Toolkit inspector.");
            Assert.That(
                root.Query<PropertyField>().ToList().Select(field => field.bindingPath),
                Is.EqualTo(new[] { "m_Script", "_value", "_count" }));

            VisualElement section = root.Q(ButtonInvokeAttributeInspector.ButtonSectionName);
            Assert.That(section, Is.Not.Null);
            Button button = section.Query<Button>().ToList().Single();
            Assert.That(button.text, Is.EqualTo("Increment"));
            Assert.That(button.style.height.value.value, Is.EqualTo(30f));
            Assert.That(section.Query<HelpBox>().ToList(), Has.Count.EqualTo(1), "The method returning int gets an error box.");
        }

        [Test]
        public void ScriptableObjectFallbackEditor_BuildsUIToolkitDefaultInspectorAndButtons()
        {
            ButtonInvokeTestAsset asset = ScriptableObject.CreateInstance<ButtonInvokeTestAsset>();
            _created.Add(asset);
            UnityEditor.Editor editor = CreateEditor(asset);

            Assert.That(editor, Is.InstanceOf<ButtonInvokeScriptableObjectEditor>());

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(root, Is.Not.Null);
            Assert.That(root.Query<PropertyField>().ToList().Select(field => field.bindingPath), Does.Contain("_value"));
            Assert.That(root.Q(ButtonInvokeAttributeInspector.ButtonSectionName).Q<Button>().text, Is.EqualTo("Invoke Increment"));
        }

        [Test]
        public void FallbackEditor_WithoutButtonInvokeMethods_AddsNoButtonSection()
        {
            GameObject gameObject = new GameObject(nameof(ButtonInvokeInspectorTests));
            _created.Add(gameObject);
            UnityEditor.Editor editor = CreateEditor(gameObject.AddComponent<InspectorPlainTestComponent>());

            Assert.That(editor, Is.InstanceOf<ButtonInvokeMonoBehaviourEditor>());

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(root, Is.Not.Null);
            Assert.That(root.Q(ButtonInvokeAttributeInspector.ButtonSectionName), Is.Null);
        }

        [UnityTest]
        public IEnumerator FallbackEditor_InAWindow_DrawsCreatePropertyGUIDrawerAndInvokesEveryTarget()
        {
            ButtonInvokeTestComponent first = CreateComponent();
            ButtonInvokeTestComponent second = CreateComponent();
            UnityEditor.Editor editor = CreateEditor(first, second);

            InspectorHostWindow window = EditorWindow.GetWindow<InspectorHostWindow>();
            _created.Add(window);

            VisualElement root = editor.CreateInspectorGUI();
            window.rootVisualElement.Add(root);
            root.Bind(editor.serializedObject);

            for (int frame = 0; frame < MaxFrames && root.Q(DrawerElementName) == null; frame++)
            {
                window.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            Assert.That(root.panel, Is.Not.Null, "The inspector needs a panel to bind.");
            Assert.That(root.Q(DrawerElementName), Is.Not.Null, "The CreatePropertyGUI drawer was not drawn.");

            Button button = root.Q(ButtonInvokeAttributeInspector.ButtonSectionName).Q<Button>();
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }

            for (int frame = 0; frame < MaxFrames && (first.Count == 0 || second.Count == 0); frame++)
            {
                yield return null;
            }

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.EqualTo(1));

            window.Close();
        }

        private ButtonInvokeTestComponent CreateComponent()
        {
            GameObject gameObject = new GameObject(nameof(ButtonInvokeInspectorTests));
            _created.Add(gameObject);
            return gameObject.AddComponent<ButtonInvokeTestComponent>();
        }

        private UnityEditor.Editor CreateEditor(params Object[] targets)
        {
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(targets);
            _created.Add(editor);
            return editor;
        }
    }
}
