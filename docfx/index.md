# ProItemsRepeater for Avalonia

**ProItemsRepeater** brings the WinUI `ItemsRepeater` control to Avalonia. It is a hard fork of the retired `Avalonia.Controls.ItemsRepeater` with a focus on virtualization, flexible layouts, and element recycling while staying aligned with Avalonia 11+.

## Getting Started

### Install

```bash
dotnet add package ProItemsRepeater
```

```xml
<PackageReference Include="ProItemsRepeater" Version="..." />
```

### Basic Usage

`ItemsRepeater` is a layout-driven panel. Wrap it in a `ScrollViewer` and select a layout:

```xml
<ScrollViewer HorizontalScrollBarVisibility="Auto"
              VerticalScrollBarVisibility="Auto">
  <ItemsRepeater ItemsSource="{Binding Items}">
    <ItemsRepeater.Layout>
      <StackLayout Orientation="Vertical" Spacing="8" />
    </ItemsRepeater.Layout>
    <ItemsRepeater.ItemTemplate>
      <DataTemplate>
        <Border Padding="8" Background="#1F2937" CornerRadius="6">
          <TextBlock Text="{Binding}" />
        </Border>
      </DataTemplate>
    </ItemsRepeater.ItemTemplate>
  </ItemsRepeater>
</ScrollViewer>
```

### Selection Support

Use `SelectingItemsRepeater` when you need selection, `SelectedItem` binding, and keyboard navigation:

```xml
<SelectingItemsRepeater ItemsSource="{Binding Items}"
                        SelectionMode="Multiple">
  <SelectingItemsRepeater.Layout>
    <UniformGridLayout MinItemWidth="120" MinItemHeight="80" MinRowSpacing="8" MinColumnSpacing="8" />
  </SelectingItemsRepeater.Layout>
</SelectingItemsRepeater>
```

### Sample App

Run the sample gallery to explore layouts, recycling, and selection:

```bash
dotnet run --project samples/Avalonia.Controls.ItemsRepeater.Samples/Avalonia.Controls.ItemsRepeater.Samples.csproj
```

## Core Concepts

- **Layout-first**: the attached layout drives measure/arrange, virtualization, and item realization.
- **Virtualization**: only items within the realization window are created; cache length grows around the viewport.
- **Recycling**: item templates and `RecyclingElementFactory` reuse elements across scrolls and updates.
- **Selection**: `SelectingItemsRepeater` layers on `ISelectionModel`, `SelectedItem`, and selection pseudo-classes.

## Documentation Sections

- **[Articles](articles/intro.md)**: Practical guides and feature-focused walkthroughs.
- **[API Documentation](api/index.md)**: Reference for all public types and members.

## License

ProItemsRepeater is licensed under the [MIT License](https://github.com/wieslawsoltes/ProItemsRepeater/blob/master/licence.md).
