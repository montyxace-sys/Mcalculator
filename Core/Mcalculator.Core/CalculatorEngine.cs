using System.Globalization;
using NCalc;

namespace Mcalculator.Core;

/// <summary>
/// El evaluador de expresiones. Entiende matemáticas de verdad: prioridad de
/// operadores, funciones, constantes, variables definidas por el usuario y
/// ángulos en grados o radianes.
/// </summary>
public sealed class CalculatorEngine
{
    private readonly Dictionary<string, double> _variables = new(StringComparer.OrdinalIgnoreCase);

    public CalculatorEngine()
    {
        AngleMode = AngleMode.Degrees;
    }

    public AngleMode AngleMode { get; set; }

    public IReadOnlyDictionary<string, double> Variables => _variables;

    /// <summary>Funciones disponibles, para el catálogo de la interfaz.</summary>
    public static IReadOnlyList<FunctionInfo> Functions { get; } = new[]
    {
        new FunctionInfo("sqrt", "sqrt(x)", "Raíz cuadrada"),
        new FunctionInfo("cbrt", "cbrt(x)", "Raíz cúbica"),
        new FunctionInfo("abs", "abs(x)", "Valor absoluto"),
        new FunctionInfo("exp", "exp(x)", "e elevado a x"),
        new FunctionInfo("ln", "ln(x)", "Logaritmo natural"),
        new FunctionInfo("log", "log(x)", "Logaritmo en base 10"),
        new FunctionInfo("log2", "log2(x)", "Logaritmo en base 2"),
        new FunctionInfo("sin", "sin(x)", "Seno"),
        new FunctionInfo("cos", "cos(x)", "Coseno"),
        new FunctionInfo("tan", "tan(x)", "Tangente"),
        new FunctionInfo("asin", "asin(x)", "Arcoseno"),
        new FunctionInfo("acos", "acos(x)", "Arcocoseno"),
        new FunctionInfo("atan", "atan(x)", "Arcotangente"),
        new FunctionInfo("sinh", "sinh(x)", "Seno hiperbólico"),
        new FunctionInfo("cosh", "cosh(x)", "Coseno hiperbólico"),
        new FunctionInfo("tanh", "tanh(x)", "Tangente hiperbólica"),
        new FunctionInfo("fact", "fact(n)", "Factorial"),
        new FunctionInfo("round", "round(x)", "Redondeo al entero más cercano"),
        new FunctionInfo("floor", "floor(x)", "Entero por defecto"),
        new FunctionInfo("ceiling", "ceiling(x)", "Entero por exceso"),
        new FunctionInfo("min", "min(a, b)", "El menor"),
        new FunctionInfo("max", "max(a, b)", "El mayor"),
        new FunctionInfo("gcd", "gcd(a, b)", "Máximo común divisor"),
        new FunctionInfo("lcm", "lcm(a, b)", "Mínimo común múltiplo"),
        new FunctionInfo("sign", "sign(x)", "Signo: -1, 0 o 1")
    };

    public static IReadOnlyList<ConstantInfo> Constants { get; } = new[]
    {
        new ConstantInfo("pi", "3,14159265358979…"),
        new ConstantInfo("e", "2,71828182845904…"),
        new ConstantInfo("tau", "2π"),
        new ConstantInfo("phi", "Número áureo"),
        new ConstantInfo("inf", "Infinito")
    };

    /// <summary>Evalúa una expresión tal cual la escribió el usuario.</summary>
    public EvalResult Evaluate(string? input, CultureInfo? culture = null)
    {
        var normalized = ExpressionNormalizer.Normalize(input, culture);
        if (normalized.Length == 0)
            return EvalResult.Failure("Escribe una expresión.");

        // Una asignación define una variable y devuelve su valor.
        var assignment = SplitAssignment(normalized);
        if (assignment is not null)
        {
            var (name, expression) = assignment.Value;
            if (!IsValidVariableName(name))
                return EvalResult.Failure($"«{name}» no es un nombre de variable válido.");

            var computed = EvaluateCore(expression, culture);
            if (!computed.Ok)
                return computed;

            _variables[name] = computed.Value;
            return computed;
        }

        return EvaluateCore(normalized, culture);
    }

