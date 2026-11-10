using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Mcalculator.Core;

namespace Mcalculator.App;

/// <summary>
/// Dibuja la gráfica de una o varias funciones sobre un Canvas. Sin librerías de
/// terceros: líneas, ejes, rejilla y etiquetas a mano.
/// </summary>
public partial class GraphView : UserControl
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(255, 138, 43),
        Color.FromRgb(107, 182, 255),
        Color.FromRgb(46, 204, 113),
        Color.FromRgb(192, 132, 252),
        Color.FromRgb(240, 180, 41)
    };

    private readonly GraphModel _model = new();
    private readonly List<string> _functions = new();
    private Point _dragStart;
    private GraphRange _xAtDragStart;
    private GraphRange _yAtDragStart;
    private bool _dragging;

    public GraphView()
    {
        InitializeComponent();
    }

    public GraphModel Model => _model;

    public IReadOnlyList<string> Functions => _functions;

    /// <summary>Sustituye las funciones dibujadas y repinta.</summary>
    public void SetFunctions(IEnumerable<string> expressions)
    {
        _functions.Clear();
        _functions.AddRange(expressions.Where(e => !string.IsNullOrWhiteSpace(e)));
        Render();
    }

    public void Zoom(double factor)
    {
        _model.XRange = _model.XRange.Zoom(factor);
        _model.YRange = _model.YRange.Zoom(factor);
        Render();
    }

    public void ResetView()
    {
        _model.Reset();
        Render();
    }

    public void FitY()
    {
        if (_functions.Count == 0)
            return;
        _model.FitY(_model.Sample(_functions[0]));
        Render();
    }

    // ---------- Dibujo ----------

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => Render();

    public void Render()
    {
        Surface.Children.Clear();

        var width = Surface.ActualWidth;
        var height = Surface.ActualHeight;
        if (width < 10 || height < 10)
            return;

        var xRange = _model.XRange;
        var yRange = _model.YRange;

        double ToPixelX(double x) => (x - xRange.Min) / xRange.Span * width;
        double ToPixelY(double y) => height - (y - yRange.Min) / yRange.Span * height;

        // Rejilla
        var gridBrush = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
        foreach (var tick in GraphModel.NiceTicks(xRange, 12))
        {
            var x = ToPixelX(tick);
            Surface.Children.Add(Line(x, 0, x, height, gridBrush, 1));
        }
        foreach (var tick in GraphModel.NiceTicks(yRange, 8))
        {
            var y = ToPixelY(tick);
            Surface.Children.Add(Line(0, y, width, y, gridBrush, 1));
        }

        // Ejes
        var axisBrush = new SolidColorBrush(Color.FromArgb(90, 230, 237, 247));
        var originY = Math.Clamp(ToPixelY(0), 0, height);
        var originX = Math.Clamp(ToPixelX(0), 0, width);
        Surface.Children.Add(Line(0, originY, width, originY, axisBrush, 1.4));
        Surface.Children.Add(Line(originX, 0, originX, height, axisBrush, 1.4));

        // Etiquetas de los ejes
        foreach (var tick in GraphModel.NiceTicks(xRange, 12))
        {
            if (Math.Abs(tick) < 1e-12) continue;
            var label = Label(GraphModel.TickLabel(tick), 10);
            Canvas.SetLeft(label, ToPixelX(tick) + 3);
            Canvas.SetTop(label, Math.Min(height - 16, originY + 3));
            Surface.Children.Add(label);
        }
        foreach (var tick in GraphModel.NiceTicks(yRange, 8))
        {
            if (Math.Abs(tick) < 1e-12) continue;
            var label = Label(GraphModel.TickLabel(tick), 10);
            Canvas.SetLeft(label, Math.Min(width - 34, originX + 4));
            Canvas.SetTop(label, ToPixelY(tick) - 14);
            Surface.Children.Add(label);
        }

        // Funciones
        for (var index = 0; index < _functions.Count; index++)
        {
            var points = _model.Sample(_functions[index], 900);
            var color = Palette[index % Palette.Length];
            var brush = new SolidColorBrush(color);

            Polyline? current = null;
            foreach (var point in points)
            {
                if (double.IsNaN(point.Y) || double.IsInfinity(point.Y) ||
                    point.Y > yRange.Max + yRange.Span || point.Y < yRange.Min - yRange.Span)
                {
                    // Corte: el siguiente punto empieza una polilínea nueva.
                    current = null;
                    continue;
                }

                var px = ToPixelX(point.X);
                var py = ToPixelY(point.Y);

                if (current is null)
                {
                    current = new Polyline
                    {
                        Stroke = brush,
                        StrokeThickness = 2,
                        StrokeLineJoin = PenLineJoin.Round
                    };
                    Surface.Children.Add(current);
                }

                current.Points.Add(new Point(px, py));
            }
        }

        // Leyenda
        for (var index = 0; index < _functions.Count; index++)
        {
            var color = Palette[index % Palette.Length];
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
            legend.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, Fill = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
            });
            legend.Children.Add(Label("y = " + _functions[index], 11));
            Canvas.SetLeft(legend, 10 + index * 130);
            Canvas.SetTop(legend, 8);
            Surface.Children.Add(legend);
        }
    }

    private static Line Line(double x1, double y1, double x2, double y2, Brush brush, double thickness) =>
        new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness };

    private static TextBlock Label(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = new SolidColorBrush(Color.FromRgb(139, 152, 173)),
        IsHitTestVisible = false
    };

    // ---------- Interacción: arrastrar y zoom ----------

    private void Surface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStart = e.GetPosition(Surface);
        _xAtDragStart = _model.XRange;
        _yAtDragStart = _model.YRange;
        Surface.CaptureMouse();
    }

    private void Surface_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Surface.ReleaseMouseCapture();
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Surface.ActualWidth < 10)
            return;

        var position = e.GetPosition(Surface);
        var dx = (position.X - _dragStart.X) / Surface.ActualWidth * _xAtDragStart.Span;
        var dy = (position.Y - _dragStart.Y) / Surface.ActualHeight * _yAtDragStart.Span;

        _model.XRange = _xAtDragStart.Pan(-dx);
        _model.YRange = _yAtDragStart.Pan(dy);
        Render();
    }

    private void Surface_Wheel(object sender, MouseWheelEventArgs e)
    {
        Zoom(e.Delta > 0 ? 0.85 : 1.18);
        e.Handled = true;
    }
}
