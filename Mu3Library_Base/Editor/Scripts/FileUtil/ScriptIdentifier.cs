using System.Collections.Generic;
using System.Text;

namespace Mu3Library.Editor.FileUtil
{
    /// <summary>
    /// 생성 스크립트에 사용할 C# 식별자를 만든다.
    /// <br/> 익스포터마다 필요한 표기 방식이 다르기 때문에 변환 규칙을 이름으로 구분해 제공한다.
    /// </summary>
    public static class ScriptIdentifier
    {
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while",
        };



        /// <summary>
        /// 밑줄은 유지하고 그 외 구분 문자는 다음 글자를 대문자로 올리며 제거한다.
        /// <br/> C# 키워드와 겹치거나 숫자로 시작하면 앞에 밑줄을 붙인다.
        /// </summary>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "_";

            var builder = new StringBuilder();
            bool capitalizeNext = false;
            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    if (builder.Length == 0 && char.IsDigit(character))
                        builder.Append('_');
                    if (capitalizeNext && char.IsLetter(character))
                        builder.Append(char.ToUpperInvariant(character));
                    else
                        builder.Append(character);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            if (builder.Length == 0)
                builder.Append('_');
            if (CSharpKeywords.Contains(builder.ToString()))
                builder.Insert(0, '_');
            return builder.ToString();
        }

        /// <summary>
        /// 첫 글자까지 대문자로 올린 파스칼 표기를 만든다. 밑줄도 구분 문자로 취급해 제거한다.
        /// <br/> 숫자로 시작하면 앞에 밑줄을 붙인다.
        /// </summary>
        public static string SanitizePascal(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var sb = new StringBuilder();
            bool capitalizeNext = false;
            bool isFirst = true;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (isFirst)
                    {
                        sb.Append(char.ToUpperInvariant(c));
                        isFirst = false;
                    }
                    else if (capitalizeNext && char.IsLetter(c))
                    {
                        sb.Append(char.ToUpperInvariant(c));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            if (sb.Length > 0 && char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        /// <summary>
        /// 원본 표기를 유지하고 사용할 수 없는 문자만 밑줄로 바꾼다.
        /// <br/> 숫자로 시작하면 앞에 밑줄을 붙인다.
        /// </summary>
        public static string SanitizeUnderscore(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            // Identifier must not start with a digit
            if (sb.Length > 0 && char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        /// <summary>
        /// public 멤버 이름을 만든다. 구분 문자는 다음 글자를 대문자로 올리며 제거하고,
        /// <br/> 만들 수 없거나 숫자로 시작하면 "Item"을 앞에 둔다.
        /// </summary>
        public static string ToPublicMember(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return "Item";

            var builder = new StringBuilder();
            bool capitalizeNext = true;
            foreach (char character in identifier)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    capitalizeNext = true;
                    continue;
                }

                if (builder.Length == 0 && char.IsDigit(character))
                    builder.Append("Item");

                if (capitalizeNext && char.IsLetter(character))
                    builder.Append(char.ToUpperInvariant(character));
                else
                    builder.Append(character);
                capitalizeNext = false;
            }

            return builder.Length == 0 ? "Item" : builder.ToString();
        }
    }
}
