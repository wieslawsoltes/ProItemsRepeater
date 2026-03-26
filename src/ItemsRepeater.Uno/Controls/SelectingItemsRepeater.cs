using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Avalonia.Controls;

public class SelectingItemsRepeater : ItemsRepeater
{
    public static readonly DependencyProperty AutoScrollToSelectedItemProperty =
        DependencyProperty.Register(
            nameof(AutoScrollToSelectedItem),
            typeof(bool),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(true, OnAutoScrollToSelectedItemChanged));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(-1, OnSelectedIndexChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(null, OnSelectedItemChanged));

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(
            nameof(SelectedValue),
            typeof(object),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(null, OnSelectedValueChanged));

    public static readonly DependencyProperty SelectedValueBindingProperty =
        DependencyProperty.Register(
            nameof(SelectedValueBinding),
            typeof(BindingBase),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(null, OnSelectedValueBindingChanged));

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(IList),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    public static readonly DependencyProperty SelectionProperty =
        DependencyProperty.Register(
            nameof(Selection),
            typeof(ISelectionModel),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(null, OnSelectionChangedProperty));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(
            nameof(SelectionMode),
            typeof(SelectionMode),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(SelectionMode.Single, OnSelectionModeChanged));

    public static readonly DependencyProperty WrapSelectionProperty =
        DependencyProperty.Register(
            nameof(WrapSelection),
            typeof(bool),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected",
            typeof(bool),
            typeof(SelectingItemsRepeater),
            new PropertyMetadata(false));

    private readonly Dictionary<int, UIElement> _realizedElements = new();
    private ISelectionModel _selection;
    private List<object?> _selectionSnapshot = new();
    private bool _updatingFromModel;
    private bool _updatingFromProperty;
    private bool _isUpdatingSelectionSource;
    private bool _isInInit;
    private int _anchorIndex = -1;

