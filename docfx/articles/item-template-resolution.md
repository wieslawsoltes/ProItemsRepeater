# ItemTemplate Resolution and DataContext

This article describes how `ItemsRepeater` chooses between item data and templates, and how it assigns DataContext. It complements `templates-and-recycling.md` with the control's resolution rules.

## Resolution order

For each item index, `ItemsRepeater` determines a control to realize in this order:

1. If the item is already realized, reuse it.
2. If a bring-into-view anchor exists for the index, reuse it.
3. If a stable reset is pending and a keyed element is available, reuse it.
4. If a pinned element matches the index, reuse it.
5. Otherwise, create or recycle via the element factory (ItemTemplate).

The last step is where template resolution happens.

## Template resolution rules

When creating a new element, the following rules apply:

- If `ItemTemplate` is an `IElementFactory`, it is used directly.
- If `ItemTemplate` is a normal `IDataTemplate`, it is wrapped and used via the element factory shim.
- If `ItemTemplate` is `null`:
  - If the item is a `Control`, the item itself is used as the element.
  - Otherwise, a default template is used to create an element.

This means control items bypass templates unless you explicitly set `ItemTemplate`.

## DataContext assignment

`ItemsRepeater` sets DataContext only when the realized element is not the data item itself:

- If the element is the data item, DataContext is left unchanged.
- If the element is a container created for the data item, DataContext is set to the data item.

When the control sets DataContext, it also tracks that it owns it. On recycle, it clears the DataContext to avoid leaks.

## Practical guidance

- If your items are already controls, you can omit `ItemTemplate` and rely on the control instances directly.
- If you use `ItemTemplate`, be aware that DataContext is always set to the data item (unless the item itself is returned).
- If your template creates containers that should keep their own DataContext, set it explicitly and do not rely on the repeater to preserve it across recycling.

## Related docs

- `templates-and-recycling.md`
- `element-factories.md`
