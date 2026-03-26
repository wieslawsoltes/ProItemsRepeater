using System;
using Avalonia.Controls.DataGrid;

namespace Avalonia.Data
{
    public enum BindingMode
    {
        Default,
        OneWay,
        TwoWay,
        OneTime,
        OneWayToSource,
    }

    public enum BindingPriority
    {
        LocalValue = 0,
        Style = 1,
    }

    public abstract class BindingBase
    {
        internal object? Evaluate(object? dataContext) => EvaluateCore(dataContext);

        protected abstract object? EvaluateCore(object? dataContext);
    }

    public sealed class Binding : BindingBase
    {
        public Binding()
        {
        }

        public Binding(string? path)
        {
            Path = path;
        }

        public string? Path { get; set; }

        protected override object? EvaluateCore(object? dataContext)
        {
            if (dataContext is null || string.IsNullOrWhiteSpace(Path))
                return dataContext;

            return RepeaterDataGridBindingHelper.GetPropertyValue(dataContext, Path);
        }
    }
}
