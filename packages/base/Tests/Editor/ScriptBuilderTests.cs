using System;
using System.Collections.Generic;
using Mu3Library.Editor.FileUtil;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class ScriptBuilderTests
    {
        private const int ClassCount = 300;
        private const int ConstantsPerClass = 5;

        [Test]
        public void Build_LongScript_KeepsEveryLineAndBalancesBraces()
        {
            var classes = new List<object>();
            for (int i = 0; i < ClassCount; i++)
            {
                var constants = new List<object>();
                for (int j = 0; j < ConstantsPerClass; j++)
                {
                    constants.Add($"public const string Value{j} = \"{i}/{j}\";");
                }

                classes.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class Folder{i}",
                    Content = constants
                });
            }

            var values = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                values.Add($"Asset{i}");
            }
            classes.Add(new ScriptBuilder.ArrayBlock { FieldName = "All", Values = values });

            var namespaceBlock = new ScriptBuilder.CodeBlock
            {
                Header = "namespace Generated.Paths",
                Content = new List<object>
                {
                    new ScriptBuilder.CodeBlock { Header = "public static class ResourcesPath", Content = classes }
                }
            };

            string script = ScriptBuilder.Build(4, namespaceBlock);
            string[] lines = script.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2000));
            AssertBracesBalanced(script);
            StringAssert.Contains($"public static class Folder{ClassCount - 1}", script);
            StringAssert.Contains($"public const string Value{ConstantsPerClass - 1} = \"{ClassCount - 1}/{ConstantsPerClass - 1}\";", script);
            StringAssert.Contains("\"Asset199\",", script);
            Assert.That(script.TrimEnd(), Does.EndWith("}"));
        }

        [Test]
        public void TryBuild_NestingAtTheLimit_SucceedsWithTheBuildText()
        {
            ScriptBuilder.CodeBlock block = Nest(ScriptBuilder.MaxNestingDepth);

            bool built = ScriptBuilder.TryBuild(4, out string script, out string error, block);

            Assert.That(built, Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(script, Is.EqualTo(ScriptBuilder.Build(4, block)));
            AssertBracesBalanced(script);
        }

        [Test]
        public void TryBuild_BlockContainingItself_FailsWithoutScript()
        {
            var content = new List<object>();
            var block = new ScriptBuilder.CodeBlock { Header = "public static class Loop", Content = content };
            content.Add(block);

            bool built = ScriptBuilder.TryBuild(4, out string script, out string error, block);

            Assert.That(built, Is.False);
            Assert.That(script, Is.Null);
            StringAssert.Contains(ScriptBuilder.MaxNestingDepth.ToString(), error);
            Assert.Throws<InvalidOperationException>(() => ScriptBuilder.Build(4, block));
        }

        [Test]
        public void TryBuild_NestingDeeperThanTheLimit_Fails()
        {
            bool built = ScriptBuilder.TryBuild(4, out string script, out _, Nest(ScriptBuilder.MaxNestingDepth + 1));

            Assert.That(built, Is.False);
            Assert.That(script, Is.Null);
        }

        [Test]
        public void TryBuild_UnsupportedContent_FailsWithoutScript()
        {
            var block = new ScriptBuilder.CodeBlock
            {
                Header = "public static class Broken",
                Content = new List<object> { "public const int A = 1;", 42 }
            };

            bool built = ScriptBuilder.TryBuild(4, out string script, out string error, block);

            Assert.That(built, Is.False);
            Assert.That(script, Is.Null);
            StringAssert.Contains(typeof(int).ToString(), error);
        }

        private static ScriptBuilder.CodeBlock Nest(int depth)
        {
            var block = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class Level{depth}",
                Content = new List<object> { "public const int Depth = 0;" }
            };

            for (int i = depth - 1; i >= 1; i--)
            {
                block = new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class Level{i}",
                    Content = new List<object> { block }
                };
            }

            return block;
        }

        private static void AssertBracesBalanced(string script)
        {
            int depth = 0;
            for (int i = 0; i < script.Length; i++)
            {
                if (script[i] == '{')
                {
                    depth++;
                }
                else if (script[i] == '}')
                {
                    depth--;
                    Assert.That(depth, Is.GreaterThanOrEqualTo(0), $"Unmatched '}}' at index {i}.");
                }
            }

            Assert.That(depth, Is.EqualTo(0), "Unclosed '{'.");
        }
    }
}
