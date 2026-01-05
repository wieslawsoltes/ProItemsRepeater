// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ScrollSnapPointsTests
{
    [AvaloniaFact]
    public void SnapPoints_Regular_Snap_On_ScrollGestureEnd()
    {
        var content = new SnapPointsControl
        {
            AreVerticalSnapPointsRegular = true,
            VerticalRegularSpacing = 50,
            VerticalRegularOffset = 0,
        };

        var target = new ScrollContentPresenter
        {
            CanVerticallyScroll = true,
            VerticalSnapPointsType = SnapPointsType.Mandatory,
            VerticalSnapPointsAlignment = SnapPointsAlignment.Near,
            Content = content,
        };

        target.UpdateChild();
        target.Measure(new Size(100, 100));
        target.Arrange(new Rect(0, 0, 100, 100));

        target.Offset = new Vector(0, 37);
        target.RaiseEvent(new ScrollGestureEndedEventArgs(1));

        Assert.Equal(50, target.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void SnapPoints_Irregular_Snap_On_ScrollGestureEnd()
    {
        var content = new SnapPointsControl
        {
            AreVerticalSnapPointsRegular = false,
            VerticalSnapPoints = new List<double> { 0, 30, 90 },
        };

        var target = new ScrollContentPresenter
        {
            CanVerticallyScroll = true,
            VerticalSnapPointsType = SnapPointsType.Mandatory,
            VerticalSnapPointsAlignment = SnapPointsAlignment.Near,
            Content = content,
        };

        target.UpdateChild();
        target.Measure(new Size(100, 100));
        target.Arrange(new Rect(0, 0, 100, 100));

        target.Offset = new Vector(0, 70);
        target.RaiseEvent(new ScrollGestureEndedEventArgs(1));

        Assert.Equal(90, target.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void SnapPoints_None_Does_Not_Snap()
    {
        var content = new SnapPointsControl
        {
            AreVerticalSnapPointsRegular = true,
            VerticalRegularSpacing = 50,
            VerticalRegularOffset = 0,
        };

        var target = new ScrollContentPresenter
        {
            CanVerticallyScroll = true,
            VerticalSnapPointsType = SnapPointsType.None,
            Content = content,
        };

        target.UpdateChild();
        target.Measure(new Size(100, 100));
        target.Arrange(new Rect(0, 0, 100, 100));

        target.Offset = new Vector(0, 37);
        target.RaiseEvent(new ScrollGestureEndedEventArgs(1));

        Assert.Equal(37, target.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void SnapPoints_Center_Alignment_Accounts_For_Viewport()
    {
        var content = new SnapPointsControl
        {
            AreVerticalSnapPointsRegular = true,
            VerticalRegularSpacing = 50,
            VerticalRegularOffset = 0,
        };

        var target = new ScrollContentPresenter
        {
            CanVerticallyScroll = true,
            VerticalSnapPointsType = SnapPointsType.Mandatory,
            VerticalSnapPointsAlignment = SnapPointsAlignment.Center,
            Content = content,
        };

        target.UpdateChild();
        target.Measure(new Size(100, 100));
        target.Arrange(new Rect(0, 0, 100, 100));

        target.Offset = new Vector(0, 20);
        target.RaiseEvent(new ScrollGestureEndedEventArgs(1));

        Assert.Equal(0, target.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void ScrollViewer_Binds_SnapPoints_To_Presenter()
    {
        ScrollContentPresenter? presenter = null;

        var scrollViewer = new ScrollViewer
        {
            HorizontalSnapPointsType = SnapPointsType.Mandatory,
            VerticalSnapPointsType = SnapPointsType.MandatorySingle,
            HorizontalSnapPointsAlignment = SnapPointsAlignment.Center,
            VerticalSnapPointsAlignment = SnapPointsAlignment.Far,
            Content = new Border { Width = 300, Height = 300 },
            Template = new FuncControlTemplate<ScrollViewer>((parent, scope) =>
            {
                presenter = new ScrollContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    [!ScrollContentPresenter.HorizontalSnapPointsTypeProperty] =
                        new TemplateBinding(ScrollViewer.HorizontalSnapPointsTypeProperty),
                    [!ScrollContentPresenter.VerticalSnapPointsTypeProperty] =
                        new TemplateBinding(ScrollViewer.VerticalSnapPointsTypeProperty),
                    [!ScrollContentPresenter.HorizontalSnapPointsAlignmentProperty] =
                        new TemplateBinding(ScrollViewer.HorizontalSnapPointsAlignmentProperty),
                    [!ScrollContentPresenter.VerticalSnapPointsAlignmentProperty] =
                        new TemplateBinding(ScrollViewer.VerticalSnapPointsAlignmentProperty),
                    [!ScrollContentPresenter.ContentProperty] =
                        new TemplateBinding(ContentControl.ContentProperty),
                };

                return new Panel
                {
                    Children =
                    {
                        presenter.RegisterInNameScope(scope)
                    }
                };
            }),
        };

        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = scrollViewer,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(presenter);
        Assert.Equal(scrollViewer.HorizontalSnapPointsType, presenter!.HorizontalSnapPointsType);
        Assert.Equal(scrollViewer.VerticalSnapPointsType, presenter.VerticalSnapPointsType);
        Assert.Equal(scrollViewer.HorizontalSnapPointsAlignment, presenter.HorizontalSnapPointsAlignment);
        Assert.Equal(scrollViewer.VerticalSnapPointsAlignment, presenter.VerticalSnapPointsAlignment);

        window.Close();
    }

    private sealed class SnapPointsControl : Control, IScrollSnapPointsInfo
    {
        public bool AreHorizontalSnapPointsRegular { get; set; }
        public bool AreVerticalSnapPointsRegular { get; set; }
        public IReadOnlyList<double> HorizontalSnapPoints { get; set; } = Array.Empty<double>();
        public IReadOnlyList<double> VerticalSnapPoints { get; set; } = Array.Empty<double>();
        public double HorizontalRegularSpacing { get; set; }
        public double VerticalRegularSpacing { get; set; }
        public double HorizontalRegularOffset { get; set; }
        public double VerticalRegularOffset { get; set; }

        public event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged;
        public event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged;

        public void RaiseHorizontalSnapPointsChanged()
        {
            HorizontalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
        }

        public void RaiseVerticalSnapPointsChanged()
        {
            VerticalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
        }

        public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
        {
            return orientation == Orientation.Horizontal ? HorizontalSnapPoints : VerticalSnapPoints;
        }

        public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
        {
            if (orientation == Orientation.Horizontal)
            {
                offset = HorizontalRegularOffset;
                return HorizontalRegularSpacing;
            }

            offset = VerticalRegularOffset;
            return VerticalRegularSpacing;
        }

        protected override Size MeasureOverride(Size availableSize) => new(300, 300);
    }
}
