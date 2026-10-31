using System.Linq;
using System.Xml.Linq;
using Mu3Library.DI;
using Mu3Library.Editor.DI;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Tests
{
    public class DILinkerProcessorTests
    {
        private const string Prefix = "Mu3Library.Tests.DILinkerProcessorTests/";

        private interface IDependency { }

        private sealed class ConstructorService
        {
            public IDependency Dependency { get; }

            public ConstructorService(IDependency dependency)
            {
                Dependency = dependency;
            }
        }

        private abstract class InjectBase
        {
            [Inject(required: false)] private IDependency _baseField = null;

            public IDependency BaseField => _baseField;
        }

        private sealed class InjectLeaf : InjectBase
        {
            [Inject(required: false)] private IDependency _field = null;
            [Inject(required: false)] public IDependency Property { get; private set; }

            public IDependency Field => _field;
        }

        private sealed class InjectedComponent : MonoBehaviour
        {
            [Inject(required: false)] public IDependency Field = null;
        }



        private static XDocument BuildForTestAssembly()
        {
            return XDocument.Parse(DILinkerProcessor.BuildLinkXml(new[] { typeof(DILinkerProcessorTests).Assembly }));
        }

        private static XElement FindType(XDocument document, string nestedName)
        {
            return document.Descendants("type").SingleOrDefault(type => (string)type.Attribute("fullname") == Prefix + nestedName);
        }

        [Test]
        public void BuildLinkXml_ClassWithPublicConstructor_KeepsConstructorsWhenTheClassIsKept()
        {
            XElement entry = FindType(BuildForTestAssembly(), nameof(ConstructorService));

            Assert.IsNotNull(entry);
            Assert.AreEqual("0", (string)entry.Attribute("required"));
            Assert.AreEqual("nothing", (string)entry.Attribute("preserve"));
            CollectionAssert.AreEqual(new[] { ".ctor" }, entry.Elements("method").Select(method => (string)method.Attribute("name")).ToArray());
        }

        [Test]
        public void BuildLinkXml_InjectMembers_AreListedOnTheTypeThatDeclaresThem()
        {
            XDocument document = BuildForTestAssembly();
            XElement leaf = FindType(document, nameof(InjectLeaf));
            XElement baseType = FindType(document, nameof(InjectBase));

            CollectionAssert.AreEqual(new[] { "_field" }, leaf.Elements("field").Select(field => (string)field.Attribute("name")).ToArray());
            CollectionAssert.AreEqual(new[] { "Property" }, leaf.Elements("property").Select(property => (string)property.Attribute("name")).ToArray());
            CollectionAssert.AreEqual(new[] { "_baseField" }, baseType.Elements("field").Select(field => (string)field.Attribute("name")).ToArray());
            Assert.IsEmpty(baseType.Elements("method"), "An abstract class has no constructor for the container to call.");
        }

        [Test]
        public void BuildLinkXml_UnityObject_IsLeftToTheLinkersOwnRules()
        {
            Assert.IsNull(FindType(BuildForTestAssembly(), nameof(InjectedComponent)));
        }

        [Test]
        public void BuildLinkXml_Assembly_IsSkippedWhenMissingOrUnreferenced()
        {
            XElement assembly = BuildForTestAssembly().Descendants("assembly").Single();

            Assert.AreEqual(typeof(DILinkerProcessorTests).Assembly.GetName().Name, (string)assembly.Attribute("fullname"));
            Assert.AreEqual("1", (string)assembly.Attribute("ignoreIfMissing"));
            Assert.AreEqual("1", (string)assembly.Attribute("ignoreIfUnreferenced"));
        }

        [Test]
        public void BuildLinkXml_AssemblyWithoutDIReference_AddsNothing()
        {
            XDocument document = XDocument.Parse(DILinkerProcessor.BuildLinkXml(new[] { typeof(UnityEngine.Object).Assembly }));

            Assert.IsEmpty(document.Root.Elements());
        }
    }
}
