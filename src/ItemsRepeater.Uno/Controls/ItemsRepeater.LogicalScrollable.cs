using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public partial class ItemsRepeater
    {
        private Size _extent;
        private Size _viewport;
        private Vector _offset;
        private bool _scrollSizeCacheValid;

        internal bool UsesLogicalScrolling => false;

        internal Vector ApplyScrollOffsetShift(Vector shift, bool raiseInvalidated)
        {
            _ = shift;
            _ = raiseInvalidated;
            return default;
        }

        private void SetExtent(Size extent)
        {
            if (_extent == extent)
            {
                return;
            }

            _extent = extent;
            _scrollSizeCacheValid = false;
        }

        private void SetViewport(Size viewport, bool raiseInvalidated, bool invalidateMeasure, bool forceInvalidate = false)
        {
            _ = raiseInvalidated;
            _ = forceInvalidate;

            if (_viewport == viewport)
            {
                return;
            }

            _viewport = viewport;
            _scrollSizeCacheValid = false;
            if (UsesLogicalScrolling || !_viewportManager.HasScroller)
            {
                _viewportManager.UpdateViewportFromLogicalScroll(_viewport, _offset, invalidateMeasure && !_isLayoutInProgress);
            }
        }

        private void UpdateLogicalScrollingState()
        {
        }
    }
}
