using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public abstract class ElementFactory : Microsoft.UI.Xaml.Controls.ElementFactory, IElementFactory
    {
        public UIElement Build(object? data)
        {
            return GetElement(new ElementFactoryGetArgs { Data = data });
        }

        public UIElement GetElement(ElementFactoryGetArgs args)
        {
            return GetElementCore(args);
        }

        public virtual bool Match(object? data) => true;

        public void RecycleElement(ElementFactoryRecycleArgs args)
        {
            RecycleElementCore(args);
        }

        protected abstract UIElement GetElementCore(ElementFactoryGetArgs args);
        protected abstract void RecycleElementCore(ElementFactoryRecycleArgs args);

        protected sealed override UIElement GetElementCore(Microsoft.UI.Xaml.Controls.ElementFactoryGetArgs args)
        {
            return GetElementCore(ElementFactoryGetArgs.FromNative(args));
        }

        protected sealed override void RecycleElementCore(Microsoft.UI.Xaml.Controls.ElementFactoryRecycleArgs args)
        {
            RecycleElementCore(ElementFactoryRecycleArgs.FromNative(args));
        }
    }
}
