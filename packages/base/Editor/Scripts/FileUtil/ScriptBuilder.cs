using System;
using System.Collections.Generic;
using System.Text;

namespace Mu3Library.Editor.FileUtil
{
    public static class ScriptBuilder
    {
        public struct CodeBlock
        {
            public string Header;
            public List<object> Content;
            public string Suffix;
        }

        public struct ArrayBlock
        {
            public string FieldName;
            public IList<string> Values;
        }

        /// <summary>
        /// Outputs each line with the current indentation prepended. Use relative
        /// extra spaces (e.g. 4 spaces for continuation args) embedded in each string.
        /// </summary>
        public struct RawBlock
        {
            public List<string> Lines;
        }

        /// <summary>
        /// The deepest <see cref="CodeBlock"/> nesting a build accepts. It only stops a block that
        /// contains itself through a shared Content list from recursing until the stack overflows;
        /// the length of the output is never limited.
        /// </summary>
        public static readonly int MaxNestingDepth = 64;

        [Obsolete("ScriptBuilder no longer limits its output by line count, so this value is ignored. Nesting is limited by MaxNestingDepth.")]
        public static int AppendStackCountMax { get; set; } = 1000;



        /// <summary>
        /// Builds the script text from the blocks.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The blocks cannot be built completely: a block nests deeper than <see cref="MaxNestingDepth"/>,
        /// or a content item is null or of an unsupported type. No partial script is returned.
        /// </exception>
        public static string Build(int spaces, params CodeBlock[] blocks)
        {
            StringBuilder sb = new StringBuilder();
            int spaceCount = 0;
            bool previousIsCodeBlock = false;

            if (blocks != null)
            {
                foreach (CodeBlock block in blocks)
                {
                    AppendCodeBlock(sb, block, spaces, 1, ref spaceCount, ref previousIsCodeBlock);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the script text from the blocks, or returns false with the reason and no script
        /// when they cannot be built completely (see <see cref="Build"/>).
        /// </summary>
        public static bool TryBuild(int spaces, out string script, out string error, params CodeBlock[] blocks)
        {
            try
            {
                script = Build(spaces, blocks);
                error = null;
                return true;
            }
            catch (InvalidOperationException e)
            {
                script = null;
                error = e.Message;
                return false;
            }
        }

        private static void AppendCodeBlock(StringBuilder sb, CodeBlock block, int spaces, int depth, ref int spaceCount, ref bool previousIsCodeBlock)
        {
            if (depth > MaxNestingDepth)
            {
                throw new InvalidOperationException(
                    $"Code blocks nest deeper than {MaxNestingDepth} levels; a block may contain itself. header: {block.Header}");
            }

            bool isHeaderExist = !string.IsNullOrEmpty(block.Header);
            int startSpaces = spaces * spaceCount;

            if (previousIsCodeBlock)
            {
                AppendLine(sb);
            }

            if (isHeaderExist)
            {
                AppendLine(sb, block.Header, startSpaces);
                AppendLine(sb, "{", startSpaces);
                spaceCount++;
                startSpaces = spaces * spaceCount;
            }

            bool childCodeBlockAppended = false;

            if (block.Content != null)
            {
                foreach (object obj in block.Content)
                {
                    switch (obj)
                    {
                        case string line:
                            previousIsCodeBlock = false;
                            AppendLine(sb, line, startSpaces);
                            break;
                        case ArrayBlock arrayBlock:
                            previousIsCodeBlock = false;
                            AppendArrayBlock(sb, arrayBlock, spaces, startSpaces);
                            break;
                        case RawBlock rawBlock:
                            previousIsCodeBlock = false;
                            if (rawBlock.Lines != null)
                                foreach (string rawLine in rawBlock.Lines)
                                    AppendLine(sb, rawLine, startSpaces);
                            break;
                        case CodeBlock childBlock:
                            previousIsCodeBlock = childCodeBlockAppended;
                            childCodeBlockAppended = true;
                            AppendCodeBlock(sb, childBlock, spaces, depth + 1, ref spaceCount, ref previousIsCodeBlock);
                            break;
                        case null:
                            throw new InvalidOperationException($"A content item is null. header: {block.Header}");

                        default:
                            throw new InvalidOperationException($"Unsupported content type: {obj.GetType()}. header: {block.Header}");
                    }
                }
            }

            if (isHeaderExist)
            {
                spaceCount--;
                startSpaces = spaces * spaceCount;
                AppendLine(sb, "}" + (block.Suffix ?? string.Empty), startSpaces);
            }
        }

        private static void AppendArrayBlock(StringBuilder sb, ArrayBlock block, int spaces, int startSpaces)
        {
            AppendLine(sb, $"public static readonly string[] {block.FieldName} = new string[]", startSpaces);
            AppendLine(sb, "{", startSpaces);
            if (block.Values != null)
            {
                foreach (string v in block.Values)
                    AppendLine(sb, $"\"{v}\",", startSpaces + spaces);
            }
            AppendLine(sb, "};", startSpaces);
        }

        private static void AppendLine(StringBuilder sb, string code = "", int spaces = 0)
        {
            if (spaces > 0)
            {
                sb.Append(' ', spaces).AppendLine(code);
            }
            else
            {
                sb.AppendLine(code);
            }
        }
    }
}
