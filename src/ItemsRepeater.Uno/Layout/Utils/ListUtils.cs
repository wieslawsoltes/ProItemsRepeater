using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Layout.Utils
{
    internal static class ListUtils
    {
        public static void Resize<T>(this List<T> list, int size, T value)
        {
            var current = list.Count;

            if (size < current)
            {
                list.RemoveRange(size, current - size);
            }
            else if (size > current)
            {
                if (size > list.Capacity)
                    list.Capacity = size;

                list.AddRange(Enumerable.Repeat(value, size - current));
            }
        }
    }
}
