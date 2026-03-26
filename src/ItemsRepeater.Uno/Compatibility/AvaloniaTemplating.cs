using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Avalonia.Controls.Templates
{
    public interface IDataTemplate
    {
        UIElement? Build(object? data);
        bool Match(object? data);
    }

    public sealed class FuncDataTemplate : IDataTemplate
    {
        public static FuncDataTemplate Default { get; } = new(
            static data =>
            {
                if (data is UIElement element)
                {
                    return element;
                }

                return new TextBlock
                {
                    Text = data?.ToString() ?? string.Empty,
                };
            });

        private readonly Func<object?, UIElement?> _build;
        private readonly Func<object?, bool> _match;

        public FuncDataTemplate(Func<object?, UIElement?> build, Func<object?, bool>? match = null)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _match = match ?? (_ => true);
        }

        public UIElement? Build(object? data) => _build(data);

        public bool Match(object? data) => _match(data);
    }
}
