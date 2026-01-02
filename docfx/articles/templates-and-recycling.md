# Templates and Recycling

ItemsRepeater uses `ItemTemplate` to build item visuals. If the template implements `IElementFactory`, ItemsRepeater uses it directly and can recycle elements efficiently.

## Basic ItemTemplate

```xml
<ItemsRepeater ItemsSource="{Binding Items}">
  <ItemsRepeater.ItemTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding}" />
    </DataTemplate>
  </ItemsRepeater.ItemTemplate>
</ItemsRepeater>
```

## Recycling with RecyclingElementFactory

`RecyclingElementFactory` selects templates by key and stores elements in a `RecyclePool` for reuse.

```xml
<UserControl.Resources>
  <RecyclePool x:Key="RecyclePool" />

  <DataTemplate x:Key="odd">
    <Border Background="#FDE68A" Padding="6">
      <TextBlock Text="{Binding}" />
    </Border>
  </DataTemplate>
  <DataTemplate x:Key="even">
    <Border Background="#FEF3C7" Padding="6">
      <TextBlock Text="{Binding}" />
    </Border>
  </DataTemplate>

  <RecyclingElementFactory x:Key="elementFactory"
                           RecyclePool="{StaticResource RecyclePool}"
                           SelectTemplateKey="OnSelectTemplateKey">
    <RecyclingElementFactory.Templates>
      <StaticResource x:Key="odd" ResourceKey="odd" />
      <StaticResource x:Key="even" ResourceKey="even" />
    </RecyclingElementFactory.Templates>
  </RecyclingElementFactory>
</UserControl.Resources>

<ItemsRepeater ItemsSource="{Binding Items}"
               ItemTemplate="{StaticResource elementFactory}" />
```

```csharp
private void OnSelectTemplateKey(object? sender, SelectTemplateEventArgs e)
{
    if (e.DataContext is ItemModel item)
    {
        e.TemplateKey = item.IsOdd ? "odd" : "even";
    }
}
```

### Notes

- `Templates` is a dictionary of template keys to `IDataTemplate` instances.
- `RecyclePool` can be shared across repeaters to reuse elements across views.
- If you only use a single template, you can skip `SelectTemplateKey` and provide one template entry.
