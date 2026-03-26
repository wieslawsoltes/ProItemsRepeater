using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;

namespace Avalonia.Controls
{
    [Flags]
    public enum SelectionMode
    {
        Single = 0x00,
        Multiple = 0x01,
        Toggle = 0x02,
        AlwaysSelected = 0x04,
    }

    public class SelectionChangedEventArgs : EventArgs
    {
        public SelectionChangedEventArgs(IList removedItems, IList addedItems)
        {
            RemovedItems = removedItems;
            AddedItems = addedItems;
        }

        public IList AddedItems { get; }
        public IList RemovedItems { get; }
    }
}

namespace Avalonia.Controls.Selection
{
    public interface ISelectionModel : INotifyPropertyChanged
    {
        IEnumerable? Source { get; set; }
        bool SingleSelect { get; set; }
        int SelectedIndex { get; set; }
        IReadOnlyList<int> SelectedIndexes { get; }
        object? SelectedItem { get; set; }
        IReadOnlyList<object?> SelectedItems { get; }
        int AnchorIndex { get; set; }
        int Count { get; }

        event EventHandler<SelectionModelIndexesChangedEventArgs>? IndexesChanged;
        event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged;
        event EventHandler? LostSelection;
        event EventHandler? SourceReset;

        void BeginBatchUpdate();
        void EndBatchUpdate();
        bool IsSelected(int index);
        void Select(int index);
        void Deselect(int index);
        void SelectRange(int start, int end);
        void DeselectRange(int start, int end);
        void SelectAll();
        void Clear();
    }

    public static class SelectionModelExtensions
    {
        public static BatchUpdateOperation BatchUpdate(this ISelectionModel model) => new(model);

        public readonly struct BatchUpdateOperation : IDisposable
        {
            private readonly ISelectionModel _owner;

            public BatchUpdateOperation(ISelectionModel owner)
            {
                _owner = owner;
                owner.BeginBatchUpdate();
            }

            public void Dispose()
            {
                _owner.EndBatchUpdate();
            }
        }
    }

    public class SelectionModelIndexesChangedEventArgs : EventArgs
    {
        public SelectionModelIndexesChangedEventArgs(int startIndex, int delta)
        {
            StartIndex = startIndex;
            Delta = delta;
        }

        public int StartIndex { get; }
        public int Delta { get; }
    }

    public abstract class SelectionModelSelectionChangedEventArgs : EventArgs
    {
        public abstract IReadOnlyList<int> DeselectedIndexes { get; }
        public abstract IReadOnlyList<int> SelectedIndexes { get; }
        public IReadOnlyList<object?> DeselectedItems => GetUntypedDeselectedItems();
        public IReadOnlyList<object?> SelectedItems => GetUntypedSelectedItems();

        protected abstract IReadOnlyList<object?> GetUntypedDeselectedItems();
        protected abstract IReadOnlyList<object?> GetUntypedSelectedItems();
    }

    public class SelectionModelSelectionChangedEventArgs<T> : SelectionModelSelectionChangedEventArgs
    {
        private IReadOnlyList<object?>? _deselectedItemsUntyped;
        private IReadOnlyList<object?>? _selectedItemsUntyped;

        public SelectionModelSelectionChangedEventArgs(
            IReadOnlyList<int>? deselectedIndices = null,
            IReadOnlyList<int>? selectedIndices = null,
            IReadOnlyList<T?>? deselectedItems = null,
            IReadOnlyList<T?>? selectedItems = null)
        {
            DeselectedIndexes = deselectedIndices ?? Array.Empty<int>();
            SelectedIndexes = selectedIndices ?? Array.Empty<int>();
            DeselectedItems = deselectedItems ?? Array.Empty<T?>();
            SelectedItems = selectedItems ?? Array.Empty<T?>();
        }

        public override IReadOnlyList<int> DeselectedIndexes { get; }
        public override IReadOnlyList<int> SelectedIndexes { get; }
        public new IReadOnlyList<T?> DeselectedItems { get; }
        public new IReadOnlyList<T?> SelectedItems { get; }

        protected override IReadOnlyList<object?> GetUntypedDeselectedItems() =>
            _deselectedItemsUntyped ??= DeselectedItems.Cast<object?>().ToArray();

        protected override IReadOnlyList<object?> GetUntypedSelectedItems() =>
            _selectedItemsUntyped ??= SelectedItems.Cast<object?>().ToArray();
    }

