using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Avalonia.Controls.Samples;

public class NestedItemsRepeaterPageViewModel : INotifyPropertyChanged
{
    private ObservableCollection<NestedGroup> _groups;

    public event PropertyChangedEventHandler? PropertyChanged;

    public NestedItemsRepeaterPageViewModel()
    {
        _groups = CreateGroups(groupCount: 50, itemsPerGroup: 200);
    }

    public ObservableCollection<NestedGroup> Groups
    {
        get => _groups;
        set
        {
            if (_groups != value)
            {
                _groups = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Groups)));
            }
        }
    }

    private static ObservableCollection<NestedGroup> CreateGroups(int groupCount, int itemsPerGroup)
    {
        return new ObservableCollection<NestedGroup>(
            Enumerable.Range(1, groupCount)
                .Select(groupIndex => new NestedGroup(
                    groupIndex,
                    new ObservableCollection<NestedItem>(
                        Enumerable.Range(1, itemsPerGroup)
                            .Select(itemIndex => new NestedItem(groupIndex, itemIndex))))));
    }
}

public sealed class NestedGroup
{
    public NestedGroup(int index, ObservableCollection<NestedItem> items)
    {
        Index = index;
        Items = items;
    }

    public int Index { get; }
    public string Title => $"Group {Index}";
    public ObservableCollection<NestedItem> Items { get; }
}

public sealed class NestedItem
{
    public NestedItem(int groupIndex, int index)
    {
        GroupIndex = groupIndex;
        Index = index;
    }

    public int GroupIndex { get; }
    public int Index { get; }
    public string Label => $"Item {GroupIndex}.{Index}";
}
