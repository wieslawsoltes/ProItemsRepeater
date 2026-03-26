using System;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls.Templates
{
    public interface IDataTemplate
    {
        UIElement? Build(object? data);
        bool Match(object? data);
    }

    public sealed class FuncDataTemplate : IDataTemplate
    {
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
