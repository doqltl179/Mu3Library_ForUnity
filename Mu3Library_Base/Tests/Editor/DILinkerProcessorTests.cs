using System.Collections.Generic;
using System.IO;
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

        private sealed class GenericInject<T>
        {
            [Inject(required: false)] public IDependency Field = null;
        }

        private sealed class BackingFieldInject
        {
            [field: Inject(required: false)] public IDependency Property { get; set; }
        }

        private sealed class InjectedComponent : MonoBehaviour
        {
            [Inject(required: false)] public IDependency Field = null;
            [Inject(required: false)] public IDependency Property { get; private set; }
        }

        private sealed class WithoutInject
        {
            public WithoutInject(IDependency dependency) { }
        }



        private static XDocument BuildForTestAssembly()
        {
            return XDocument.Parse(DILinkerProcessor.BuildLinkXml(new string[0], new[] { typeof(DILinkerProcessorTests).Assembly }));
        }

        private static XElement FindType(XDocument document, string nestedName)
        {
            return document.Descendants("type").SingleOrDefault(type => (string)type.Attribute("fullname") == Prefix + nestedName);
        }

        private static string[] Names(XElement type, string element)
        {
            return type.Elements(element).Select(member => (string)member.Attribute("name")).ToArray();
        }

        [Test]
        public void SelectAssemblies_FollowsReferencesOutOfTheAssembliesThatReferenceDI()
        {
            Dictionary<string, IReadOnlyCollection<string>> references = new()
            {
                ["Game"] = new[] { "Mu3Library.DI", "Domain", "UnityEngine.CoreModule" },
                ["Domain"] = new[] { "Shared" },
                ["Shared"] = new[] { "Domain" },
                ["Mu3Library.DI"] = new[] { "Mu3Library.Foundation" },
                ["Mu3Library.Foundation"] = new string[0],
                ["Tool"] = new[] { "Helper" },
                ["Helper"] = new string[0],
            };

            CollectionAssert.AreEqual(
                new[] { "Domain", "Game", "Mu3Library.DI", "Mu3Library.Foundation", "Shared" },
                DILinkerProcessor.SelectAssemblies(references, "Mu3Library.DI").ToArray());
        }

        [Test]
        public void BuildLinkXml_ConstructorAssembly_KeepsTheConstructorsOfEveryClassItKeeps()
        {
            XElement assembly = XDocument.Parse(DILinkerProcessor.BuildLinkXml(new[] { "Domain" }, new System.Reflection.Assembly[0]))
                .Root.Elements("assembly").Single();
            XElement type = assembly.Elements("type").Single();

            Assert.AreEqual("Domain", (string)assembly.Attribute("fullname"));
            Assert.AreEqual("1", (string)assembly.Attribute("ignoreIfMissing"));
            Assert.AreEqual("1", (string)assembly.Attribute("ignoreIfUnreferenced"));
            Assert.AreEqual("*", (string)type.Attribute("fullname"));
            Assert.AreEqual("0", (string)type.Attribute("required"));
            Assert.AreEqual("nothing", (string)type.Attribute("preserve"));
            CollectionAssert.AreEqual(new[] { ".ctor" }, Names(type, "method"));
        }

        [Test]
        public void BuildLinkXml_InjectMembers_AreListedOnTheTypeThatDeclaresThem()
        {
            XDocument document = BuildForTestAssembly();
            XElement leaf = FindType(document, nameof(InjectLeaf));
            XElement baseType = FindType(document, nameof(InjectBase));

            Assert.AreEqual("0", (string)leaf.Attribute("required"));
            CollectionAssert.AreEqual(new[] { "_field" }, Names(leaf, "field"));
            CollectionAssert.AreEqual(new[] { "Property" }, Names(leaf, "property"));
            CollectionAssert.AreEqual(new[] { "_baseField" }, Names(baseType, "field"));
            Assert.IsEmpty(Names(baseType, "method"));
        }

        [Test]
        public void BuildLinkXml_InjectMembersOfAUnityObject_AreListed()
        {
            XElement component = FindType(BuildForTestAssembly(), nameof(InjectedComponent));

            CollectionAssert.AreEqual(new[] { "Field" }, Names(component, "field"));
            CollectionAssert.AreEqual(new[] { "Property" }, Names(component, "property"));
        }

        [Test]
        public void BuildLinkXml_GenericAndNestedTypes_UseTheLinkerNotation()
        {
            Assert.IsNotNull(FindType(BuildForTestAssembly(), "GenericInject`1"));
        }

        [Test]
        public void BuildLinkXml_CompilerGeneratedBackingField_IsEscaped()
        {
            XElement type = FindType(BuildForTestAssembly(), nameof(BackingFieldInject));

            CollectionAssert.AreEqual(new[] { "<Property>k__BackingField" }, Names(type, "field"));
        }

        [Test]
        public void BuildLinkXml_ClassWithoutInjectMembers_IsLeftToTheConstructorWildcard()
        {
            Assert.IsNull(FindType(BuildForTestAssembly(), nameof(WithoutInject)));
        }

        [Test]
        public void BuildLinkXml_AssemblyInBothLists_HasOneElement()
        {
            System.Reflection.Assembly testAssembly = typeof(DILinkerProcessorTests).Assembly;
            XDocument document = XDocument.Parse(DILinkerProcessor.BuildLinkXml(new[] { testAssembly.GetName().Name }, new[] { testAssembly }));

            XElement assembly = document.Root.Elements("assembly").Single();
            Assert.AreEqual("*", (string)assembly.Elements("type").First().Attribute("fullname"));
            Assert.IsNotNull(FindType(document, nameof(InjectLeaf)));
        }

        [Test]
        public void GenerateAdditionalLinkXmlFile_ListsThePlayerAssembliesThatUseDIAndWhatTheyReference()
        {
            string path = new DILinkerProcessor().GenerateAdditionalLinkXmlFile(null, null);
            XDocument document = XDocument.Load(path);
            string[] assemblies = document.Root.Elements("assembly").Select(assembly => (string)assembly.Attribute("fullname")).ToArray();

            Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "Mu3Library", "DI.link.xml")), Path.GetFullPath(path));
            CollectionAssert.Contains(assemblies, "Mu3Library");
            CollectionAssert.Contains(assemblies, "Mu3Library.DI");
            CollectionAssert.Contains(assemblies, "Mu3Library.Foundation");
            CollectionAssert.DoesNotContain(assemblies, "Mu3Library.Editor");
            Assert.IsTrue(document.Descendants("type").Any(type =>
                (string)type.Attribute("fullname") == "Mu3Library.UI.MVP.MVPManager" &&
                Names(type, "field").Contains("_objectInjector")));
        }
    }
}
