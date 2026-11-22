using System;

namespace Mu3Library.Save
{
    /// <summary>
    /// Turns a section's data into bytes and back. <see cref="SaveManager"/> catches what a serializer
    /// throws and reports it as <see cref="SaveFailureReason.SerializationFailed"/>.
    /// </summary>
    public interface ISaveSerializer
    {
        public byte[] Serialize(object data, Type type);
        public object Deserialize(byte[] bytes, Type type);
    }
}
