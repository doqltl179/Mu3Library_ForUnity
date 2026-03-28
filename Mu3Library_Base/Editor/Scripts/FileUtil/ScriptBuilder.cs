using System.Collections.Generic;
using System.Text;
using UnityEngine;

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

        private static int _appendStackCountMax = 1000;
        public static int AppendStackCountMax
        {
            get => _appendStackCountMax;
            set => _appendStackCountMax = value;
        }

        private static int _appendStackCount = 0;



        public static string Build(int spaces, params CodeBlock[] blocks)
        {
            _appendStackCount = 0;

            StringBuilder sb = new StringBuilder();
            int spaceCount = 0;
            bool previousIsCodeBlock = false;

            foreach (CodeBlock block in blocks)
            {
                AppendCodeBlock(sb, block, spaces, ref spaceCount, ref previousIsCodeBlock);
            }

            if (_appendStackCount >= _appendStackCountMax)
            {
                Debug.LogWarning($"The append count exceeded the maximum value. max: {_appendStackCountMax}");
            }
            else
            {
                Debug.Log($"Append Count: {_appendStackCount}");
            }

            return sb.ToString();
        }

        private static void AppendCodeBlock(StringBuilder sb, CodeBlock block, int spaces, ref int spaceCount, ref bool previousIsCodeBlock)
        {
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
                        AppendCodeBlock(sb, childBlock, spaces, ref spaceCount, ref previousIsCodeBlock);
                        break;

                    default:
                        Debug.LogWarning($"Undefined object type. type: {obj.GetType()}");
                        break;
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
            if (_appendStackCount >= _appendStackCountMax)
            {
                return;
            }

            if (spaces > 0)
            {
                sb.Append(' ', spaces).AppendLine(code);
            }
            else
            {
                sb.AppendLine(code);
            }

            _appendStackCount++;
        }
    }
}