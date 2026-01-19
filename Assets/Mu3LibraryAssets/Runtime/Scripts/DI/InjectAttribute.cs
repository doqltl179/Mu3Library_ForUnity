using System;

namespace Mu3Library.DI
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public bool Required { get; }
        public string Key { get; }
        public Type CoreType { get; }



        public InjectAttribute(bool required = true, string key = null)
        {
            Required = required;
            Key = key;
            CoreType = null;
        }

        public InjectAttribute(Type coreType, bool required = true, string key = null)
        {
            CoreType = coreType;
            Required = required;
            Key = key;
        }
    }
}
