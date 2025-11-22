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
                AppendLine(sb, "}", startSpaces);
            }
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