    /// <summary>Evalúa sin tocar las variables (para la vista previa en vivo).</summary>
    public EvalResult EvaluateCore(string expression, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        try
        {
            // La expresión ya viene normalizada a punto decimal (ExpressionNormalizer),
            // así que el motor se evalúa siempre con la cultura invariante. Sin esto,
            // NCalc parsea «2.5» con la cultura del sistema y en español lee 25.
            var source = new Expression(expression, ExpressionOptions.NoCache)
            {
                CultureInfo = CultureInfo.InvariantCulture
            };

            RegisterFunctions(source);
            RegisterConstants(source);



            foreach (var (variable, stored) in _variables)
                source.Parameters[variable] = stored;

            var raw = source.Evaluate();
            var value = ToDouble(raw);

            if (double.IsNaN(value))
                return EvalResult.Failure("El resultado no es un número.");

            return EvalResult.Success(value, ExpressionNormalizer.FormatResult(value, culture));
        }
        catch (Exception ex)
        {
            return EvalResult.Failure(FriendlyError(ex));
        }
    }

    public bool RemoveVariable(string name) => _variables.Remove(name);

    public void ClearVariables() => _variables.Clear();

    private void RegisterFunctions(Expression expression)
    {
        // En NCalc 7 una función recibe FunctionData y evalúa sus argumentos con
        // Evaluate(i); no hay un object[] de parámetros.
        // NCalc busca el nombre tal cual lo escribió el usuario: se registra el
        // mismo nombre en minúscula y la interfaz siempre inserta minúsculas.
        expression.Functions["sqrt"] = d => Math.Sqrt(Num(d, 0));
        expression.Functions["cbrt"] = d => Math.Cbrt(Num(d, 0));
        expression.Functions["abs"] = d => Math.Abs(Num(d, 0));
        expression.Functions["exp"] = d => Math.Exp(Num(d, 0));
        expression.Functions["ln"] = d => Math.Log(Num(d, 0));
        expression.Functions["log"] = d => Math.Log10(Num(d, 0));
        expression.Functions["log2"] = d => Math.Log2(Num(d, 0));

        expression.Functions["sin"] = d => Trig(Num(d, 0), Math.Sin);
        expression.Functions["cos"] = d => Trig(Num(d, 0), Math.Cos);
        expression.Functions["tan"] = d => Trig(Num(d, 0), Math.Tan);
        expression.Functions["asin"] = d => InverseTrig(Num(d, 0), Math.Asin);
        expression.Functions["acos"] = d => InverseTrig(Num(d, 0), Math.Acos);
        expression.Functions["atan"] = d => InverseTrig(Num(d, 0), Math.Atan);

        expression.Functions["sinh"] = d => Math.Sinh(Num(d, 0));
        expression.Functions["cosh"] = d => Math.Cosh(Num(d, 0));
        expression.Functions["tanh"] = d => Math.Tanh(Num(d, 0));

        expression.Functions["fact"] = d => Factorial(Num(d, 0));
        expression.Functions["floor"] = d => Math.Floor(Num(d, 0));
        expression.Functions["ceiling"] = d => Math.Ceiling(Num(d, 0));

        expression.Functions["min"] = d => { var a = Args(d); return Math.Min(a[0], a[1]); };
        expression.Functions["max"] = d => { var a = Args(d); return Math.Max(a[0], a[1]); };
        expression.Functions["gcd"] = d => { var a = Args(d); return Gcd(a[0], a[1]); };
        expression.Functions["lcm"] = d => { var a = Args(d); return Lcm(a[0], a[1]); };
        expression.Functions["sign"] = d => Math.Sign(Num(d, 0));

        // "round" de NCalc exige dos argumentos; el nuestro redondea con uno solo.
        expression.Functions["round"] = d => Math.Round(Num(d, 0), MidpointRounding.AwayFromZero);
    }