    public class SelectionModel<T> : ISelectionModel
    {
        private IEnumerable? _source;
        private readonly SortedSet<int> _selectedIndexes = new();
        private int _selectedIndex = -1;
        private int _anchorIndex = -1;
        private bool _singleSelect = true;
        private int _batchDepth;
        private List<int>? _batchStartIndexes;
        private List<object?>? _batchStartItems;
        private INotifyCollectionChanged? _observableSource;

        public IEnumerable? Source
        {
            get => _source;
            set
            {
                if (ReferenceEquals(_source, value))
                    return;

                var oldSelectedItems = SelectedItems.ToArray();
                UnsubscribeFromSource();
                _source = value;
                SubscribeToSource();

                using var update = this.BatchUpdate();
                RebuildSelectionFromItems(oldSelectedItems);
                SourceReset?.Invoke(this, EventArgs.Empty);
                RaisePropertyChanged(nameof(Source));
            }
        }

        public bool SingleSelect
        {
            get => _singleSelect;
            set
            {
                if (_singleSelect == value)
                    return;

                _singleSelect = value;
                if (value && _selectedIndexes.Count > 1)
                {
                    var keepIndex = SelectedIndex;
                    Clear();
                    Select(keepIndex);
                }

                RaisePropertyChanged(nameof(SingleSelect));
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value < 0)
                {
                    Clear();
                    return;
                }

                using var update = this.BatchUpdate();
                SetSelection(new[] { value });
                AnchorIndex = value;
            }
        }

        public IReadOnlyList<int> SelectedIndexes => _selectedIndexes.ToArray();

        public object? SelectedItem
        {
            get
            {
                if (_selectedIndex < 0)
                    return default;

                return GetItemAt(_selectedIndex);
            }
            set
            {
                var index = IndexOf(value);
                SelectedIndex = index;
            }
        }

        public IReadOnlyList<object?> SelectedItems =>
            _selectedIndexes
                .Select(GetItemAt)
                .Where(static x => true)
                .ToArray();

        public int AnchorIndex
        {
            get => _anchorIndex;
            set
            {
                if (_anchorIndex == value)
                    return;

                _anchorIndex = value;
                RaisePropertyChanged(nameof(AnchorIndex));
            }
        }

        public int Count => _selectedIndexes.Count;

        public event EventHandler<SelectionModelIndexesChangedEventArgs>? IndexesChanged;
        public event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged;
        public event EventHandler? LostSelection;
        public event EventHandler? SourceReset;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void BeginBatchUpdate()
        {
            if (_batchDepth == 0)
            {
                _batchStartIndexes = _selectedIndexes.ToList();
                _batchStartItems = SelectedItems.ToList();
            }

            ++_batchDepth;
        }

        public void EndBatchUpdate()
        {
            if (_batchDepth == 0)
                throw new InvalidOperationException("No batch update in progress.");

            --_batchDepth;
            if (_batchDepth == 0)
            {
                RaiseSelectionChanges(
                    _batchStartIndexes is not null ? _batchStartIndexes : Array.Empty<int>(),
                    _batchStartItems is not null ? _batchStartItems : Array.Empty<object?>());
                _batchStartIndexes = null;
                _batchStartItems = null;
            }
        }

        public bool IsSelected(int index) => _selectedIndexes.Contains(index);

        public void Select(int index)
        {
            if (!IsValidIndex(index))
                return;

            using var update = this.BatchUpdate();

            if (SingleSelect)
            {
                SetSelection(new[] { index });
            }
            else
            {
                _selectedIndexes.Add(index);
                UpdatePrimarySelection();
            }

            AnchorIndex = index;
        }

        public void Deselect(int index)
        {
            using var update = this.BatchUpdate();
            if (_selectedIndexes.Remove(index))
                UpdatePrimarySelection();
        }

        public void SelectRange(int start, int end)
        {
            if (GetCount() == 0)
                return;

            var (from, to) = NormalizeRange(start, end);
            using var update = this.BatchUpdate();

            if (SingleSelect)
            {
                SetSelection(new[] { to });
            }
            else
            {
                for (var i = from; i <= to; ++i)
                    _selectedIndexes.Add(i);

                UpdatePrimarySelection();
            }
        }

        public void DeselectRange(int start, int end)
        {
            if (_selectedIndexes.Count == 0)
                return;

            var (from, to) = NormalizeRange(start, end);
            using var update = this.BatchUpdate();
            for (var i = from; i <= to; ++i)
                _selectedIndexes.Remove(i);

            UpdatePrimarySelection();
        }

