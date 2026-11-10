using System.Globalization;

namespace Mcalculator.Core;

/// <summary>Un punto de la gráfica. Y puede ser NaN si la función no está definida ahí.</summary>
public readonly record struct GraphPoint(double X, double Y);

/// <summary>Un rango del eje X o Y.</summary>
public readonly record struct GraphRange(double Min, double Max)
{
    public double Span => Max - Min;
    public bool Contains(double value) => value >= Min && value <= Max;

    public GraphRange Zoom(double factor)
    {
        var center = (Min + Max) / 2;
        var half = Span * factor / 2;
        return new GraphRange(center - half, center + half);
    }

    public GraphRange Pan(double delta) => new(Min + delta, Max + delta);
}

/// <summary>
/// Evalúa una o varias funciones sobre un rango y devuelve los puntos que se pueden
/// dibujar. No dibuja nada: así se puede probar sin ventana.
/// </summary>
public sealed class GraphModel
{
    /// <summary>Límite de puntos por función: más no se aprecia y cuesta CPU.</summary>
    public const int MaxSamples = 2000;

    private readonly CalculatorEngine _engine;

    public GraphModel(CalculatorEngine? engine = null)
    {
        _engine = engine ?? new CalculatorEngine();
    }

    public GraphRange XRange { get; set; } = new(-10, 10);

    public GraphRange YRange { get; set; } = new(-6.5, 6.5);

    /// <summary>
    /// Muestrea "f(x)" con la variable "x" en el rango actual. Devuelve puntos
    /// ordenados; los valores no definidos (raíces negativas, asíntotas) salen como NaN
    /// para que el dibujo corte la línea en lugar de unir dos ramas.
    /// </summary>
    public IReadOnlyList<GraphPoint> Sample(string expression, int samples = 800)
    {
        samples = Math.Clamp(samples, 2, MaxSamples);
        var points = new List<GraphPoint>(samples + 1);
        var step = XRange.Span / samples;

        for (var i = 0; i <= samples; i++)
        {
            var x = XRange.Min + step * i;
            var y = EvaluateAt(expression, x);
            points.Add(new GraphPoint(x, y));
        }

        return points;
    }

    /// <summary>Valor de la función en un x concreto, con la variable x definida.</summary>
    public double EvaluateAt(string expression, double x)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return double.NaN;

        // Igual que la calculadora: "^" es potencia, no XOR.
        var normalized = ExpressionNormalizer.Normalize(expression);
        var previous = _engine.Variables.TryGetValue("x", out var old) ? old : (double?)null;
        _engine.SetVariable("x", x);
        try
        {
            var result = _engine.EvaluateCore(normalized);
            return result.Ok ? result.Value : double.NaN;
        }
        catch (Exception)
        {
            return double.NaN;
        }
        finally
        {
            if (previous is { } value)
                _engine.SetVariable("x", value);
            else
                _engine.RemoveVariable("x");
        }
    }

    /// <summary>Separa el rango visible en saltos "redondos" para dibujar la rejilla.</summary>
    public static IReadOnlyList<double> NiceTicks(GraphRange range, int targetTicks = 10)
    {
        var span = range.Span;
        if (span <= 0 || double.IsNaN(span) || double.IsInfinity(span))
            return Array.Empty<double>();

        var rawStep = span / Math.Max(1, targetTicks);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;

        var step = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        } * magnitude;

        var start = Math.Ceiling(range.Min / step) * step;
        var ticks = new List<double>();
        for (var value = start; value <= range.Max + step * 0.001; value += step)
        {
            // Se redondea para que 0.30000000000004 no aparezca como etiqueta.
            ticks.Add(Math.Round(value, 10));
            if (ticks.Count > 200)
                break;
        }

        return ticks;
    }

    /// <summary>Etiqueta corta de un tick, sin ceros de relleno.</summary>
    public static string TickLabel(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var text = Math.Round(value, 10).ToString("0.######", CultureInfo.InvariantCulture);
        if (text == "-0")
            text = "0";
        return text.Replace('.', culture.NumberFormat.NumberDecimalSeparator[0]);
    }

    /// <summary>True si la variable «x» se quedó definida tras muestrear (no debería).</summary>
    public bool OrphanCheck() => _engine.Variables.ContainsKey("x");

    public void Reset()
    {
        XRange = new GraphRange(-10, 10);
        YRange = new GraphRange(-6.5, 6.5);
    }

    /// <summary>Ajusta el eje Y a los datos, dejando un margen del 10 %.</summary>
    public void FitY(IEnumerable<GraphPoint> points)
    {
        var values = points.Select(p => p.Y).Where(y => !double.IsNaN(y) && !double.IsInfinity(y)).ToList();
        if (values.Count == 0)
            return;

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= 1;
            max += 1;
        }

        var margin = (max - min) * 0.1;
        YRange = new GraphRange(min - margin, max + margin);
    }
}