    private void RegisterConstants(Expression expression)
    {
        expression.Parameters["pi"] = Math.PI;
        expression.Parameters["e"] = Math.E;
        expression.Parameters["tau"] = Math.Tau;
        expression.Parameters["phi"] = (1 + Math.Sqrt(5)) / 2;
        expression.Parameters["inf"] = double.PositiveInfinity;
    }

    private double Trig(double value, Func<double, double> fn) =>
        fn(AngleMode == AngleMode.Degrees ? value * Math.PI / 180 : value);

    private double InverseTrig(double value, Func<double, double> fn)
    {
        var result = fn(value);
        return AngleMode == AngleMode.Degrees ? result * 180 / Math.PI : result;
    }

    /// <summary>Evalúa el argumento número <paramref name="index"/> de una función.</summary>
    private static double Num(NCalc.Handlers.FunctionData data, int index)
    {
        if (data.Count <= index)
            throw new ArgumentException("Faltan argumentos en la función.");
        return ToDouble(data.Evaluate(index));
    }

    /// <summary>
    /// Evalúa todos los argumentos de una función. Se recorren uno a uno porque, con
    /// varias funciones propias en la misma expresión, NCalc 7 reutiliza el contexto y
    /// un handler aislado solo ve el primer argumento.
    /// </summary>
    private static double[] Args(NCalc.Handlers.FunctionData data)
    {
        var count = data.Count;
        var values = new double[count];
        for (var i = 0; i < count; i++)
            values[i] = ToDouble(data.Evaluate(i));
        return values;
    }

    private static double ToDouble(object? value) => value switch
    {
        null => double.NaN,
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"«{s}» no es un número."),
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };

    private static double Factorial(double value)
    {
        if (value < 0 || value != Math.Floor(value))
            throw new ArgumentException("El factorial solo se calcula con enteros no negativos.");
        if (value > 170)
            throw new ArgumentException("El factorial es demasiado grande.");

        var result = 1d;
        for (var i = 2; i <= (int)value; i++)
            result *= i;
        return result;
    }

    private static double Gcd(double a, double b)
    {
        var x = (long)Math.Abs(Math.Round(a));
        var y = (long)Math.Abs(Math.Round(b));
        while (y != 0)
        {
            (x, y) = (y, x % y);
        }
        return x;
    }

    private static double Lcm(double a, double b)
    {
        var gcd = Gcd(a, b);
        return gcd == 0 ? 0 : Math.Abs(a * b) / gcd;
    }

    /// <summary>Separa "nombre = expresión" en sus dos partes.</summary>
    private static (string Name, string Expression)? SplitAssignment(string text)
    {
        var index = text.IndexOf('=');
        if (index <= 0)
            return null;

        // "==" no es una asignación.
        if (index + 1 < text.Length && text[index + 1] == '=')
            return null;

        var name = text[..index].Trim();
        var rest = text[(index + 1)..].Trim();
        return rest.Length == 0 ? null : (name, rest);
    }

    private static bool IsValidVariableName(string name)
    {
        if (name.Length == 0 || !char.IsLetter(name[0]))
            return false;
        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                return false;
        }

        return name.ToLowerInvariant() is not ("pi" or "e" or "tau" or "phi" or "inf");
    }

    private static string FriendlyError(Exception ex)
    {
        var message = ex.Message.Trim();

        if (message.Contains("Missing", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("parameter", StringComparison.OrdinalIgnoreCase))
            return "Hay un nombre que no conozco. Revisa la expresión.";

        if (message.Contains("Unexpected", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unexpected", StringComparison.OrdinalIgnoreCase))
            return "La expresión tiene algo mal escrito.";

        return string.IsNullOrWhiteSpace(message) ? "No se pudo calcular." : message;
    }
}

public enum AngleMode
{
    Degrees,
    Radians
}
