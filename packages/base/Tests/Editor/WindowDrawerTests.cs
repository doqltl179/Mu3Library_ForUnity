using Mu3Library.Editor.Window.Drawer;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class WindowDrawerTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Game")]
        [TestCase(" My.Game.UI ")]
        public void GetNamespaceWarning_EmptyOrValidNamespace_AllowsGeneration(string scriptNamespace)
        {
            Assert.That(Mu3WindowDrawer.GetNamespaceWarning(scriptNamespace), Is.Null);
        }

        [TestCase("My Game")]
        [TestCase("Game.")]
        [TestCase("1st")]
        [TestCase("class")]
        public void GetNamespaceWarning_InvalidNamespace_NamesIt(string scriptNamespace)
        {
            StringAssert.Contains($"\"{scriptNamespace}\"", Mu3WindowDrawer.GetNamespaceWarning($" {scriptNamespace} "));
        }
    }
}
