using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window.Drawer;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class MVPHelperTests
    {
        [TestCase("Popup")]
        [TestCase("Foo_1")]
        [TestCase("_popup")]
        [TestCase("팝업")]
        public void IsValidIdentifier_AcceptsIdentifiers(string name)
        {
            Assert.That(ScriptIdentifier.IsValidIdentifier(name), Is.True);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("My Popup")]
        [TestCase("1Popup")]
        [TestCase("class")]
        [TestCase("Pop-up")]
        [TestCase("Popup.View")]
        public void IsValidIdentifier_RejectsOtherNames(string name)
        {
            Assert.That(ScriptIdentifier.IsValidIdentifier(name), Is.False);
        }

        [TestCase("Game")]
        [TestCase("My.Game.UI")]
        [TestCase("_Game.V2")]
        public void IsValidNamespace_AcceptsDottedIdentifiers(string name)
        {
            Assert.That(ScriptIdentifier.IsValidNamespace(name), Is.True);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("A..B")]
        [TestCase(".A")]
        [TestCase("A.")]
        [TestCase("A.class")]
        [TestCase("A. B")]
        [TestCase("My Game")]
        public void IsValidNamespace_RejectsOtherNames(string name)
        {
            Assert.That(ScriptIdentifier.IsValidNamespace(name), Is.False);
        }

        [Test]
        public void GetInputWarning_ExplainsAnInvalidNameOrNamespace()
        {
            StringAssert.Contains("My Popup", MVPHelperDrawer.GetInputWarning("", "My Popup"));
            StringAssert.Contains("My Game", MVPHelperDrawer.GetInputWarning("My Game", "Popup"));
            Assert.That(MVPHelperDrawer.GetInputWarning("", "Popup"), Is.Null, "An empty namespace is valid.");
            Assert.That(MVPHelperDrawer.GetInputWarning("My.Game", "Popup"), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Scripts_WithEmptyNamespace_HaveNoNamespaceBlock(bool applyAnimationView)
        {
            List<(string ClassName, string UsingLine, ScriptBuilder.CodeBlock ClassBlock)> components =
                MVPHelperDrawer.GetComponents("Popup", applyAnimationView);

            Assert.That(
                components.Select(component => component.ClassName),
                Is.EqualTo(new[] { "PopupArguments", "PopupModel", "PopupView", "PopupPresenter" }));

            foreach (var component in components)
            {
                string body = BuildScript("", component);

                Assert.That(body, Does.Not.Contain("namespace"), component.ClassName);
                Assert.That(body, Does.StartWith(component.UsingLine));
                StringAssert.Contains($"public class {component.ClassName} : ", body);
                AssertBracesBalanced(body);
            }
        }

        [Test]
        public void Scripts_WithNamespace_WrapEachClassInIt()
        {
            foreach (var component in MVPHelperDrawer.GetComponents("Popup", false))
            {
                string body = BuildScript("My.Game", component);
                string[] lines = body.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                Assert.That(lines, Does.Contain("namespace My.Game"), component.ClassName);
                Assert.That(lines, Does.Contain($"    public class {component.ClassName} : " + GetBaseTypeName(component.ClassName)));
                AssertBracesBalanced(body);
            }
        }

        [Test]
        public void GetExistingScriptFiles_ListsOnlyTheFilesThatExist()
        {
            string folder = Path.Combine(Path.GetTempPath(), $"{nameof(MVPHelperTests)}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "PopupView.cs"), "// existing");
                File.WriteAllText(Path.Combine(folder, "PopupModel.cs"), "// existing");
                File.WriteAllText(Path.Combine(folder, "Other.cs"), "// unrelated");

                List<string> existing = MVPHelperDrawer.GetExistingScriptFiles(
                    folder,
                    new[] { "PopupArguments", "PopupModel", "PopupView", "PopupPresenter" });

                Assert.That(existing, Is.EqualTo(new[] { "PopupModel.cs", "PopupView.cs" }));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string BuildScript(string scriptNamespace, (string ClassName, string UsingLine, ScriptBuilder.CodeBlock ClassBlock) component)
        {
            bool built = MVPHelperDrawer.TryBuildScript(4, scriptNamespace, component.UsingLine, component.ClassBlock, out string body, out string error);
            Assert.That(built, Is.True, error);
            return body;
        }

        private static string GetBaseTypeName(string className)
        {
            if (className.EndsWith("Arguments")) return "Arguments";
            if (className.EndsWith("Model")) return "Model<PopupArguments>";
            if (className.EndsWith("View")) return "View";
            return "Presenter<PopupView, PopupModel, PopupArguments>";
        }

        private static void AssertBracesBalanced(string script)
        {
            int depth = 0;
            foreach (char character in script)
            {
                if (character == '{') depth++;
                else if (character == '}') depth--;
                Assert.That(depth, Is.GreaterThanOrEqualTo(0));
            }

            Assert.That(depth, Is.EqualTo(0));
        }
    }
}
