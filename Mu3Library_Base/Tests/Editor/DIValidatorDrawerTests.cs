using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Editor.Window.Drawer;
using NUnit.Framework;
using UnityEngine;

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

        private sealed class RequiredReadOnly
        {
            [Inject] private readonly IService _service = null;

            public IService Service => _service;
        }

        private sealed class OptionalReadOnly
        {
            [Inject(required: false)] private readonly IService _service = null;

            public IService Service => _service;
        }

        private sealed class PlainCore : CoreBase { }

        // The container injects a core in Start, so the validator checks the core's own members too.
        private sealed class CoreWithUnfillableMembers : CoreBase
        {
            [Inject] private readonly IService _field = null;

            [Inject] private IService Property => null;

            public IService Field => _field;
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

        [Test]
        public void ReadOnlyFields_RequiredReadOnlyField_IsReported()
        {
            List<string> issues = new();

            DIValidatorDrawer.ValidateReadOnlyFields(typeof(RequiredReadOnly), issues);

            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains($"{nameof(RequiredReadOnly)}._service", issues[0]);
            StringAssert.Contains("readonly", issues[0]);
        }

        [Test]
        public void ReadOnlyFields_OptionalReadOnlyField_IsNotReported()
        {
            List<string> issues = new();

            DIValidatorDrawer.ValidateReadOnlyFields(typeof(OptionalReadOnly), issues);

            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void ValidateCore_RegisteredClassWithRequiredReadOnlyField_IsReported()
        {
            GameObject go = new(nameof(PlainCore));
            try
            {
                PlainCore core = go.AddComponent<PlainCore>();
                ServiceDescriptor[] descriptors =
                {
                    new ServiceDescriptor(typeof(RequiredReadOnly), typeof(RequiredReadOnly), ServiceLifetime.Singleton),
                };
                List<string> issues = new();

                DIValidatorDrawer.ValidateCore(null, core, descriptors, issues);

                Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
                StringAssert.Contains($"{nameof(RequiredReadOnly)}._service", issues[0]);
                StringAssert.Contains("readonly", issues[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ValidateCore_RegisteredInstance_ReportsOnlyMembersTheContainerCannotFill()
        {
            GameObject go = new(nameof(PlainCore));
            try
            {
                PlainCore core = go.AddComponent<PlainCore>();
                ServiceDescriptor[] descriptors =
                {
                    new ServiceDescriptor(typeof(RequiredGetOnly), new RequiredGetOnly()),
                    new ServiceDescriptor(typeof(RequiredReadOnly), new RequiredReadOnly()),
                    // IService is not registered, but the checks that look up a registration
                    // still cover only the classes the container creates.
                    new ServiceDescriptor(typeof(RequiredPrivateSet), new RequiredPrivateSet()),
                };
                List<string> issues = new();

                DIValidatorDrawer.ValidateCore(null, core, descriptors, issues);

                Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
                StringAssert.Contains($"{nameof(RequiredGetOnly)}.{nameof(RequiredGetOnly.Service)}", issues[0]);
                StringAssert.Contains("no setter", issues[0]);
                StringAssert.Contains($"{nameof(RequiredReadOnly)}._service", issues[1]);
                StringAssert.Contains("readonly", issues[1]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ValidateCore_CoreItself_ReportsItsMembersTheContainerCannotFill()
        {
            GameObject go = new(nameof(CoreWithUnfillableMembers));
            try
            {
                CoreWithUnfillableMembers core = go.AddComponent<CoreWithUnfillableMembers>();
                List<string> issues = new();

                DIValidatorDrawer.ValidateCore(null, core, core.GetRegisteredDescriptors(), issues);

                Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
                StringAssert.Contains($"{nameof(CoreWithUnfillableMembers)}._field", issues[0]);
                StringAssert.Contains("readonly", issues[0]);
                StringAssert.Contains($"{nameof(CoreWithUnfillableMembers)}.Property", issues[1]);
                StringAssert.Contains("no setter", issues[1]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
