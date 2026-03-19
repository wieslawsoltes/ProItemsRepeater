using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Data;

namespace Avalonia.Controls.Utils
{
    /// <summary>
    /// Helper class for evaluating a binding from an item and binding instance.
    /// </summary>
    internal sealed class RepeaterBindingEvaluator<T> : StyledElement, IDisposable
    {
        private IDisposable? _expression;
        private BindingBase? _lastBinding;

        [SuppressMessage(
            "AvaloniaProperty",
            "AVP1002:AvaloniaProperty objects should not be owned by a generic type",
            Justification = "This property is not supposed to be used from XAML.")]
        public static readonly StyledProperty<T> ValueProperty =
            AvaloniaProperty.Register<RepeaterBindingEvaluator<T>, T>("Value");

        /// <summary>
        /// Gets or sets the data item value.
        /// </summary>
        public T Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public T Evaluate(object? dataContext)
        {
            if (!Equals(dataContext, DataContext))
                DataContext = dataContext;

            return GetValue(ValueProperty);
        }

        public void UpdateBinding(BindingBase binding)
        {
            if (binding == _lastBinding)
                return;

            _expression?.Dispose();
            _expression = null;
            _expression = this.Bind(ValueProperty, binding);
            _lastBinding = binding;
        }

        public void ClearDataContext()
            => DataContext = null;

        public void Dispose()
        {
            _expression?.Dispose();
            _expression = null;
            _lastBinding = null;
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
