# Getting Started

## Install the Package

```bash
dotnet add package ProItemsRepeater
```

```xml
<ItemGroup>
  <PackageReference Include="ProItemsRepeater" Version="x.y.z" />
</ItemGroup>
```

`ItemsRepeater` lives in the default Avalonia XAML namespace, so no extra XML namespace is required.

## Basic XAML Usage

`ItemsRepeater` is a layout-driven panel. For scrolling, wrap it in a `ScrollViewer`.

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

## Switching Layouts

Swap layouts to change how items are arranged and virtualized:

```xml
<ItemsRepeater ItemsSource="{Binding Items}">
  <ItemsRepeater.Layout>
    <UniformGridLayout MinItemWidth="120" MinItemHeight="80"
                       MinRowSpacing="8" MinColumnSpacing="8" />
  </ItemsRepeater.Layout>
</ItemsRepeater>
```

## Running the Sample App

The repository includes a sample app with ItemsRepeater and SelectingItemsRepeater pages:

```bash
dotnet run --project samples/Avalonia.Controls.ItemsRepeater.Samples/Avalonia.Controls.ItemsRepeater.Samples.csproj
```
