using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Avalonia.Collections
{
    public class AvaloniaList<T> : ObservableCollection<T>
    {
        public AvaloniaList()
        {
        }

        public AvaloniaList(IEnumerable<T> items)
            : base(items)
        {
        }
    }
}
