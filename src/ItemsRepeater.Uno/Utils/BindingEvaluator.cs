using System;
using Avalonia.Data;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls.Utils
{
    /// <summary>
    /// Helper class for evaluating a binding from an item and binding instance.
    /// </summary>
    internal sealed class RepeaterBindingEvaluator<T> : FrameworkElement, IDisposable
    {
        private BindingBase? _lastBinding;
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(object),
                typeof(RepeaterBindingEvaluator<T>),
                new PropertyMetadata(default(object)));

        /// <summary>
        /// Gets or sets the data item value.
        /// </summary>
        public T Value
        {
            get => GetValue(ValueProperty) is T value ? value : default!;
            set => SetValue(ValueProperty, value);
        }

        public T Evaluate(object? dataContext)
        {
            if (!Equals(dataContext, DataContext))
                DataContext = dataContext;

            return GetValue(ValueProperty) is T value ? value : default!;
        }

        public void UpdateBinding(BindingBase binding)
        {
            if (binding == _lastBinding)
                return;

            ClearValue(ValueProperty);
            SetBinding(ValueProperty, binding.CreateNativeBinding());
            _lastBinding = binding;
        }

        public void ClearDataContext()
            => DataContext = null;

        public new void Dispose()
        {
            _lastBinding = null;
            ClearValue(ValueProperty);
            DataContext = null;
        }

        public static RepeaterBindingEvaluator<T>? TryCreate(BindingBase? binding)
        {
            if (binding is null)
                return null;

            var evaluator = new RepeaterBindingEvaluator<T>();
            evaluator.UpdateBinding(binding);
            return evaluator;
        }
    }
}