        public void SelectAll()
        {
            if (SingleSelect)
            {
                if (GetCount() > 0)
                    SelectedIndex = 0;
                return;
            }

            using var update = this.BatchUpdate();
            _selectedIndexes.Clear();
            for (var i = 0; i < GetCount(); ++i)
                _selectedIndexes.Add(i);
            UpdatePrimarySelection();
        }

        public void Clear()
        {
            using var update = this.BatchUpdate();
            _selectedIndexes.Clear();
            UpdatePrimarySelection();
        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnSourceCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            using var update = this.BatchUpdate();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    ShiftSelectedIndexes(e.NewStartingIndex, e.NewItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveAndShiftSelectedIndexes(e.OldStartingIndex, e.OldItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Move:
                    MoveSelectedIndexes(e.OldStartingIndex, e.NewStartingIndex, e.NewItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    break;

                case NotifyCollectionChangedAction.Reset:
                    Clear();
                    SourceReset?.Invoke(this, EventArgs.Empty);
                    return;
            }

            var delta = e.Action switch
            {
                NotifyCollectionChangedAction.Add => e.NewItems?.Count ?? 0,
                NotifyCollectionChangedAction.Remove => -(e.OldItems?.Count ?? 0),
                NotifyCollectionChangedAction.Replace => 0,
                NotifyCollectionChangedAction.Move => 0,
                _ => 0,
            };

            IndexesChanged?.Invoke(
                this,
                new SelectionModelIndexesChangedEventArgs(
                    Math.Max(0, e.NewStartingIndex >= 0 ? e.NewStartingIndex : e.OldStartingIndex),
                    delta));
        }

        private void ShiftSelectedIndexes(int startIndex, int delta)
        {
            if (delta == 0 || _selectedIndexes.Count == 0 || startIndex < 0)
                return;

            var updated = new SortedSet<int>();
            foreach (var index in _selectedIndexes)
            {
                updated.Add(index >= startIndex ? index + delta : index);
            }

            ReplaceSelectedIndexes(updated);
        }

        private void RemoveAndShiftSelectedIndexes(int startIndex, int removedCount)
        {
            if (removedCount == 0 || _selectedIndexes.Count == 0 || startIndex < 0)
                return;

            var updated = new SortedSet<int>();
            foreach (var index in _selectedIndexes)
            {
                if (index < startIndex)
                {
                    updated.Add(index);
                }
                else if (index >= startIndex + removedCount)
                {
                    updated.Add(index - removedCount);
                }
            }

            ReplaceSelectedIndexes(updated);
        }

        private void MoveSelectedIndexes(int oldIndex, int newIndex, int movedCount)
        {
            if (movedCount <= 0 || _selectedIndexes.Count == 0 || oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
                return;

            var updated = new SortedSet<int>();
            foreach (var index in _selectedIndexes)
            {
                if (index >= oldIndex && index < oldIndex + movedCount)
                {
                    updated.Add(newIndex + (index - oldIndex));
                }
                else if (oldIndex < newIndex && index > oldIndex + movedCount - 1 && index <= newIndex + movedCount - 1)
                {
                    updated.Add(index - movedCount);
                }
                else if (newIndex < oldIndex && index >= newIndex && index < oldIndex)
                {
                    updated.Add(index + movedCount);
                }
                else
                {
                    updated.Add(index);
                }
            }

            ReplaceSelectedIndexes(updated);
        }

        private void ReplaceSelectedIndexes(SortedSet<int> updated)
        {
            _selectedIndexes.Clear();
            foreach (var index in updated)
                _selectedIndexes.Add(index);

            if (_selectedIndexes.Count == 0)
                LostSelection?.Invoke(this, EventArgs.Empty);

            UpdatePrimarySelection();
        }

        private void SubscribeToSource()
        {
            _observableSource = _source as INotifyCollectionChanged;
            if (_observableSource is not null)
                _observableSource.CollectionChanged += OnSourceCollectionChanged;
        }

        private void UnsubscribeFromSource()
        {
            if (_observableSource is not null)
                _observableSource.CollectionChanged -= OnSourceCollectionChanged;
            _observableSource = null;
        }

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSourceCollectionChanged(e);

        private void RebuildSelectionFromItems(IEnumerable<object?> selectedItems)
        {
            var oldCount = _selectedIndexes.Count;
            _selectedIndexes.Clear();

            foreach (var item in selectedItems)
            {
                var index = IndexOf(item);
                if (index >= 0)
                {
                    if (SingleSelect)
                    {
                        _selectedIndexes.Clear();
                        _selectedIndexes.Add(index);
                        break;
                    }

                    _selectedIndexes.Add(index);
                }
            }

            UpdatePrimarySelection();

            if (oldCount > 0 && _selectedIndexes.Count == 0)
                LostSelection?.Invoke(this, EventArgs.Empty);
        }

        private void SetSelection(IEnumerable<int> indexes)
        {
            _selectedIndexes.Clear();
            foreach (var index in indexes.Where(IsValidIndex))
            {
                _selectedIndexes.Add(index);
                if (SingleSelect)
                    break;
            }

            UpdatePrimarySelection();
        }

        private void UpdatePrimarySelection()
        {
            var newSelectedIndex = _selectedIndexes.Count > 0 ? _selectedIndexes.Min : -1;
            var selectedIndexChanged = _selectedIndex != newSelectedIndex;
            _selectedIndex = newSelectedIndex;

            if (selectedIndexChanged)
            {
                RaisePropertyChanged(nameof(SelectedIndex));
                RaisePropertyChanged(nameof(SelectedItem));
            }

            RaisePropertyChanged(nameof(SelectedIndexes));
            RaisePropertyChanged(nameof(SelectedItems));
            RaisePropertyChanged(nameof(Count));
        }

        private void RaiseSelectionChanges(IReadOnlyList<int> oldIndexes, IReadOnlyList<object?> oldItems)
        {
            var newIndexes = SelectedIndexes;
            var newItems = SelectedItems;

            if (oldIndexes.SequenceEqual(newIndexes) && oldItems.SequenceEqual(newItems))
                return;

            var deselectedIndexes = oldIndexes.Except(newIndexes).ToArray();
            var selectedIndexes = newIndexes.Except(oldIndexes).ToArray();
            var deselectedItems = oldItems.Except(newItems).Cast<T?>().ToArray();
            var selectedItems = newItems.Except(oldItems).Cast<T?>().ToArray();

            SelectionChanged?.Invoke(
                this,
                new SelectionModelSelectionChangedEventArgs<T>(
                    deselectedIndexes,
                    selectedIndexes,
                    deselectedItems,
                    selectedItems));
        }

        private bool IsValidIndex(int index) => index >= 0 && index < GetCount();

        private int GetCount()
        {
            if (_source is null)
                return 0;

            if (_source is IList list)
                return list.Count;

            if (_source is IReadOnlyList<T> readOnlyList)
                return readOnlyList.Count;

            if (_source is IEnumerable enumerable)
                return enumerable.Cast<object?>().Count();

            return 0;
        }

        private object? GetItemAt(int index)
        {
            if (_source is IList list && index >= 0 && index < list.Count)
                return list[index];

            if (_source is IReadOnlyList<T> readOnlyList && index >= 0 && index < readOnlyList.Count)
                return readOnlyList[index];

            if (_source is IEnumerable enumerable)
                return enumerable.Cast<object?>().ElementAtOrDefault(index);

            return default;
        }

        private int IndexOf(object? item)
        {
            if (_source is IList list)
                return list.IndexOf(item);

            if (_source is IReadOnlyList<T> readOnlyList)
                return FindIndex(readOnlyList, item);

            if (_source is IEnumerable enumerable)
                return FindIndex(enumerable.Cast<object?>().ToList(), item);

            return -1;
        }

        private static int FindIndex(IReadOnlyList<T> items, object? item)
        {
            for (var i = 0; i < items.Count; ++i)
            {
                if (EqualityComparer<object?>.Default.Equals(items[i], item))
                    return i;
            }

            return -1;
        }

        private static int FindIndex(IReadOnlyList<object?> items, object? item)
        {
            for (var i = 0; i < items.Count; ++i)
            {
                if (EqualityComparer<object?>.Default.Equals(items[i], item))
                    return i;
            }

            return -1;
        }

        private (int from, int to) NormalizeRange(int start, int end)
        {
            var count = GetCount();
            if (count == 0)
                return (0, -1);

            var from = Math.Clamp(Math.Min(start, end), 0, count - 1);
            var to = Math.Clamp(Math.Max(start, end), 0, count - 1);
            return (from, to);
        }
    }

    internal sealed class RepeaterSelectionModel : SelectionModel<object?>
    {
        private IList? _writableSelectedItems;
        private int _ignoreModelChanges;
        private bool _ignoreSelectedItemsChanges;

        public RepeaterSelectionModel()
        {
            SelectionChanged += OnSelectionChanged;
            SourceReset += OnSourceReset;
        }

        public IList WritableSelectedItems
        {
            get
            {
                if (_writableSelectedItems is null)
                {
                    _writableSelectedItems = new AvaloniaList<object?>();
                    SubscribeToSelectedItems();
                }

                return _writableSelectedItems;
            }
            set
            {
                value ??= new AvaloniaList<object?>();
                if (value.IsFixedSize)
                {
                    throw new NotSupportedException("Cannot assign fixed size selection to SelectedItems.");
                }

                if (ReferenceEquals(_writableSelectedItems, value))
                    return;

                UnsubscribeFromSelectedItems();
                _writableSelectedItems = value;
                SubscribeToSelectedItems();
                SyncFromSelectedItems();
                RaisePropertyChanged(nameof(WritableSelectedItems));
            }
        }

        private void OnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e)
        {
            if (_ignoreModelChanges > 0)
                return;

            try
            {
                _ignoreSelectedItemsChanges = true;
                var target = WritableSelectedItems;

                foreach (var item in e.DeselectedItems)
                    target.Remove(item);

                foreach (var item in e.SelectedItems)
                {
                    if (!target.Contains(item))
                        target.Add(item);
                }
            }
            finally
            {
                _ignoreSelectedItemsChanges = false;
            }
        }

        private void OnSourceReset(object? sender, EventArgs e) => SyncFromSelectedItems();

        private void SubscribeToSelectedItems()
        {
            if (_writableSelectedItems is INotifyCollectionChanged observable)
                observable.CollectionChanged += OnSelectedItemsCollectionChanged;
        }

        private void UnsubscribeFromSelectedItems()
        {
            if (_writableSelectedItems is INotifyCollectionChanged observable)
                observable.CollectionChanged -= OnSelectedItemsCollectionChanged;
        }

        private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_ignoreSelectedItemsChanges)
                return;

            if (_writableSelectedItems is null)
                throw new InvalidOperationException("CollectionChanged raised but the selected items list is unavailable.");

            void RemoveItems(IList items)
            {
                foreach (var item in items)
                {
                    var index = IndexOfSourceItem(item);
                    if (index >= 0)
                        Deselect(index);
                }
            }

            try
            {
                using var update = this.BatchUpdate();
                ++_ignoreModelChanges;

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        AddItems(e.NewItems);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems is not null)
                            RemoveItems(e.OldItems);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        if (e.OldItems is not null)
                            RemoveItems(e.OldItems);
                        AddItems(e.NewItems);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        Clear();
                        AddItems(_writableSelectedItems);
                        break;

                    case NotifyCollectionChangedAction.Move:
                        break;
                }
            }
            finally
            {
                --_ignoreModelChanges;
            }
        }

        private void SyncFromSelectedItems()
        {
            if (Source is null || _writableSelectedItems is null)
                return;

            try
            {
                ++_ignoreModelChanges;
                using var update = this.BatchUpdate();
                Clear();

                for (var i = 0; i < _writableSelectedItems.Count; ++i)
                {
                    var item = _writableSelectedItems[i];
                    var index = IndexOfSourceItem(item);

                    if (index != -1)
                    {
                        Select(index);
                    }
                    else
                    {
                        try
                        {
                            _ignoreSelectedItemsChanges = true;
                            _writableSelectedItems.RemoveAt(i);
                            --i;
                        }
                        finally
                        {
                            _ignoreSelectedItemsChanges = false;
                        }
                    }
                }
            }
            finally
            {
                --_ignoreModelChanges;
            }
        }

        private void AddItems(IList? items)
        {
            if (items is null)
                return;

            foreach (var item in items)
            {
                var index = IndexOfSourceItem(item);
                if (index >= 0)
                    Select(index);
            }
        }

        private int IndexOfSourceItem(object? item)
        {
            return Source switch
            {
                IList list => list.IndexOf(item),
                IEnumerable enumerable => IndexOfEnumerable(enumerable, item),
                _ => -1,
            };
        }

        private static int IndexOfEnumerable(IEnumerable enumerable, object? item)
        {
            var index = 0;
            foreach (var current in enumerable)
            {
                if (EqualityComparer<object?>.Default.Equals(current, item))
                    return index;

                ++index;
            }

            return -1;
        }
    }
}
