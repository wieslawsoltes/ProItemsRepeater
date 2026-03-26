using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ItemsRepeaterUnoSample;

public class SelectingItemsRepeaterPageViewModel : INotifyPropertyChanged
{
    private int _newItemIndex = 1;
    private int _newGenerationIndex;
    private ObservableCollection<SelectingItemsRepeaterPageViewModelItem> _items;

    public SelectingItemsRepeaterPageViewModel()
    {
        _items = CreateItems();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SelectingItemsRepeaterPageViewModelItem> Items
    {
        get => _items;
        set
        {
            if (_items == value)
                return;

            _items = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
        }
    }

    public SelectingItemsRepeaterPageViewModelItem? SelectedItem { get; set; }

    public void AddItem()
    {
        var index = SelectedItem is not null ? Items.IndexOf(SelectedItem) : -1;
        Items.Insert(index + 1, new SelectingItemsRepeaterPageViewModelItem(index + 1, $"New Item {_newItemIndex++}"));
    }

    public void RemoveItem()
    {
        if (SelectedItem is not null)
        {
            Items.Remove(SelectedItem);
            SelectedItem = null;
        }
        else if (Items.Count > 0)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    public void RandomizeHeights()
    {
        var random = new Random();
        foreach (var item in Items)
            item.Height = random.Next(240) + 10;
    }

    public void RandomizeWidths()
    {
        var random = new Random();
        foreach (var item in Items)
            item.Width = random.Next(240) + 10;
    }

    public void ResetItems()
    {
        Items = CreateItems();
    }

    private ObservableCollection<SelectingItemsRepeaterPageViewModelItem> CreateItems()
    {
        var suffix = _newGenerationIndex == 0 ? string.Empty : $"[{_newGenerationIndex}]";
        _newGenerationIndex++;

        return new ObservableCollection<SelectingItemsRepeaterPageViewModelItem>(
            Enumerable.Range(1, 100000).Select(i => new SelectingItemsRepeaterPageViewModelItem(i, $"Item {i} {suffix}")));
    }
}

public class SelectingItemsRepeaterPageViewModelItem : INotifyPropertyChanged
{
    private double _height = double.NaN;
    private double _width = double.NaN;

    public SelectingItemsRepeaterPageViewModelItem(int index, string text)
    {
        Index = index;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; }

    public string Text { get; }

    public double Height
    {
        get => _height;
        set
        {
            if (Math.Abs(_height - value) < 0.01)
                return;

            _height = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            if (Math.Abs(_width - value) < 0.01)
                return;

            _width = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        }
    }
}
