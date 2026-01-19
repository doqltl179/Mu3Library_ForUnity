using System;

namespace Mu3Library.DI
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public bool Required { get; }
        public string Key { get; }

        public InjectAttribute(bool required = true, string key = null)
        {
            Required = required;
            Key = key;
        }
    }
}
