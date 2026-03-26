using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ItemsRepeaterUnoSample;

public class RepeaterDataGridPageViewModel : INotifyPropertyChanged
{
    private readonly Random _random = new(1);
    private ObservableCollection<RepeaterDataGridItem> _items;

    public RepeaterDataGridPageViewModel()
    {
        _items = CreateItems();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepeaterDataGridItem> Items
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

    public void RandomizeHeights()
    {
        foreach (var item in Items)
            item.Height = _random.Next(22, 60);
    }

    private ObservableCollection<RepeaterDataGridItem> CreateItems()
    {
        var categories = new[] { "Hardware", "Software", "Services", "Support" };
        return new ObservableCollection<RepeaterDataGridItem>(
            Enumerable.Range(1, 100000).Select(i => new RepeaterDataGridItem
            {
                Id = i,
                Name = $"Item {i}",
                Category = categories[i % categories.Length],
                Price = Math.Round(_random.NextDouble() * 5000, 2).ToString("0.00"),
                Stock = (i * 3) % 120,
                Height = 28,
            }));
    }
}

public class RepeaterDataGridItem : INotifyPropertyChanged
{
    private int _id;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _price = string.Empty;
    private int _stock;
    private double _height;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id
    {
        get => _id;
        set
        {
            if (_id == value)
                return;

            _id = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            if (_category == value)
                return;

            _category = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
        }
    }

    public string Price
    {
        get => _price;
        set
        {
            if (_price == value)
                return;

            _price = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
        }
    }

    public int Stock
    {
        get => _stock;
        set
        {
            if (_stock == value)
                return;

            _stock = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stock)));
        }
    }

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
}
