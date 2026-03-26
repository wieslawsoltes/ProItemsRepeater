using Avalonia.Controls.Templates;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public class ElementFactoryGetArgs
    {
        public object? Data { get; set; }
        public UIElement? Parent { get; set; }
        public int Index { get; set; }

        internal static ElementFactoryGetArgs FromNative(Microsoft.UI.Xaml.Controls.ElementFactoryGetArgs args)
        {
            return new ElementFactoryGetArgs
            {
                Data = args.Data,
                Parent = args.Parent,
            };
        }
    }

    public class ElementFactoryRecycleArgs
    {
        public UIElement? Element { get; set; }
        public UIElement? Parent { get; set; }

        internal static ElementFactoryRecycleArgs FromNative(Microsoft.UI.Xaml.Controls.ElementFactoryRecycleArgs args)
        {
            return new ElementFactoryRecycleArgs
            {
                Element = args.Element,
                Parent = args.Parent,
            };
        }
    }

    public interface IElementFactory : IDataTemplate
    {
        UIElement GetElement(ElementFactoryGetArgs args);
        void RecycleElement(ElementFactoryRecycleArgs args);
    }
}
