# Virtualization and Scrolling

ItemsRepeater is a panel, not a scroll viewer. For virtualized scrolling, place it inside a `ScrollViewer` and select a virtualizing layout. When hosted in a scroll viewer, the repeater participates via `ILogicalScrollable` so scroll input is quantized using estimated item sizes.

```xml
<ScrollViewer HorizontalScrollBarVisibility="Auto"
              VerticalScrollBarVisibility="Auto">
  <ItemsRepeater ItemsSource="{Binding Items}">
    <ItemsRepeater.Layout>
      <StackLayout Orientation="Vertical" />
    </ItemsRepeater.Layout>
  </ItemsRepeater>
</ScrollViewer>
```

## Logical scrolling

`ItemsRepeater` implements `ILogicalScrollable`, which allows the scroll viewer to treat it as a logical scroll source. This keeps scroll steps aligned with item sizing and lets the repeater drive its viewport internally. For details, see [Logical Scrolling (ILogicalScrollable)](logical-scrolling.md).

## Realization Window

Virtualizing layouts only realize elements within the *realization window*, which is the viewport plus a cache buffer. This keeps UI creation and measure/arrange costs proportional to what is visible.

## Cache Length

`ItemsRepeater.HorizontalCacheLength` and `ItemsRepeater.VerticalCacheLength` control how far the realization window extends beyond the viewport. These values are multipliers of the visible size. The default is `2.0`, which allocates roughly one viewport of cache on each side and grows gradually as you scroll.

```xml
<ItemsRepeater ItemsSource="{Binding Items}"
               HorizontalCacheLength="1"
               VerticalCacheLength="3" />
```

## Bringing Items into View

To programmatically scroll to an item, you can create an anchor element and then request it be brought into view:

```csharp
var element = repeater.GetOrCreateElement(index);
element.BringIntoView();
```

`GetOrCreateElement` will realize the element if it is not already on screen, then update the realization window on the next layout pass.
