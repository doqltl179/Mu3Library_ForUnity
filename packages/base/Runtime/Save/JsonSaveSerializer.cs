using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Mu3Library.Save
{
    /// <summary>
    /// Writes a section's data as UTF-8 JSON through <see cref="JsonUtility"/>, so Unity types such as
    /// <see cref="Vector3"/>, <see cref="Quaternion"/>, and <see cref="Color"/> save as they are. The
    /// data must be a serializable class or struct: <see cref="JsonUtility"/> writes no primitive,
    /// string, or collection on its own, so this serializer refuses those types; put them in a field.
    /// </summary>
    public class JsonSaveSerializer : ISaveSerializer
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly bool _prettyPrint;



        public JsonSaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        public byte[] Serialize(object data, Type type)
        {
            ThrowIfUnsupported(type);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "The section captured no data.");
            }

            return Utf8.GetBytes(JsonUtility.ToJson(data, _prettyPrint));
        }

        public object Deserialize(byte[] bytes, Type type)
        {
            ThrowIfUnsupported(type);
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return JsonUtility.FromJson(Utf8.GetString(bytes), type);
        }



        private static void ThrowIfUnsupported(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                typeof(IEnumerable).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                throw new NotSupportedException(
                    $"{type.Name} cannot be the data of a section: JsonUtility writes a serializable class or struct only.");
            }
        }
    }
}
