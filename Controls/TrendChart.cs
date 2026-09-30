using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using BatteryDoctor.Models;

using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

// File responsibility: Lightweight WPF chart renderer used for battery history/live trends without a third-party chart dependency.

namespace BatteryDoctor.Controls;

/// <summary>
/// Small dependency-free WPF time-series chart.
/// </summary>
public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IEnumerable),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(WpfBrush),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(WpfBrushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit),
        typeof(string),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));


    /// <summary>
    /// Rewires collection-change notifications when the chart data source changes, then invalidates the visual for repaint.
    /// </summary>
    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TrendChart)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= chart.OnPointsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += chart.OnPointsCollectionChanged;
        chart.InvalidateVisual();
    }

    /// <summary>
    /// Requests a repaint whenever an observable source collection is modified.
    /// </summary>
    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    public IEnumerable? Points
    {
        get => (IEnumerable?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public WpfBrush Stroke
    {
        get => (WpfBrush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>
    /// Normalizes valid trend points into chart coordinates, draws grid/axes labels, and renders the time-series polyline.
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 20 || height <= 20) return;

        var bg = new SolidColorBrush(WpfColor.FromRgb(248, 250, 252));
        var grid = new WpfPen(new SolidColorBrush(WpfColor.FromRgb(226, 232, 240)), 1);
        var textBrush = new SolidColorBrush(WpfColor.FromRgb(100, 116, 139));
        dc.DrawRoundedRectangle(bg, null, new WpfRect(0, 0, width, height), 10, 10);

        const double left = 48;
        const double right = 12;
        const double top = 14;
        const double bottom = 28;
        var plot = new WpfRect(left, top, Math.Max(1, width - left - right), Math.Max(1, height - top - bottom));

        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Top + plot.Height * i / 4.0;
            dc.DrawLine(grid, new WpfPoint(plot.Left, y), new WpfPoint(plot.Right, y));
        }

        var data = new List<TrendPoint>();
        if (Points is not null)
        {
            foreach (var item in Points)
                if (item is TrendPoint point && !double.IsNaN(point.Value) && !double.IsInfinity(point.Value))
                    data.Add(point);
        }

        if (data.Count == 0)
        {
            DrawText(dc, "—", textBrush, 16, new WpfPoint(width / 2 - 5, height / 2 - 10));
            return;
        }

        data.Sort((a, b) => a.CapturedAt.CompareTo(b.CapturedAt));
        var min = data.Min(x => x.Value);
        var max = data.Max(x => x.Value);
        if (Math.Abs(max - min) < 0.01)
        {
            min -= 1;
            max += 1;
        }
        else
        {
            var pad = (max - min) * 0.12;
            min -= pad;
            max += pad;
        }

        for (var i = 0; i <= 4; i++)
        {
            var value = max - (max - min) * i / 4.0;
            var y = plot.Top + plot.Height * i / 4.0 - 7;
            DrawText(dc, $"{value:0.#}{Unit}", textBrush, 10, new WpfPoint(4, y));
        }

        var start = data[0].CapturedAt;
        var end = data[^1].CapturedAt;
        var spanTicks = Math.Max(1L, (end - start).Ticks);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < data.Count; i++)
            {
                var p = data[i];
                var x = plot.Left + plot.Width * (p.CapturedAt - start).Ticks / spanTicks;
                var y = plot.Bottom - plot.Height * (p.Value - min) / (max - min);
                if (i == 0) ctx.BeginFigure(new WpfPoint(x, y), false, false);
                else ctx.LineTo(new WpfPoint(x, y), true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new WpfPen(Stroke, 2), geometry);

        DrawText(dc, start.LocalDateTime.ToString("g"), textBrush, 9, new WpfPoint(plot.Left, plot.Bottom + 6));
        var endText = end.LocalDateTime.ToString("g");
        var ft = MakeText(endText, textBrush, 9);
        dc.DrawText(ft, new WpfPoint(Math.Max(plot.Left, plot.Right - ft.Width), plot.Bottom + 6));
    }

    /// <summary>
    /// Draws one formatted text label at a WPF point using the current display DPI.
    /// </summary>
    private void DrawText(DrawingContext dc, string text, WpfBrush brush, double size, WpfPoint point)
        => dc.DrawText(MakeText(text, brush, size), point);

    /// <summary>
    /// Creates DPI-aware FormattedText using the current UI culture and left-to-right flow.
    /// </summary>
    private FormattedText MakeText(string text, WpfBrush brush, double size)
        => new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
