using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Editor.Window.Drawer;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class DIValidatorDrawerTests
    {
        private interface IService { }

        private sealed class RequiredGetOnly
        {
            [Inject] public IService Service { get; }
        }

        private sealed class OptionalGetOnly
        {
            [Inject(required: false)] public IService Service { get; }
        }

        private sealed class RequiredPrivateSet
        {
            [Inject] public IService Service { get; private set; }
        }



        [Test]
        public void UnwritableProperties_RequiredGetOnlyProperty_IsReported()
        {
            List<string> issues = new();

            DIValidatorDrawer.ValidateUnwritableProperties(typeof(RequiredGetOnly), issues);

            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains($"{nameof(RequiredGetOnly)}.{nameof(RequiredGetOnly.Service)}", issues[0]);
            StringAssert.Contains("no setter", issues[0]);
        }

        [Test]
        public void UnwritableProperties_OptionalOrWritableProperty_IsNotReported()
        {
            List<string> issues = new();

            DIValidatorDrawer.ValidateUnwritableProperties(typeof(OptionalGetOnly), issues);
            DIValidatorDrawer.ValidateUnwritableProperties(typeof(RequiredPrivateSet), issues);

            CollectionAssert.IsEmpty(issues);
        }
    }
}
