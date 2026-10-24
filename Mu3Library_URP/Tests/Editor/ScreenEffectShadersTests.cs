using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mu3Library.URP.ScreenEffect;
using Mu3Library.URP.ScreenEffect.Effects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.URP.Tests
{
    public class ScreenEffectShadersTests
    {
        private const string PackagePath = "Packages/com.github.doqltl179.mu3library.urp/";

        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // A pass a project derives for a shader the package does not ship.
        private sealed class NamedShaderPass : ScreenPassBase
        {
            private readonly string _namedShaderPath;
            protected override string _shaderPath => _namedShaderPath;

            public Material Material => _material;



            public NamedShaderPass(string shaderPath)
            {
                _namedShaderPath = shaderPath;
            }
        }

        // Every pass the package ships; each one needs its shader in the asset to work in a player.
        private static IEnumerable<Type> PackagePassTypes => typeof(ScreenPassBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ScreenPassBase)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);



        [Test]
        public void Load_ReturnsTheAssetFromAResourcesFolderOfThePackage()
        {
            ScreenEffectShaders shaders = LoadShaders();

            // An Editor/Resources folder would load here as well but stays out of players.
            Assert.AreEqual(PackagePath + "Runtime/Resources/" + ScreenEffectShaders.ResourcePath + ".asset", AssetDatabase.GetAssetPath(shaders));
        }

        [Test]
        public void Shaders_AreExactlyThePackagePassShaders()
        {
            ScreenEffectShaders shaders = LoadShaders();

            foreach (Shader shader in shaders.Shaders)
            {
                Assert.IsNotNull(shader, "The asset holds an empty shader slot.");
                StringAssert.StartsWith(PackagePath + "Runtime/Shaders/", AssetDatabase.GetAssetPath(shader));
            }

            string[] expected = PackagePassTypes.Select(GetShaderPath).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] actual = shaders.Shaders.Select(shader => shader.name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestCaseSource(nameof(PackagePassTypes))]
        public void Find_PackagePassShader_ReturnsTheReferencedShader(Type passType)
        {
            string shaderPath = GetShaderPath(passType);
            Shader referenced = LoadShaders().Get(shaderPath);

            Assert.IsNotNull(referenced, $"The asset does not reference '{shaderPath}'.");
            Assert.AreSame(referenced, ScreenEffectShaders.Find(shaderPath));
        }

        [TestCaseSource(nameof(PackagePassTypes))]
        public void Material_PackagePass_IsCreatedFromTheReferencedShader(Type passType)
        {
            ScreenPassBase pass = (ScreenPassBase)Activator.CreateInstance(passType);

            try
            {
                Material material = (Material)passType.GetProperty("_material", InstanceMembers).GetValue(pass);

                Assert.IsNotNull(material);
                Assert.AreSame(LoadShaders().Get(GetShaderPath(passType)), material.shader);
            }
            finally
            {
                pass.Dispose();
            }
        }

        [Test]
        public void Material_ShaderOutsideTheAsset_IsCreatedFromShaderFind()
        {
            const string shaderPath = "Universal Render Pipeline/Unlit";
            Shader expected = Shader.Find(shaderPath);
            Assert.IsNotNull(expected, $"The project has no '{shaderPath}' shader to test with.");
            Assert.IsNull(LoadShaders().Get(shaderPath));

            NamedShaderPass pass = new(shaderPath);

            try
            {
                Assert.IsNotNull(pass.Material);
                Assert.AreSame(expected, pass.Material.shader);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                pass.Dispose();
            }
        }

        [Test]
        public void Material_MissingShader_LogsOneErrorAcrossPassesAndAccesses()
        {
            string shaderPath = CreateMissingShaderPath();
            NamedShaderPass first = new(shaderPath);
            NamedShaderPass second = new(shaderPath);

            LogAssert.Expect(LogType.Error, new Regex("^Shader not found\\. path: " + Regex.Escape(shaderPath)));

            for (int i = 0; i < 5; i++)
            {
                Assert.IsNull(first.Material);
                Assert.IsNull(second.Material);
            }

            LogAssert.NoUnexpectedReceived();

            first.Dispose();
            second.Dispose();
        }

        [Test]
        public void Material_TwoMissingShaders_LogOneErrorEach()
        {
            string firstPath = CreateMissingShaderPath();
            string secondPath = CreateMissingShaderPath();
            NamedShaderPass first = new(firstPath);
            NamedShaderPass second = new(secondPath);

            LogAssert.Expect(LogType.Error, new Regex("^Shader not found\\. path: " + Regex.Escape(firstPath)));
            LogAssert.Expect(LogType.Error, new Regex("^Shader not found\\. path: " + Regex.Escape(secondPath)));

            Assert.IsNull(first.Material);
            Assert.IsNull(second.Material);
            Assert.IsNull(first.Material);
            Assert.IsNull(second.Material);

            LogAssert.NoUnexpectedReceived();

            first.Dispose();
            second.Dispose();
        }

        private static ScreenEffectShaders LoadShaders()
        {
            ScreenEffectShaders shaders = ScreenEffectShaders.Load();
            Assert.IsNotNull(shaders, $"Resources.Load found no asset at '{ScreenEffectShaders.ResourcePath}'.");

            return shaders;
        }

        private static string GetShaderPath(Type passType)
        {
            ScreenPassBase pass = (ScreenPassBase)Activator.CreateInstance(passType);

            try
            {
                return (string)passType.GetProperty("_shaderPath", InstanceMembers).GetValue(pass);
            }
            finally
            {
                pass.Dispose();
            }
        }

        // The error is reported once per shader name for the whole session, so each test uses names
        // no other test has used.
        private static string CreateMissingShaderPath() => $"Hidden/Mu3Library/Tests/Missing_{Guid.NewGuid():N}";
    }
}
