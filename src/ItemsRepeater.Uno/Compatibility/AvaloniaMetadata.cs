using System;

namespace Avalonia.Metadata
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class InheritDataTypeFromItemsAttribute : Attribute
    {
        public InheritDataTypeFromItemsAttribute(string itemsPropertyName)
        {
            ItemsPropertyName = itemsPropertyName;
        }

        public string ItemsPropertyName { get; }
    }
}
