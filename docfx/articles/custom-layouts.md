# Custom Layouts

ItemsRepeater supports custom layouts by deriving from `VirtualizingLayout` or `NonVirtualizingLayout`.

## Virtualizing Layout Skeleton

A virtualizing layout should only realize elements that intersect the realization window. The skeleton below shows the basic shape:

```csharp
using System;
using Avalonia.Layout;

public class SimpleVerticalLayout : VirtualizingLayout
{
    public SimpleVerticalLayout()
    {
        LayoutId = "SimpleVerticalLayout";
    }

    protected internal override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
    {
        var y = 0.0;
        var width = 0.0;

        // In a real layout, use context.RealizationRect to limit realized items.
        for (var i = 0; i < context.ItemCount; i++)
        {
            var element = context.GetOrCreateElementAt(i);
            element.Measure(availableSize);
            y += element.DesiredSize.Height;
            width = Math.Max(width, element.DesiredSize.Width);
        }

        return new Size(width, y);
    }

    protected internal override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
    {
        var y = 0.0;

        for (var i = 0; i < context.ItemCount; i++)
        {
            var element = context.GetOrCreateElementAt(i);
            var height = element.DesiredSize.Height;
            element.Arrange(new Rect(0, y, finalSize.Width, height));
            y += height;
        }

        return new Size(finalSize.Width, y);
    }
}
```

### Tips for Real Virtualization

- Use `context.RealizationRect` to compute the visible index range.
- Call `GetOrCreateElementAt` only for items within that range.
- Call `context.RecycleElement` for realized elements that fall outside your range.
- Use `ElementRealizationOptions.SuppressAutoRecycle` when you need to keep elements alive across passes.

## Non-Virtualizing Layout Skeleton

If you need size-to-content or want all items realized, derive from `NonVirtualizingLayout`:

```csharp
using System;
using Avalonia.Layout;

public class SimpleNonVirtualLayout : NonVirtualizingLayout
{
    protected internal override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var desired = new Size();
        foreach (var child in context.Children)
        {
            child.Measure(availableSize);
            desired = new Size(
                Math.Max(desired.Width, child.DesiredSize.Width),
                desired.Height + child.DesiredSize.Height);
        }

        return desired;
    }
}
```

## Assigning a Custom Layout

```xml
<ItemsRepeater ItemsSource="{Binding Items}">
  <ItemsRepeater.Layout>
    <local:SimpleVerticalLayout />
  </ItemsRepeater.Layout>
</ItemsRepeater>
```
