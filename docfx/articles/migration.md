# Migration

## From Avalonia.Controls.ItemsRepeater

`ProItemsRepeater` is a hard fork of the retired `Avalonia.Controls.ItemsRepeater`. The public surface area and namespaces are preserved, so migration is typically just a package reference change.

1. Add the package:

```xml
<ItemGroup>
  <PackageReference Include="ProItemsRepeater" Version="x.y.z" />
</ItemGroup>
```

2. Remove any older ItemsRepeater package or source copies.
3. Rebuild. XAML namespaces remain `xmlns="https://github.com/avaloniaui"`.

## From ItemsControl-based Lists

If you are moving from `ItemsControl` or `ListBox`:

- Replace the control with `ItemsRepeater` or `SelectingItemsRepeater`.
- Provide an explicit layout (for example, `StackLayout`).
- Move styling to target the item container in your template.

ItemsRepeater does not include built-in selection or item panels, so those must be configured explicitly.
