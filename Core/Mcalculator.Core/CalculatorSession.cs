using System.Globalization;

namespace Mcalculator.Core;

/// <summary>Una línea del historial: lo que se escribió y lo que dio.</summary>
public sealed record HistoryEntry(string Expression, double Value, string Display, DateTimeOffset At)
{
    public string When => At.LocalDateTime.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture);
}

/// <summary>
/// Historial de cálculos, memoria de la calculadora y conversión entre bases
/// numéricas. Todo sin estado global, para poder probarlo.
/// </summary>
public sealed class CalculatorSession
{
    private readonly List<HistoryEntry> _history = new();
    private readonly List<string> _undo = new();

    public CalculatorSession(CalculatorEngine? engine = null)
    {
        Engine = engine ?? new CalculatorEngine();
    }

    public CalculatorEngine Engine { get; }

    public IReadOnlyList<HistoryEntry> History => _history;

    /// <summary>Valor guardado con M+ / M-.</summary>
    public double Memory { get; private set; }

    public bool HasMemory { get; private set; }

    /// <summary>Último resultado, para reutilizarlo con Ans.</summary>
    public double LastResult { get; private set; } = double.NaN;

    /// <summary>Evalúa, guarda en el historial y deja el resultado como Ans.</summary>
    public EvalResult Evaluate(string? input, CultureInfo? culture = null)
    {
        var result = Engine.Evaluate(input, culture);
        if (!result.Ok)
            return result;

        LastResult = result.Value;
        Engine.SetVariable("ans", result.Value);

        var expression = (input ?? "").Trim();
        if (expression.Length > 0)
        {
            _history.Insert(0, new HistoryEntry(expression, result.Value, result.Display, DateTimeOffset.Now));
            if (_history.Count > 200)
                _history.RemoveAt(_history.Count - 1);
        }

        return result;
    }

    public void AddHistory(IEnumerable<HistoryEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (_history.Count >= 200) break;
            _history.Add(entry);
        }
    }

    public void ClearHistory() => _history.Clear();

    public void RemoveHistoryEntry(HistoryEntry entry) => _history.Remove(entry);

    // ---------- Memoria ----------

    public void MemoryAdd(double value)
    {
        Memory += value;
        HasMemory = true;
    }

    public void MemorySubtract(double value)
    {
        Memory -= value;
        HasMemory = true;
    }

    public void MemoryStore(double value)
    {
        Memory = value;
        HasMemory = true;
    }

    public void MemoryClear()
    {
        Memory = 0;
        HasMemory = false;
    }

    // ---------- Deshacer del historial de entrada ----------

    public void PushUndo(string expression)
    {
        _undo.Add(expression);
        if (_undo.Count > 50)
            _undo.RemoveAt(0);
    }

    public string? PopUndo() => _undo.Count == 0 ? null : _undo[^1] is var last && RemoveLast() ? last : null;

    private bool RemoveLast()
    {
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    public void ClearUndo() => _undo.Clear();

    // ---------- Bases numéricas ----------

    /// <summary>Convierte un entero a texto en la base pedida (2, 8, 10 o 16).</summary>
    public static string ToBase(double value, int numberBase)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "—";

        var truncated = Math.Truncate(value);
        if (Math.Abs(truncated) > 9.2e18)
            return "demasiado grande";

        var number = (long)truncated;
        return numberBase switch
        {
            2 => Convert.ToString(number, 2),
            8 => Convert.ToString(number, 8),
            16 => Convert.ToString(number, 16).ToUpperInvariant(),
            _ => number.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Lee un número escrito en la base indicada. Devuelve false si no es válido.</summary>
    public static bool TryParseBase(string? text, int numberBase, out double value)
    {
        value = double.NaN;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var clean = text.Trim().Replace(" ", "").Replace("_", "");
        var negative = clean.StartsWith('-');
        if (negative)
            clean = clean[1..];
        if (clean.Length == 0)
            return false;

        try
        {
            long number = 0;
            foreach (var ch in clean)
            {
                var digit = DigitValue(ch);
                if (digit < 0 || digit >= numberBase)
                    return false;
                checked
                {
                    number = number * numberBase + digit;
                }
            }

            value = negative ? -number : number;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static int DigitValue(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'a' and <= 'f' => ch - 'a' + 10,
        >= 'A' and <= 'F' => ch - 'A' + 10,
        _ => -1
    };

    /// <summary>Las cuatro bases a la vez, para el modo programador.</summary>
    public static IReadOnlyList<(string Name, string Value)> AllBases(double value) => new[]
    {
        ("HEX", ToBase(value, 16)),
        ("DEC", ToBase(value, 10)),
        ("OCT", ToBase(value, 8)),
        ("BIN", ToBase(value, 2))
    };
}
