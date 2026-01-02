using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
    /// <summary>
    /// An <see cref="ItemsRepeater"/> that maintains a selection.
    /// </summary>
    public class SelectingItemsRepeater : ItemsRepeater
    {
        static SelectingItemsRepeater()
        {
            FocusableProperty.OverrideDefaultValue<SelectingItemsRepeater>(true);
        }

        /// <summary>
        /// Defines the <see cref="AutoScrollToSelectedItem"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoScrollToSelectedItemProperty =
            AvaloniaProperty.Register<SelectingItemsRepeater, bool>(
                nameof(AutoScrollToSelectedItem),
                defaultValue: true);

        /// <summary>
        /// Defines the <see cref="SelectedIndex"/> property.
        /// </summary>
        public static readonly DirectProperty<SelectingItemsRepeater, int> SelectedIndexProperty =
            AvaloniaProperty.RegisterDirect<SelectingItemsRepeater, int>(
                nameof(SelectedIndex),
                o => o.SelectedIndex,
                (o, v) => o.SelectedIndex = v,
                unsetValue: -1,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedItem"/> property.
        /// </summary>
        public static readonly DirectProperty<SelectingItemsRepeater, object?> SelectedItemProperty =
            AvaloniaProperty.RegisterDirect<SelectingItemsRepeater, object?>(
                nameof(SelectedItem),
                o => o.SelectedItem,
                (o, v) => o.SelectedItem = v,
                defaultBindingMode: BindingMode.TwoWay,
                enableDataValidation: true);

        /// <summary>
        /// Defines the <see cref="SelectedValue"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> SelectedValueProperty =
            AvaloniaProperty.Register<SelectingItemsRepeater, object?>(
                nameof(SelectedValue),
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedValueBinding"/> property.
        /// </summary>
        public static readonly StyledProperty<IBinding?> SelectedValueBindingProperty =
            AvaloniaProperty.Register<SelectingItemsRepeater, IBinding?>(nameof(SelectedValueBinding));

        /// <summary>
        /// Defines the <see cref="SelectedItems"/> property.
        /// </summary>
        public static readonly DirectProperty<SelectingItemsRepeater, IList?> SelectedItemsProperty =
            AvaloniaProperty.RegisterDirect<SelectingItemsRepeater, IList?>(
                nameof(SelectedItems),
                o => o.SelectedItems,
                (o, v) => o.SelectedItems = v);

        /// <summary>
        /// Defines the <see cref="Selection"/> property.
        /// </summary>
        public static readonly DirectProperty<SelectingItemsRepeater, ISelectionModel> SelectionProperty =
            AvaloniaProperty.RegisterDirect<SelectingItemsRepeater, ISelectionModel>(
                nameof(Selection),
                o => o.Selection,
                (o, v) => o.Selection = v);

        /// <summary>
        /// Defines the <see cref="SelectionMode"/> property.
        /// </summary>
        public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<SelectingItemsRepeater, SelectionMode>(
                nameof(SelectionMode));

        /// <summary>
        /// Defines the <see cref="WrapSelection"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> WrapSelectionProperty =
            AvaloniaProperty.Register<SelectingItemsRepeater, bool>(
                nameof(WrapSelection));

        /// <summary>
        /// Defines the IsSelected attached property.
        /// </summary>
        public static readonly StyledProperty<bool> IsSelectedProperty =
            SelectingItemsControl.IsSelectedProperty;

        /// <summary>
        /// Defines the <see cref="SelectionChanged"/> event.
        /// </summary>
        public static readonly RoutedEvent<SelectionChangedEventArgs> SelectionChangedEvent =
            RoutedEvent.Register<SelectingItemsRepeater, SelectionChangedEventArgs>(
                nameof(SelectionChanged),
                RoutingStrategies.Bubble);

        private static readonly AttachedProperty<bool> IsSelectedManagedProperty =
            AvaloniaProperty.RegisterAttached<SelectingItemsRepeater, Control, bool>("IsSelectedManaged");

        private readonly HashSet<Control> _selectionSubscriptions = new();
        private ISelectionModel? _selection;
        private int _oldSelectedIndex;
        private WeakReference _oldSelectedItem = new(null);
        private WeakReference<IList?> _oldSelectedItems = new(null);
        private bool _ignoreContainerSelectionChanged;
        private UpdateState? _updateState;
        private bool _hasScrolledToSelectedItem;
        private BindingEvaluator<object?>? _selectedValueBindingEvaluator;
        private bool _isSelectionChangeActive;

        public SelectingItemsRepeater()
        {
            ElementPrepared += OnElementPrepared;
            ElementClearing += OnElementClearing;
            ElementIndexChanged += OnElementIndexChanged;
        }

        /// <summary>
        /// Occurs when the control's selection changes.
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to automatically scroll to newly selected items.
        /// </summary>
        public bool AutoScrollToSelectedItem
        {
            get => GetValue(AutoScrollToSelectedItemProperty);
            set => SetValue(AutoScrollToSelectedItemProperty, value);
        }

        /// <summary>
        /// Gets or sets the index of the selected item.
        /// </summary>
        public int SelectedIndex
        {
            get
            {
                if (_updateState is not null)
                {
                    return _updateState.SelectedIndex.HasValue ?
                        _updateState.SelectedIndex.Value :
                        TryGetExistingSelection()?.SelectedIndex ?? -1;
                }

                return Selection.SelectedIndex;
            }
            set
            {
                if (_updateState is object)
                {
                    _updateState.SelectedIndex = value;
                }
                else
                {
                    Selection.SelectedIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected item.
        /// </summary>
        public object? SelectedItem
        {
            get
            {
                if (_updateState is not null)
                {
                    return _updateState.SelectedItem.HasValue ?
                        _updateState.SelectedItem.Value :
                        TryGetExistingSelection()?.SelectedItem;
                }

                return Selection.SelectedItem;
            }
            set
            {
                if (_updateState is object)
                {
                    _updateState.SelectedItem = value;
                }
                else
                {
                    Selection.SelectedItem = value;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="IBinding"/> instance used to obtain the <see cref="SelectedValue"/> property.
        /// </summary>
        [AssignBinding]
        [InheritDataTypeFromItems(nameof(ItemsSource))]
        public IBinding? SelectedValueBinding
        {
            get => GetValue(SelectedValueBindingProperty);
            set => SetValue(SelectedValueBindingProperty, value);
        }

        /// <summary>
        /// Gets or sets the value of the selected item, obtained using <see cref="SelectedValueBinding"/>.
        /// </summary>
        public object? SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        /// <summary>
        /// Gets or sets the selected items.
        /// </summary>
        /// <remarks>
        /// By default returns a collection that can be modified in order to manipulate the control
        /// selection, however this property will return null if <see cref="Selection"/> is
        /// re-assigned; you should only use either Selection or SelectedItems.
        /// </remarks>
        public IList? SelectedItems
        {
            get
            {
                if (_updateState?.SelectedItems.HasValue == true)
                {
                    return _updateState.SelectedItems.Value;
                }

                if (Selection is RepeaterSelectionModel ism)
                {
                    var result = ism.WritableSelectedItems;
                    _oldSelectedItems.SetTarget(result);
                    return result;
                }

                return null;
            }
            set
            {
                if (_updateState is object)
                {
                    _updateState.SelectedItems = new Optional<IList?>(value);
                }
                else if (Selection is RepeaterSelectionModel i)
                {
                    i.WritableSelectedItems = value ?? new AvaloniaList<object?>();
                }
                else
                {
                    throw new InvalidOperationException("Cannot set both Selection and SelectedItems.");
                }
            }
        }

        /// <summary>
        /// Gets or sets the model that holds the current selection.
        /// </summary>
        public ISelectionModel Selection
        {
            get => _updateState?.Selection.HasValue == true ?
                    _updateState.Selection.Value :
                    GetOrCreateSelectionModel();
            set
            {
                value ??= CreateDefaultSelectionModel();

                if (_updateState is object)
                {
                    _updateState.Selection = new Optional<ISelectionModel>(value);
                }
                else if (_selection != value)
                {
                    var source = ItemsSourceView?.Source;

                    if (value.Source != null && source != null && value.Source != source)
                    {
                        throw new ArgumentException(
                            "The supplied ISelectionModel already has an assigned Source but this " +
                            "collection is different to the ItemsSource on the control.");
                    }

                    var oldSelection = _selection?.SelectedItems.ToArray();
                    DeinitializeSelectionModel(_selection);
                    _selection = value;

                    if (oldSelection?.Length > 0)
                    {
                        RaiseEvent(new SelectionChangedEventArgs(
                            SelectionChangedEvent,
                            oldSelection,
                            Array.Empty<object>()));
                    }

                    InitializeSelectionModel(_selection);

                    var selectedItems = SelectedItems;
                    _oldSelectedItems.TryGetTarget(out var oldSelectedItems);
                    if (oldSelectedItems != selectedItems)
                    {
                        RaisePropertyChanged(SelectedItemsProperty, oldSelectedItems, selectedItems);
                        _oldSelectedItems.SetTarget(selectedItems);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the selection mode.
        /// </summary>
        public SelectionMode SelectionMode
        {
            get => GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether selection wraps around on directional navigation.
        /// </summary>
        public bool WrapSelection
        {
            get => GetValue(WrapSelectionProperty);
            set => SetValue(WrapSelectionProperty, value);
        }

        private bool AlwaysSelected => HasAllFlags(SelectionMode, SelectionMode.AlwaysSelected);

        /// <inheritdoc />
        public override void BeginInit()
        {
            base.BeginInit();
            BeginUpdating();
        }

        /// <inheritdoc />
        public override void EndInit()
        {
            base.EndInit();
            EndUpdating();
        }

        /// <summary>
        /// Gets the value of the <see cref="IsSelectedProperty"/> on the specified control.
        /// </summary>
        public static bool GetIsSelected(Control control) => control.GetValue(IsSelectedProperty);

        /// <summary>
        /// Sets the value of the <see cref="IsSelectedProperty"/> on the specified control.
        /// </summary>
        public static void SetIsSelected(Control control, bool value) => control.SetValue(IsSelectedProperty, value);

        /// <summary>
        /// Tries to get the container that was the source of an event.
        /// </summary>
        protected Control? GetContainerFromEventSource(object? eventSource)
        {
            for (var current = eventSource as Visual; current != null; current = current.GetVisualParent())
            {
                if (current is Control control && control.Parent == this &&
                    IndexFromContainer(control) != -1)
                {
                    return control;
                }
            }

            return null;
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            AutoScrollToSelectedItemIfNecessary(GetAnchorIndex());
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _hasScrolledToSelectedItem = false;
        }

        /// <inheritdoc />
        protected override void OnDataContextBeginUpdate()
        {
            base.OnDataContextBeginUpdate();
            BeginUpdating();
        }

        /// <inheritdoc />
        protected override void OnDataContextEndUpdate()
        {
            base.OnDataContextEndUpdate();
            EndUpdating();
        }

        /// <inheritdoc />
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            var oldItemsSourceView = ItemsSourceView;

            base.OnPropertyChanged(change);

            if (change.Property == ItemsSourceProperty)
            {
                var newItemsSourceView = ItemsSourceView;

                if (!ReferenceEquals(oldItemsSourceView, newItemsSourceView))
                {
                    if (oldItemsSourceView != null)
                    {
                        oldItemsSourceView.CollectionChanged -= OnItemsSourceViewCollectionChanged;
                    }

                    if (newItemsSourceView != null)
                    {
                        newItemsSourceView.CollectionChanged += OnItemsSourceViewCollectionChanged;
                    }
                }

                if (_updateState is null)
                {
                    TryInitializeSelectionSource(_selection, true);
                }

                if (AlwaysSelected && SelectedIndex == -1 && ItemCount > 0 && _updateState is null)
                {
                    SelectedIndex = 0;
                }
            }
            else if (change.Property == AutoScrollToSelectedItemProperty)
            {
                AutoScrollToSelectedItemIfNecessary(GetAnchorIndex());
            }
            else if (change.Property == SelectionModeProperty && _selection is object)
            {
                _selection.SingleSelect = !HasAllFlags(SelectionMode, SelectionMode.Multiple);
            }
            else if (change.Property == SelectedValueProperty)
            {
                if (_isSelectionChangeActive)
                    return;

                if (_updateState is not null)
                {
                    _updateState.SelectedValue = change.NewValue;
                    return;
                }

                SelectItemWithValue(change.NewValue);
            }
            else if (change.Property == SelectedValueBindingProperty)
            {
                var idx = SelectedIndex;

                if (idx == -1)
                {
                    return;
                }

                var value = change.GetNewValue<IBinding?>();
                if (value is null)
                {
                    SetCurrentValue(SelectedValueProperty, SelectedItem);
                    return;
                }

                var selectedItem = SelectedItem;

                try
                {
                    _isSelectionChangeActive = true;

                    var bindingEvaluator = GetSelectedValueBindingEvaluator(value);

                    SetCurrentValue(SelectedValueProperty, bindingEvaluator.Evaluate(selectedItem));
                }
                finally
                {
                    _isSelectionChangeActive = false;
                }
            }
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!e.Handled)
            {
                var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
                var ctrl = hotkeys is not null && HasAllFlags(e.KeyModifiers, hotkeys.CommandModifiers);
                var range = HasAllFlags(e.KeyModifiers, KeyModifiers.Shift);

                if (!ctrl &&
                    e.Key.ToNavigationDirection() is { } direction &&
                    direction.IsDirectional())
                {
                    e.Handled |= MoveSelection(direction, WrapSelection, range);
                }
                else if (HasAllFlags(SelectionMode, SelectionMode.Multiple) &&
                    hotkeys is not null &&
                    hotkeys.SelectAll.Any(x => x.Matches(e)))
                {
                    Selection.SelectAll();
                    e.Handled = true;
                }
                else
                {
                    e.Handled = UpdateSelectionFromEventSource(e.Source, e);
                }
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc />
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.Handled)
            {
                e.Handled = UpdateSelectionFromEventSource(e.Source, e);
            }
        }

        /// <inheritdoc />
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!e.Handled)
            {
                e.Handled = UpdateSelectionFromEventSource(e.Source, e);
            }
        }

        private void OnItemsSourceViewCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updateState is not null)
            {
                return;
            }

            if (AlwaysSelected && SelectedIndex == -1 && ItemCount > 0)
            {
                SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Updates the selection for an item based on user interaction.
        /// </summary>
        protected void UpdateSelection(
            int index,
            bool select = true,
            bool rangeModifier = false,
            bool toggleModifier = false,
            bool rightButton = false,
            bool fromFocus = false)
        {
            if (index < 0 || index >= ItemCount)
            {
                return;
            }

            var mode = SelectionMode;
            var multi = HasAllFlags(mode, SelectionMode.Multiple);
            var toggle = toggleModifier || HasAllFlags(mode, SelectionMode.Toggle);
            var range = multi && rangeModifier;

            if (!select)
            {
                Selection.Deselect(index);
            }
            else if (rightButton)
            {
                if (Selection.IsSelected(index) == false)
                {
                    SelectedIndex = index;
                }
            }
            else if (range)
            {
                using var operation = Selection.BatchUpdate();
                if (!toggleModifier)
                {
                    Selection.Clear();
                }
                Selection.SelectRange(Selection.AnchorIndex, index);
            }
            else if (!fromFocus && toggle)
            {
                if (multi)
                {
                    if (Selection.IsSelected(index))
                    {
                        Selection.Deselect(index);
                    }
                    else
                    {
                        Selection.Select(index);
                    }
                }
                else
                {
                    SelectedIndex = (SelectedIndex == index) ? -1 : index;
                }
            }
            else if (!toggle)
            {
                using var operation = Selection.BatchUpdate();
                Selection.Clear();
                Selection.Select(index);
            }
        }

        /// <summary>
        /// Determines whether the pointer event should trigger selection.
        /// </summary>
        protected virtual bool ShouldTriggerSelection(Visual selectable, PointerEventArgs eventArgs) =>
            ShouldTriggerSelectionInternal(selectable, eventArgs);

        /// <summary>
        /// Determines whether the key event should trigger selection.
        /// </summary>
        protected virtual bool ShouldTriggerSelection(Visual selectable, KeyEventArgs eventArgs) =>
            ShouldTriggerSelectionInternal(selectable, eventArgs);

        /// <summary>
        /// Updates the selection based on an event that may have originated in a container that
        /// belongs to the control.
        /// </summary>
        /// <returns>True if the event was accepted and handled, otherwise false.</returns>
        public virtual bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
        {
            if (eventArgs.Handled)
            {
                return false;
            }

            var containerIndex = IndexFromContainer(container);
            if (containerIndex == -1)
            {
                return false;
            }

            switch (eventArgs)
            {
                case PointerEventArgs pointerEvent when ShouldTriggerSelection(container, pointerEvent):
                    UpdateSelection(containerIndex, true,
                        HasRangeSelectionModifier(container, eventArgs),
                        HasToggleSelectionModifier(container, eventArgs),
                        pointerEvent.GetCurrentPoint(container).Properties.IsRightButtonPressed);

                    eventArgs.Handled = true;
                    return true;

                case KeyEventArgs keyEvent when ShouldTriggerSelection(container, keyEvent):
                    UpdateSelection(containerIndex, true,
                        HasRangeSelectionModifier(container, eventArgs),
                        HasToggleSelectionModifier(container, eventArgs));

                    eventArgs.Handled = true;
                    return true;

                default:
                    return false;
            }
        }

        private bool UpdateSelectionFromEventSource(object? eventSource, RoutedEventArgs eventArgs)
        {
            var container = GetContainerFromEventSource(eventSource);

            if (container != null)
            {
                return UpdateSelectionFromEvent(container, eventArgs);
            }

            return false;
        }

        private int ItemCount => ItemsSourceView?.Count ?? 0;

        private int IndexFromContainer(Control container) => GetElementIndex(container);

        private Control? ContainerFromIndex(int index) => TryGetElement(index);

        private int GetAnchorIndex()
        {
            var selection = _updateState is not null ? TryGetExistingSelection() : Selection;
            return selection?.AnchorIndex ?? -1;
        }

        private ISelectionModel? TryGetExistingSelection()
            => _updateState?.Selection.HasValue == true ? _updateState.Selection.Value : _selection;

        private ISelectionModel GetOrCreateSelectionModel()
        {
            if (_selection is null)
            {
                _selection = CreateDefaultSelectionModel();
                InitializeSelectionModel(_selection);
            }

            return _selection;
        }

        private void OnSelectionModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ISelectionModel.AnchorIndex))
            {
                _hasScrolledToSelectedItem = false;
                var anchorIndex = GetAnchorIndex();
                KeyboardNavigation.SetTabOnceActiveElement(this, ContainerFromIndex(anchorIndex));
                AutoScrollToSelectedItemIfNecessary(anchorIndex);
            }
            else if (e.PropertyName == nameof(ISelectionModel.SelectedIndex))
            {
                var selectedIndex = SelectedIndex;
                var oldSelectedIndex = _oldSelectedIndex;
                if (_oldSelectedIndex != selectedIndex)
                {
                    RaisePropertyChanged(SelectedIndexProperty, oldSelectedIndex, selectedIndex);
                    _oldSelectedIndex = selectedIndex;
                }
            }
            else if (e.PropertyName == nameof(ISelectionModel.SelectedItem))
            {
                var selectedItem = SelectedItem;
                var oldSelectedItem = _oldSelectedItem.Target;
                if (selectedItem != oldSelectedItem)
                {
                    RaisePropertyChanged(SelectedItemProperty, oldSelectedItem, selectedItem);
                    _oldSelectedItem.Target = selectedItem;
                }
            }
            else if (e.PropertyName == nameof(RepeaterSelectionModel.WritableSelectedItems))
            {
                _oldSelectedItems.TryGetTarget(out var oldSelectedItems);
                if (oldSelectedItems != (Selection as RepeaterSelectionModel)?.SelectedItems)
                {
                    var selectedItems = SelectedItems;
                    RaisePropertyChanged(SelectedItemsProperty, oldSelectedItems, selectedItems);
                    _oldSelectedItems.SetTarget(selectedItems);
                }
            }
            else if (e.PropertyName == nameof(ISelectionModel.Source))
            {
                ClearValue(SelectedValueProperty);
            }
        }

        private void OnSelectionModelSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e)
        {
            void Mark(int index, bool selected)
            {
                var container = ContainerFromIndex(index);

                if (container != null)
                {
                    MarkContainerSelected(container, selected);
                }
            }

            foreach (var i in e.SelectedIndexes)
            {
                Mark(i, true);
            }

            foreach (var i in e.DeselectedIndexes)
            {
                Mark(i, false);
            }

            if (!_isSelectionChangeActive)
            {
                UpdateSelectedValueFromItem();
            }

            var route = BuildEventRoute(SelectionChangedEvent);

            if (route.HasHandlers)
            {
                var ev = new SelectionChangedEventArgs(
                    SelectionChangedEvent,
                    e.DeselectedItems.ToArray(),
                    e.SelectedItems.ToArray());
                RaiseEvent(ev);
            }
        }

        private void OnSelectionModelLostSelection(object? sender, EventArgs e)
        {
            if (AlwaysSelected && ItemCount > 0)
            {
                SelectedIndex = 0;
            }
        }

        private void SelectItemWithValue(object? value)
        {
            if (ItemCount == 0 || _isSelectionChangeActive)
                return;

            try
            {
                _isSelectionChangeActive = true;
                var si = FindItemWithValue(value);
                if (si != AvaloniaProperty.UnsetValue)
                {
                    SelectedItem = si;
                }
                else
                {
                    SelectedItem = null;
                }
            }
            finally
            {
                _isSelectionChangeActive = false;
            }
        }

        private object? FindItemWithValue(object? value)
        {
            if (ItemCount == 0 || value is null)
            {
                return AvaloniaProperty.UnsetValue;
            }

            var items = ItemsSourceView;
            var binding = SelectedValueBinding;

            if (items is null)
            {
                return AvaloniaProperty.UnsetValue;
            }

            if (binding is null)
            {
                var index = items.IndexOf(value);

                if (index >= 0)
                {
                    return value;
                }

                return AvaloniaProperty.UnsetValue;
            }

            var bindingEvaluator = GetSelectedValueBindingEvaluator(binding);

            foreach (var item in items)
            {
                var itemValue = bindingEvaluator.Evaluate(item);

                if (Equals(itemValue, value))
                {
                    bindingEvaluator.ClearDataContext();
                    return item;
                }
            }

            bindingEvaluator.ClearDataContext();

            return AvaloniaProperty.UnsetValue;
        }

        private void UpdateSelectedValueFromItem()
        {
            if (_isSelectionChangeActive)
                return;

            var binding = SelectedValueBinding;
            var item = SelectedItem;

            if (binding is null || item is null)
            {
                try
                {
                    _isSelectionChangeActive = true;
                    SetCurrentValue(SelectedValueProperty, item);
                }
                finally
                {
                    _isSelectionChangeActive = false;
                }
                return;
            }

            var bindingEvaluator = GetSelectedValueBindingEvaluator(binding);

            try
            {
                _isSelectionChangeActive = true;
                SetCurrentValue(SelectedValueProperty, bindingEvaluator.Evaluate(item));
            }
            finally
            {
                _isSelectionChangeActive = false;
            }
        }

        private void AutoScrollToSelectedItemIfNecessary(int anchorIndex)
        {
            if (AutoScrollToSelectedItem &&
                !_hasScrolledToSelectedItem &&
                anchorIndex >= 0 &&
                this.IsAttachedToVisualTree())
            {
                Dispatcher.UIThread.Post(state =>
                {
                    ScrollIntoView((int)state!);
                    _hasScrolledToSelectedItem = true;
                }, anchorIndex);
            }
        }

        private Control? ScrollIntoView(int index)
        {
            if ((uint)index >= (uint)ItemCount)
            {
                return null;
            }

            var element = TryGetElement(index) ?? GetOrCreateElement(index);
            element?.BringIntoView();
            return element;
        }

        private bool MoveSelection(NavigationDirection direction, bool wrap = false, bool rangeModifier = false)
        {
            var count = ItemCount;

            if (count == 0)
            {
                return false;
            }

            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            var from = GetContainerFromEventSource(focused);
            if (from is null && Selection.AnchorIndex >= 0)
            {
                from = ContainerFromIndex(Selection.AnchorIndex);
            }
            var fromIndex = from is not null ? IndexFromContainer(from) : Selection.AnchorIndex;

            if (fromIndex < 0)
            {
                direction = direction switch
                {
                    NavigationDirection.Down => NavigationDirection.First,
                    NavigationDirection.Right => NavigationDirection.First,
                    NavigationDirection.PageDown => NavigationDirection.First,
                    NavigationDirection.Up => NavigationDirection.Last,
                    NavigationDirection.Left => NavigationDirection.Last,
                    NavigationDirection.PageUp => NavigationDirection.Last,
                    _ => direction
                };
            }

            var normalized = direction switch
            {
                NavigationDirection.PageDown => NavigationDirection.Down,
                NavigationDirection.PageUp => NavigationDirection.Up,
                _ => direction
            };

            if (normalized is NavigationDirection.Left or NavigationDirection.Right or NavigationDirection.Up or NavigationDirection.Down)
            {
                var nextContainer = GetContainerInDirection(from, normalized, wrap);

                if (nextContainer is not null)
                {
                    var nextIndex = IndexFromContainer(nextContainer);

                    if (nextIndex != -1 && nextIndex != fromIndex)
                    {
                        UpdateSelection(nextIndex, true, rangeModifier);
                        nextContainer.BringIntoView();
                        nextContainer.Focus();
                        return true;
                    }
                }
            }

            var toIndex = fromIndex;

            switch (direction)
            {
                case NavigationDirection.First:
                    toIndex = 0;
                    break;
                case NavigationDirection.Last:
                    toIndex = count - 1;
                    break;
                case NavigationDirection.PageDown:
                case NavigationDirection.Next:
                    ++toIndex;
                    break;
                case NavigationDirection.PageUp:
                case NavigationDirection.Previous:
                    --toIndex;
                    break;
                default:
                    return false;
            }

            if (wrap)
            {
                if (toIndex < 0)
                {
                    toIndex = count - 1;
                }
                else if (toIndex >= count)
                {
                    toIndex = 0;
                }
            }

            if (toIndex < 0 || toIndex >= count || toIndex == fromIndex)
            {
                return false;
            }

            UpdateSelection(toIndex, true, rangeModifier);

            var element = ScrollIntoView(toIndex);
            element?.Focus();

            return true;
        }

        private Control? GetContainerInDirection(Control? from, NavigationDirection direction, bool wrap)
        {
            if (from is null)
            {
                return direction is NavigationDirection.Left or NavigationDirection.Up
                    ? GetLastRealizedContainer()
                    : GetFirstRealizedContainer();
            }

            if (TryGetContainerBounds(from) is not Rect fromBounds)
            {
                return null;
            }

            var fromCenter = fromBounds.Center;
            Control? best = null;
            var bestScore = double.PositiveInfinity;

            foreach (var candidate in GetRealizedContainers())
            {
                if (candidate == from || !IsFocusableCandidate(candidate))
                {
                    continue;
                }

                if (TryGetContainerBounds(candidate) is not Rect bounds)
                {
                    continue;
                }

                var center = bounds.Center;

                if (!IsInDirection(direction, fromCenter, center))
                {
                    continue;
                }

                var dx = center.X - fromCenter.X;
                var dy = center.Y - fromCenter.Y;
                var primary = direction is NavigationDirection.Left or NavigationDirection.Right ? Math.Abs(dx) : Math.Abs(dy);
                var secondary = direction is NavigationDirection.Left or NavigationDirection.Right ? Math.Abs(dy) : Math.Abs(dx);
                var score = (primary * primary) + (secondary * secondary);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best is null && wrap)
            {
                return direction is NavigationDirection.Left or NavigationDirection.Up
                    ? GetLastRealizedContainer()
                    : GetFirstRealizedContainer();
            }

            return best;
        }

        private IEnumerable<Control> GetRealizedContainers()
        {
            foreach (var container in Children)
            {
                var info = TryGetVirtualizationInfo(container);
                if (info?.IsRealized == true)
                {
                    yield return container;
                }
            }
        }

        private static bool IsFocusableCandidate(Control container)
        {
            return container.Focusable && container.IsEffectivelyEnabled && container.IsEffectivelyVisible;
        }

        private Control? GetFirstRealizedContainer()
        {
            foreach (var container in Children)
            {
                var info = TryGetVirtualizationInfo(container);
                if (info?.IsRealized == true)
                {
                    return container;
                }
            }

            return null;
        }

        private Control? GetLastRealizedContainer()
        {
            for (var i = Children.Count - 1; i >= 0; --i)
            {
                var container = Children[i];
                var info = TryGetVirtualizationInfo(container);
                if (info?.IsRealized == true)
                {
                    return container;
                }
            }

            return null;
        }

        private Rect? TryGetContainerBounds(Control container)
        {
            var topLeft = container.TranslatePoint(default, this);
            if (topLeft is null)
            {
                return null;
            }

            return new Rect(topLeft.Value, container.Bounds.Size);
        }

        private static bool IsInDirection(NavigationDirection direction, Point from, Point candidate)
        {
            const double epsilon = 0.5;

            return direction switch
            {
                NavigationDirection.Left => candidate.X < from.X - epsilon,
                NavigationDirection.Right => candidate.X > from.X + epsilon,
                NavigationDirection.Up => candidate.Y < from.Y - epsilon,
                NavigationDirection.Down => candidate.Y > from.Y + epsilon,
                _ => false
            };
        }

        private void ApplyContainerSelection(Control container, int index)
        {
            if (index < 0)
            {
                return;
            }

            var isExternallyManaged = !GetIsSelectedManaged(container) && container.IsSet(IsSelectedProperty);

            if (isExternallyManaged)
            {
                var containerIsSelected = GetIsSelected(container);
                SetContainerPseudoClass(container, containerIsSelected);
                UpdateSelection(index, containerIsSelected, toggleModifier: true);
            }
            else
            {
                MarkContainerSelected(container, Selection.IsSelected(index));
            }
        }

        private void MarkContainerSelected(Control container, bool selected)
        {
            _ignoreContainerSelectionChanged = true;

            try
            {
                SetIsSelectedManaged(container, true);
                container.SetCurrentValue(IsSelectedProperty, selected);
                SetContainerPseudoClass(container, selected);
            }
            finally
            {
                _ignoreContainerSelectionChanged = false;
            }
        }

        private void ClearContainerSelection(Control container)
        {
            SetContainerPseudoClass(container, false);

            if (!GetIsSelectedManaged(container))
            {
                return;
            }

            _ignoreContainerSelectionChanged = true;

            try
            {
                container.ClearValue(IsSelectedProperty);
                SetIsSelectedManaged(container, false);
            }
            finally
            {
                _ignoreContainerSelectionChanged = false;
            }
        }

        private void UpdateContainerSelection()
        {
            foreach (var container in Children)
            {
                var info = TryGetVirtualizationInfo(container);
                if (info?.IsRealized != true)
                {
                    continue;
                }

                MarkContainerSelected(container, Selection.IsSelected(IndexFromContainer(container)));
            }
        }

        private ISelectionModel CreateDefaultSelectionModel()
        {
            return new RepeaterSelectionModel
            {
                SingleSelect = !HasAllFlags(SelectionMode, SelectionMode.Multiple),
            };
        }

        private void InitializeSelectionModel(ISelectionModel model)
        {
            if (_updateState is null)
            {
                TryInitializeSelectionSource(model, false);
            }

            model.PropertyChanged += OnSelectionModelPropertyChanged;
            model.SelectionChanged += OnSelectionModelSelectionChanged;
            model.LostSelection += OnSelectionModelLostSelection;

            if (model.SingleSelect)
            {
                SelectionMode &= ~SelectionMode.Multiple;
            }
            else
            {
                SelectionMode |= SelectionMode.Multiple;
            }

            _oldSelectedIndex = model.SelectedIndex;
            _oldSelectedItem.Target = model.SelectedItem;

            if (_updateState is null && AlwaysSelected && model.Count == 0)
            {
                model.SelectedIndex = 0;
            }

            UpdateContainerSelection();

            if (SelectedIndex != -1)
            {
                RaiseEvent(new SelectionChangedEventArgs(
                    SelectionChangedEvent,
                    Array.Empty<object>(),
                    Selection.SelectedItems.ToArray()));
            }
        }

        private void TryInitializeSelectionSource(ISelectionModel? selection, bool shouldSelectItemFromSelectedValue)
        {
            if (selection is not null && ItemsSourceView?.Source is { } source)
            {
                if (shouldSelectItemFromSelectedValue && selection.SelectedIndex == -1 && selection.SelectedItem is null)
                {
                    var item = FindItemWithValue(SelectedValue);
                    if (item != AvaloniaProperty.UnsetValue)
                        selection.SelectedItem = item;
                }

                selection.Source = source;
            }
        }

        private void DeinitializeSelectionModel(ISelectionModel? model)
        {
            if (model is object)
            {
                model.PropertyChanged -= OnSelectionModelPropertyChanged;
                model.SelectionChanged -= OnSelectionModelSelectionChanged;
            }
        }

        private void BeginUpdating()
        {
            _updateState ??= new UpdateState();
            _updateState.UpdateCount++;
        }

        private void EndUpdating()
        {
            if (_updateState is object && --_updateState.UpdateCount == 0)
            {
                var state = _updateState;
                _updateState = null;

                if (state.Selection.HasValue)
                {
                    Selection = state.Selection.Value;
                }

                if (_selection is RepeaterSelectionModel s)
                {
                    s.Update(ItemsSourceView?.Source, state.SelectedItems);
                }
                else
                {
                    if (state.SelectedItems.HasValue)
                    {
                        SelectedItems = state.SelectedItems.Value;
                    }

                    TryInitializeSelectionSource(Selection, false);
                }

                if (state.SelectedValue.HasValue)
                {
                    var item = FindItemWithValue(state.SelectedValue.Value);
                    if (item != AvaloniaProperty.UnsetValue)
                        state.SelectedItem = item;
                }

                if (state.SelectedIndex.HasValue)
                {
                    var selectedIndex = state.SelectedIndex.Value;
                    if (selectedIndex >= 0 || !state.SelectedItem.HasValue)
                        SelectedIndex = selectedIndex;
                    else
                        SelectedItem = state.SelectedItem.Value;
                }
                else if (state.SelectedItem.HasValue)
                {
                    SelectedItem = state.SelectedItem.Value;
                }

                if (AlwaysSelected && SelectedIndex == -1 && ItemCount > 0)
                {
                    SelectedIndex = 0;
                }
            }
        }

        private BindingEvaluator<object?> GetSelectedValueBindingEvaluator(IBinding binding)
        {
            _selectedValueBindingEvaluator ??= new();
            _selectedValueBindingEvaluator.UpdateBinding(binding);
            return _selectedValueBindingEvaluator;
        }

        private void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
        {
            EnsureContainerFocusable(e.Element);
            RegisterContainerSelection(e.Element);
            ApplyContainerSelection(e.Element, e.Index);
        }

        private void OnElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
        {
            UnregisterContainerSelection(e.Element);
            ClearContainerSelection(e.Element);
        }

        private void OnElementIndexChanged(object? sender, ItemsRepeaterElementIndexChangedEventArgs e)
        {
            ApplyContainerSelection(e.Element, e.NewIndex);
        }

        private void RegisterContainerSelection(Control element)
        {
            if (_selectionSubscriptions.Add(element))
            {
                element.PropertyChanged += OnContainerPropertyChanged;
            }
        }

        private void UnregisterContainerSelection(Control element)
        {
            if (_selectionSubscriptions.Remove(element))
            {
                element.PropertyChanged -= OnContainerPropertyChanged;
            }
        }

        private void OnContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != IsSelectedProperty || sender is not Control control)
            {
                return;
            }

            var isSelected = e.GetNewValue<bool>();
            ((IPseudoClasses)control.Classes).Set(":selected", isSelected);

            if (_ignoreContainerSelectionChanged)
            {
                return;
            }

            SetIsSelectedManaged(control, false);

            var index = IndexFromContainer(control);
            if (index < 0)
            {
                return;
            }

            if (isSelected)
            {
                Selection.Select(index);
            }
            else
            {
                Selection.Deselect(index);
            }
        }

        private static bool GetIsSelectedManaged(Control control) => control.GetValue(IsSelectedManagedProperty);

        private static void SetIsSelectedManaged(Control control, bool value) => control.SetValue(IsSelectedManagedProperty, value);

        private static void SetContainerPseudoClass(Control container, bool selected)
        {
            ((IPseudoClasses)container.Classes).Set(":selected", selected);
        }

        private static void EnsureContainerFocusable(Control container)
        {
            if (!container.IsSet(FocusableProperty))
            {
                container.SetCurrentValue(FocusableProperty, true);
            }
        }

        private static bool ShouldTriggerSelectionInternal(Visual selectable, PointerEventArgs eventArgs)
        {
            if (!IsPointerEventWithinBounds(selectable, eventArgs))
            {
                return false;
            }

            var properties = eventArgs.GetCurrentPoint(selectable).Properties;

            if (properties.PointerUpdateKind is not (
                PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed or
                PointerUpdateKind.LeftButtonReleased or PointerUpdateKind.RightButtonReleased))
            {
                return false;
            }

            switch (eventArgs.Pointer.Type)
            {
                case PointerType.Mouse:
                    return Gestures.GetIsHoldWithMouseEnabled(selectable)
                        ? eventArgs.RoutedEvent == InputElement.PointerReleasedEvent
                        : eventArgs.RoutedEvent == InputElement.PointerPressedEvent;
                case PointerType.Pen:
                    return properties.PointerUpdateKind is PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased
                        ? eventArgs.RoutedEvent == InputElement.PointerPressedEvent
                        : eventArgs.RoutedEvent == InputElement.PointerReleasedEvent;
                case PointerType.Touch:
                    return eventArgs.RoutedEvent == InputElement.PointerReleasedEvent;
                default:
                    return false;
            }
        }

        private static bool ShouldTriggerSelectionInternal(Visual selectable, KeyEventArgs eventArgs)
        {
            return eventArgs.Source == selectable &&
                eventArgs.Key is Key.Space or Key.Enter &&
                eventArgs.RoutedEvent == InputElement.KeyDownEvent;
        }

        private static bool HasRangeSelectionModifier(Visual selectable, RoutedEventArgs eventArgs) =>
            HasModifiers(eventArgs, GetHotkeys(selectable)?.SelectionModifiers);

        private static bool HasToggleSelectionModifier(Visual selectable, RoutedEventArgs eventArgs) =>
            HasModifiers(eventArgs, GetHotkeys(selectable)?.CommandModifiers);

        private static PlatformHotkeyConfiguration? GetHotkeys(Visual element) =>
            (TopLevel.GetTopLevel(element)?.PlatformSettings ?? Application.Current?.PlatformSettings)?.HotkeyConfiguration;

        private static bool HasModifiers(RoutedEventArgs eventArgs, KeyModifiers? modifiers)
        {
            if (modifiers == null)
            {
                return false;
            }

            KeyModifiers? eventModifiers = null;

            if (eventArgs is KeyEventArgs keyArgs)
            {
                eventModifiers = keyArgs.KeyModifiers;
            }
            else if (eventArgs is PointerEventArgs pointerArgs)
            {
                eventModifiers = pointerArgs.KeyModifiers;
            }

            return eventModifiers.HasValue && HasAllFlags(eventModifiers.Value, modifiers.Value);
        }

        private static bool IsPointerEventWithinBounds(Visual selectable, PointerEventArgs eventArgs) =>
            new Rect(selectable.Bounds.Size).Contains(eventArgs.GetPosition(selectable));

        private static bool HasAllFlags(SelectionMode value, SelectionMode flags) => (value & flags) == flags;

        private static bool HasAllFlags(KeyModifiers value, KeyModifiers flags) => (value & flags) == flags;

        private class UpdateState
        {
            public int UpdateCount { get; set; }
            public Optional<ISelectionModel> Selection { get; set; }
            public Optional<IList?> SelectedItems { get; set; }
            public Optional<int> SelectedIndex { get; set; }
            public Optional<object?> SelectedItem { get; set; }
            public Optional<object?> SelectedValue { get; set; }
        }
    }
}
