[![Build](https://github.com/wieslawsoltes/ProItemsRepeater/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/ProItemsRepeater/actions/workflows/build.yml)

[![NuGet](https://img.shields.io/nuget/v/ProItemsRepeater.svg)](https://www.nuget.org/packages/ProItemsRepeater/)

#  `ProItemsRepeater` - ItemsRepeater control for Avalonia

## Introduction

`ItemsRepeater` control and associated infrastructure ported from the WinUI library: https://github.com/microsoft/microsoft-ui-xaml
Previously this port was part of the Avalonia package itself.
`ProItemsRepeater` is a hard fork of `Avalonia.Controls.ItemsRepeater` that was retired.

## Quick Start

Install the package:

```bash
dotnet add package ProItemsRepeater
```

Or add a package reference:

```xml
<ItemGroup>
  <PackageReference Include="ProItemsRepeater" Version="x.y.z" />
</ItemGroup>
```

The control lives in the default Avalonia XAML namespace and works with Avalonia 11.0+.

## Basic Usage

```xml
<ScrollViewer>
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

Swap in `WrapLayout` or `UniformGridLayout` when you need wrapping or tile layouts.
Virtualizing layouts only realize items inside the viewport plus cache lengths.

## Layouts

ItemsRepeater delegates measure and arrange to the attached layout.
Virtualizing layouts realize only items inside the realization window and reuse elements as you scroll.
Non-virtualizing layouts measure and arrange all items.

| Layout | Type | How it works | Use cases |
| --- | --- | --- | --- |
| `StackLayout` | Virtualizing | Single line along `Orientation` with `Spacing`; virtualizes by viewport. | Long lists, chat/logs, horizontal or vertical lists. |
| `WrapLayout` | Virtualizing | Wraps items into rows or columns based on available space; uses `HorizontalSpacing` and `VerticalSpacing`. | Variable-size tiles, tags, flowing item grids. |
| `UniformGridLayout` | Virtualizing | Uniform cells using min item size and spacing with optional stretch/justification. | Photo grids, dashboards, icon tiles. |
| `NonVirtualizingStackLayout` | Non-virtualizing | Single line layout without virtualization; measures all items. | Small collections, size-to-content, animation scenarios. |

## License

This repository is licensed under the MIT License; see `licence.md`.
Some files originate from the old Avalonia repository and remain under the Avalonia MIT license text; see `LICENSE-AVALONIA`.
