namespace Avalonia.Layout
{
    /// <summary>
    /// Represents the base class for an object that facilitates communication between an attached
    /// layout and its host container.
    /// </summary>
    public class LayoutContext : AvaloniaObject
    {
        /// <summary>
        /// Gets or sets an object that represents the state of a layout.
        /// </summary>
        public object? LayoutState
        {
            get => LayoutStateCore;
            set => LayoutStateCore = value;
        }

        /// <summary>
        /// Implements the behavior of <see cref="LayoutState"/> in a derived or custom
        /// <see cref="LayoutContext"/>.
        /// </summary>
        protected virtual object? LayoutStateCore { get; set; }
    }
}