    public SelectingItemsRepeater()
    {
        IsTabStop = true;
        _selection = CreateDefaultSelectionModel();
        AttachSelectionModel(_selection);

        ElementPrepared += OnElementPrepared;
        ElementClearing += OnElementClearing;
        ElementIndexChanged += OnElementIndexChanged;
        Loaded += OnLoaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDownHandler), true);
        RegisterPropertyChangedCallback(ItemsSourceProperty, OnItemsSourcePropertyChanged);
    }

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public bool AutoScrollToSelectedItem
    {
        get => (bool)GetValue(AutoScrollToSelectedItemProperty);
        set => SetValue(AutoScrollToSelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public BindingBase? SelectedValueBinding
    {
        get => (BindingBase?)GetValue(SelectedValueBindingProperty);
        set => SetValue(SelectedValueBindingProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public IList? SelectedItems
    {
        get => _selection is RepeaterSelectionModel repeaterSelectionModel
            ? (IList?)GetValue(SelectedItemsProperty) ?? repeaterSelectionModel.WritableSelectedItems
            : null;
        set => SetValue(SelectedItemsProperty, value);
    }

    public ISelectionModel Selection
    {
        get => _selection;
        set => SetValue(SelectionProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public bool WrapSelection
    {
        get => (bool)GetValue(WrapSelectionProperty);
        set => SetValue(WrapSelectionProperty, value);
    }

    public IEnumerable<UIElement> GetRealizedElements() => _realizedElements.Values;

    public static bool GetIsSelected(DependencyObject element) => (bool)element.GetValue(IsSelectedProperty);

    public static void SetIsSelected(DependencyObject element, bool value) => element.SetValue(IsSelectedProperty, value);

    public void BeginInit()
    {
        _isInInit = true;
    }

    public void EndInit()
    {
        _isInInit = false;
        SyncSelectionSource();
    }

    public virtual bool UpdateSelectionFromEvent(UIElement container, RoutedEventArgs eventArgs)
    {
        return eventArgs is PointerRoutedEventArgs pointerArgs && UpdateSelectionFromEvent(container, pointerArgs);
    }

    public bool UpdateSelectionFromEvent(UIElement element, PointerRoutedEventArgs e)
    {
        var index = GetElementIndex(element);
        if (index < 0)
            return false;

        var properties = e.GetCurrentPoint(element).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
            return false;

        var ctrl = IsModifierKeyDown(VirtualKey.Control) || IsModifierKeyDown(VirtualKey.LeftWindows) || IsModifierKeyDown(VirtualKey.RightWindows);
        var shift = IsModifierKeyDown(VirtualKey.Shift);
        ApplySelectionGesture(index, ctrl, shift, properties.IsRightButtonPressed);
        Focus(FocusState.Programmatic);
        return true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncSelectionSource();
        EnsureSelectedItemVisible();
    }

    private void OnItemsSourcePropertyChanged(DependencyObject sender, DependencyProperty dp)
    {
        _ = sender;
        _ = dp;
        SyncSelectionSource();
    }

    private void SyncSelectionSource()
    {
        if (_isUpdatingSelectionSource || _isInInit)
            return;

        _isUpdatingSelectionSource = true;
        try
        {
            _selection.Source = ItemsSource as IEnumerable;
            _selection.SingleSelect = !HasAllFlags(SelectionMode, SelectionMode.Multiple);

            if (_selection is RepeaterSelectionModel repeaterSelectionModel && GetValue(SelectedItemsProperty) is IList explicitSelectedItems)
                repeaterSelectionModel.WritableSelectedItems = explicitSelectedItems;

            if (SelectedItem is not null)
            {
                ApplySelectedItemProperty(SelectedItem);
            }
            else if (SelectedValue is not null && SelectedValueBinding is not null)
            {
                ApplySelectedValueProperty(SelectedValue);
            }
            else if (SelectedIndex >= 0)
            {
                ApplySelectedIndexProperty(SelectedIndex);
            }

            EnsureAlwaysSelected();
            _selectionSnapshot = SnapshotSelectedItems();
            UpdateSelectedValueFromModel();
            UpdateRealizedSelectionStates();
        }
        finally
        {
            _isUpdatingSelectionSource = false;
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
            return;

        var element = FindRealizedElement(e.OriginalSource as DependencyObject);
        if (element is null)
            return;

        UpdateSelectionFromEvent(element, e);
    }

    private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;
        var itemCount = ItemsSourceView?.Count ?? 0;
        if (itemCount == 0)
            return;

        var ctrl = IsModifierKeyDown(VirtualKey.Control) || IsModifierKeyDown(VirtualKey.LeftWindows) || IsModifierKeyDown(VirtualKey.RightWindows);
        var shift = IsModifierKeyDown(VirtualKey.Shift);

        if (ctrl && HasAllFlags(SelectionMode, SelectionMode.Multiple) && e.Key == VirtualKey.A)
        {
            _selection.SelectAll();
            e.Handled = true;
            return;
        }

        var current = SelectedIndex < 0 ? 0 : SelectedIndex;
        var target = current;
        var handled = true;

        switch (e.Key)
        {
            case VirtualKey.Up:
            case VirtualKey.Left:
                target = current - 1;
                break;
            case VirtualKey.Down:
            case VirtualKey.Right:
                target = current + 1;
                break;
            case VirtualKey.Home:
                target = 0;
                break;
            case VirtualKey.End:
                target = itemCount - 1;
                break;
            case VirtualKey.Space:
            case VirtualKey.Enter:
                break;
            default:
                handled = false;
                break;
        }

        if (!handled)
            return;

        if (WrapSelection && itemCount > 0)
        {
            if (target < 0)
                target = itemCount - 1;
            else if (target >= itemCount)
                target = 0;
        }
        else
        {
            target = Math.Clamp(target, 0, itemCount - 1);
        }

        ApplySelectionGesture(target, ctrl, shift);
        e.Handled = true;
    }

    private void ApplySelectionGesture(int index, bool ctrl, bool shift, bool rightButton = false)
    {
        if (index < 0 || index >= (ItemsSourceView?.Count ?? 0))
            return;

        var multiple = HasAllFlags(SelectionMode, SelectionMode.Multiple);
        var toggle = HasAllFlags(SelectionMode, SelectionMode.Toggle);

        if (rightButton)
        {
            if (!_selection.IsSelected(index))
            {
                _selection.SelectedIndex = index;
                _anchorIndex = index;
            }

            return;
        }

        if (!multiple)
        {
            if (toggle && _selection.IsSelected(index))
            {
                ToggleSelection(index);
            }
            else
            {
                _selection.SelectedIndex = index;
            }
            _anchorIndex = index;
            return;
        }

        if (shift && _anchorIndex >= 0)
        {
            SelectRange(_anchorIndex, index, clearExisting: !ctrl && !toggle);
            return;
        }

        if (ctrl || toggle)
        {
            ToggleSelection(index);
            _anchorIndex = index;
            return;
        }

        _selection.SelectedIndex = index;
        _anchorIndex = index;
    }

    private void ToggleSelection(int index)
    {
        if (_selection.IsSelected(index))
        {
            if (HasAllFlags(SelectionMode, SelectionMode.AlwaysSelected) && _selection.Count == 1)
                return;

            _selection.Deselect(index);
        }
        else
        {
            _selection.Select(index);
        }

        EnsureAlwaysSelected();
    }

    private void SelectRange(int start, int end, bool clearExisting)
    {
        using var update = _selection.BatchUpdate();
        if (clearExisting)
            _selection.Clear();

        _selection.SelectRange(Math.Min(start, end), Math.Max(start, end));
        EnsureAlwaysSelected();
    }

    private void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        _realizedElements[args.Index] = args.Element;
        UpdateSelectionState(args.Element, args.Index);
    }

    private void OnElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs args)
    {
        RemoveRealizedElement(args.Element);
        SetIsSelected(args.Element, false);
    }

    private void OnElementIndexChanged(object? sender, ItemsRepeaterElementIndexChangedEventArgs args)
    {
        if (_realizedElements.Remove(args.OldIndex))
            _realizedElements[args.NewIndex] = args.Element;

        UpdateSelectionState(args.Element, args.NewIndex);
    }

    private void OnSelectionModelChanged(object? sender, Avalonia.Controls.Selection.SelectionModelSelectionChangedEventArgs args)
    {
        _ = sender;
        var newSelection = SnapshotSelectedItems();
        var removed = Except(_selectionSnapshot, newSelection);
        var added = Except(newSelection, _selectionSnapshot);
        _selectionSnapshot = newSelection;

        _updatingFromModel = true;
        try
        {
            SetValue(SelectedIndexProperty, _selection.SelectedIndex);
            SetValue(SelectedItemProperty, _selection.SelectedItem);
            SetValue(SelectedItemsProperty, (_selection as RepeaterSelectionModel)?.WritableSelectedItems);
            UpdateSelectedValueFromModel();
        }
        finally
        {
            _updatingFromModel = false;
        }

        UpdateRealizedSelectionStates();
        EnsureSelectedItemVisible();
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(removed, added));
    }

    private void UpdateRealizedSelectionStates()
    {
        foreach (var pair in _realizedElements)
            UpdateSelectionState(pair.Value, pair.Key);
    }

    private void UpdateSelectionState(UIElement element, int index)
    {
        SetIsSelected(element, _selection.IsSelected(index));
    }

    private void EnsureSelectedItemVisible()
    {
        if (!AutoScrollToSelectedItem)
            return;

        var index = _selection.SelectedIndex;
        if (index < 0)
            return;

        var element = GetOrCreateElement(index);
        if (element is FrameworkElement frameworkElement)
            frameworkElement.StartBringIntoView();
    }

    private void RemoveRealizedElement(UIElement element)
    {
        foreach (var pair in _realizedElements)
        {
            if (ReferenceEquals(pair.Value, element))
            {
                _realizedElements.Remove(pair.Key);
                return;
            }
        }
    }

    private UIElement? FindRealizedElement(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is not UIElement element)
                continue;

            if (_realizedElements.ContainsValue(element))
                return element;
        }

        return null;
    }

    private void ApplySelectedIndexProperty(int index)
    {
        if (_updatingFromModel)
            return;

        _updatingFromProperty = true;
        try
        {
            if (index < 0)
            {
                _selection.Clear();
            }
            else
            {
                _selection.SelectedIndex = index;
                _anchorIndex = index;
            }

            EnsureAlwaysSelected();
        }
        finally
        {
            _updatingFromProperty = false;
        }
    }

    private void ApplySelectedItemProperty(object? item)
    {
        if (_updatingFromModel)
            return;

        _updatingFromProperty = true;
        try
        {
            _selection.SelectedItem = item;
            if (_selection.SelectedIndex >= 0)
                _anchorIndex = _selection.SelectedIndex;
            EnsureAlwaysSelected();
        }
        finally
        {
            _updatingFromProperty = false;
        }
    }

    private void ApplySelectedValueProperty(object? value)
    {
        if (_updatingFromModel || SelectedValueBinding is null || ItemsSourceView is null)
            return;

        _updatingFromProperty = true;
        try
        {
            for (var i = 0; i < ItemsSourceView.Count; ++i)
            {
                var candidate = ItemsSourceView.GetAt(i);
                if (Equals(SelectedValueBinding.Evaluate(candidate), value))
                {
                    _selection.SelectedIndex = i;
                    _anchorIndex = i;
                    return;
                }
            }

            _selection.Clear();
            EnsureAlwaysSelected();
        }
        finally
        {
            _updatingFromProperty = false;
        }
    }

    private void UpdateSelectedValueFromModel()
    {
        if (SelectedValueBinding is null)
            return;

        SetValue(SelectedValueProperty, SelectedValueBinding.Evaluate(_selection.SelectedItem));
    }

    private void ApplySelectedItemsProperty(IList? selectedItems)
    {
        if (_updatingFromModel)
            return;

        if (_selection is not RepeaterSelectionModel repeaterSelectionModel)
        {
            if (selectedItems is not null)
                throw new InvalidOperationException("Cannot set both Selection and SelectedItems.");

            return;
        }

        repeaterSelectionModel.WritableSelectedItems = selectedItems ?? new System.Collections.ArrayList();
        EnsureAlwaysSelected();
    }

    private void SetSelectionModel(ISelectionModel? model)
    {
        model ??= CreateDefaultSelectionModel();
        if (ReferenceEquals(_selection, model))
            return;

        var itemsSource = ItemsSource as IEnumerable;
        if (model.Source is not null && itemsSource is not null && !ReferenceEquals(model.Source, itemsSource))
        {
            throw new ArgumentException(
                "The supplied ISelectionModel already has an assigned Source but this collection is different to the ItemsSource on the control.",
                nameof(model));
        }

        DetachSelectionModel(_selection);
        _selection = model;
        AttachSelectionModel(_selection);

        _anchorIndex = _selection.AnchorIndex;

        _updatingFromModel = true;
        try
        {
            SetValue(
                SelectionModeProperty,
                _selection.SingleSelect
                    ? SelectionMode & ~SelectionMode.Multiple
                    : SelectionMode | SelectionMode.Multiple);

            SetValue(
                SelectedItemsProperty,
                (_selection as RepeaterSelectionModel)?.WritableSelectedItems);
        }
        finally
        {
            _updatingFromModel = false;
        }

        SyncSelectionSource();
    }

    private void AttachSelectionModel(ISelectionModel model)
    {
        model.SelectionChanged += OnSelectionModelChanged;
        model.PropertyChanged += OnSelectionModelPropertyChanged;
        model.LostSelection += OnSelectionModelLostSelection;
    }

    private void DetachSelectionModel(ISelectionModel model)
    {
        model.SelectionChanged -= OnSelectionModelChanged;
        model.PropertyChanged -= OnSelectionModelPropertyChanged;
        model.LostSelection -= OnSelectionModelLostSelection;
    }

    private static ISelectionModel CreateDefaultSelectionModel() => new RepeaterSelectionModel();

    private void OnSelectionModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not ISelectionModel selection)
            return;

        if (args.PropertyName == nameof(ISelectionModel.AnchorIndex))
        {
            _anchorIndex = selection.AnchorIndex;
            return;
        }

        if (args.PropertyName == nameof(RepeaterSelectionModel.WritableSelectedItems) &&
            sender is RepeaterSelectionModel repeaterSelectionModel)
        {
            _updatingFromModel = true;
            try
            {
                SetValue(SelectedItemsProperty, repeaterSelectionModel.WritableSelectedItems);
            }
            finally
            {
                _updatingFromModel = false;
            }
        }
    }

    private void OnSelectionModelLostSelection(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        EnsureAlwaysSelected();
    }

    private void EnsureAlwaysSelected()
    {
        if (!HasAllFlags(SelectionMode, SelectionMode.AlwaysSelected))
            return;

        if (_selection.Count == 0 && (ItemsSourceView?.Count ?? 0) > 0)
        {
            _selection.SelectedIndex = 0;
            _anchorIndex = 0;
        }
    }

    private List<object?> SnapshotSelectedItems()
    {
        return _selection.SelectedItems.ToList();
    }

    private static System.Collections.ArrayList Except(List<object?> first, List<object?> second)
    {
        var result = new System.Collections.ArrayList();
        foreach (var item in first)
        {
            if (!second.Contains(item))
                result.Add(item);
        }

        return result;
    }

    private static bool HasAllFlags(SelectionMode mode, SelectionMode flags) => (mode & flags) == flags;

    private static bool IsModifierKeyDown(VirtualKey key)
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static void OnAutoScrollToSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater && (bool)args.NewValue)
            repeater.EnsureSelectedItemVisible();
    }

    private static void OnSelectedIndexChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater && !repeater._updatingFromProperty)
            repeater.ApplySelectedIndexProperty((int)args.NewValue);
    }

    private static void OnSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater && !repeater._updatingFromProperty)
            repeater.ApplySelectedItemProperty(args.NewValue);
    }

    private static void OnSelectedValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater && !repeater._updatingFromProperty)
            repeater.ApplySelectedValueProperty(args.NewValue);
    }

    private static void OnSelectedValueBindingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater)
            repeater.UpdateSelectedValueFromModel();
    }

    private static void OnSelectedItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater && !repeater._updatingFromProperty)
            repeater.ApplySelectedItemsProperty((IList?)args.NewValue);
    }

    private static void OnSelectionChangedProperty(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SelectingItemsRepeater repeater)
            repeater.SetSelectionModel((ISelectionModel?)args.NewValue);
    }

    private static void OnSelectionModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not SelectingItemsRepeater repeater)
            return;

        repeater._selection.SingleSelect = !HasAllFlags((SelectionMode)args.NewValue, SelectionMode.Multiple);
        repeater.EnsureAlwaysSelected();
        repeater.UpdateRealizedSelectionStates();
    }
}
