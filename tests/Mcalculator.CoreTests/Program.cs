using System.Globalization;
using Mcalculator.Core;

namespace Mcalculator.CoreTests;

/// <summary>
/// Pruebas del núcleo de Mcalculator: normalización de la entrada, motor de
/// expresiones y conversión de unidades. Consola que devuelve 0 si todo pasa.
/// </summary>
internal static class Program
{
    private static int _checks;
    private static int _failures;
    // Se construye a mano: en este equipo .NET puede servir la cultura en modo
    // invariante (ICU desactivado) y es-ES acabaría con "." como separador.
    private static readonly CultureInfo Spanish = CreateSpanish();
    private static readonly CultureInfo English = new("en-US");

    private static CultureInfo CreateSpanish()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }

    private static int Main()
    {
        NormalizerTests();
        EngineTests();
        AngleTests();
        VariableTests();
        UnitTests();
        FormatTests();

        Console.WriteLine();
        Console.WriteLine($"{_checks} comprobaciones, {_failures} fallos.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check(string name, bool condition)
    {
        _checks++;
        if (condition)
        {
            Console.WriteLine($"  OK    {name}");
        }
        else
        {
            _failures++;
            Console.WriteLine($"  FALLO {name}");
        }
    }

    private static void CheckValue(string name, EvalResult result, double expected, double tolerance = 1e-9)
    {
        _checks++;
        if (!double.IsNaN(expected) && result.Ok && Math.Abs(result.Value - expected) <= tolerance)
        {
            Console.WriteLine($"  OK    {name} = {result.Display}");
        }
        else
        {
            _failures++;
            Console.WriteLine($"  FALLO {name}: esperado {expected}, obtenido {(result.Ok ? result.Value.ToString(CultureInfo.InvariantCulture) : "ERROR " + result.Error)}");
        }
    }

    private static void NormalizerTests()
    {
        Console.WriteLine("Normalización de la entrada");

        Check("× se convierte en *", ExpressionNormalizer.Normalize("2×3", Spanish) == "2*3");
        Check("÷ se convierte en /", ExpressionNormalizer.Normalize("6÷2", Spanish) == "6/2");
        Check("π se convierte en pi", ExpressionNormalizer.Normalize("π", Spanish) == "pi");
        Check("menos tipográfico", ExpressionNormalizer.Normalize("5\u22123", Spanish) == "5-3");
        Check("coma decimal en español", ExpressionNormalizer.Normalize("2,5+1", Spanish) == "2.5+1");
        Check("punto decimal en inglés", ExpressionNormalizer.Normalize("2.5+1", English) == "2.5+1");
        Check("separador de miles se descarta", ExpressionNormalizer.Normalize("1.234,5", Spanish) == "1234.5");
        Check("superíndice cuadrado", ExpressionNormalizer.Normalize("5²", Spanish) == "(5)**2");
        Check("superíndice cúbico sobre paréntesis", ExpressionNormalizer.Normalize("(2+1)³", Spanish) == "((2+1))**3");
        Check("superíndice sobre nombre", ExpressionNormalizer.Normalize("pi²", Spanish) == "(pi)**2");
        Check("espacios sobrantes", ExpressionNormalizer.Normalize("  1 + 2  ", Spanish) == "1 + 2");
        Check("cadena vacía", ExpressionNormalizer.Normalize("   ", Spanish) == "");
    }

    private static void EngineTests()
    {
        Console.WriteLine("Motor de expresiones");
        var engine = new CalculatorEngine();
        CheckValue("2+3*4", engine.Evaluate("2+3*4", Spanish), 14);
        CheckValue("prioridad con paréntesis", engine.Evaluate("(2+3)*4", Spanish), 20);
        CheckValue("potencia con ^", engine.Evaluate("2^10", Spanish), 1024);
        CheckValue("potencia con **", engine.Evaluate("2**8", Spanish), 256);
        CheckValue("potencia negativa", engine.Evaluate("2^-2", Spanish), 0.25);
        CheckValue("raíz cuadrada", engine.Evaluate("sqrt(16)", Spanish), 4);
        CheckValue("raíz cúbica", engine.Evaluate("cbrt(27)", Spanish), 3);
        CheckValue("logaritmo natural", engine.Evaluate("ln(e)", Spanish), 1);
        CheckValue("logaritmo decimal", engine.Evaluate("log(1000)", Spanish), 3);
        CheckValue("valor absoluto", engine.Evaluate("abs(-7)", Spanish), 7);
        CheckValue("factorial", engine.Evaluate("fact(5)", Spanish), 120);
        CheckValue("redondeo", engine.Evaluate("round(2.5)", Spanish), 3);
        CheckValue("suelo", engine.Evaluate("floor(2.9)", Spanish), 2);
        CheckValue("techo", engine.Evaluate("ceiling(2.1)", Spanish), 3);
        CheckValue("mínimo", engine.Evaluate("min(3, 8)", Spanish), 3);
        CheckValue("máximo", engine.Evaluate("max(3, 8)", Spanish), 8);
        CheckValue("mcd", engine.Evaluate("gcd(48, 18)", Spanish), 6);
        CheckValue("mcm", engine.Evaluate("lcm(4, 6)", Spanish), 12);
        CheckValue("signo", engine.Evaluate("sign(-3)", Spanish), -1);
        CheckValue("constante pi", engine.Evaluate("pi", Spanish), Math.PI);
        CheckValue("constante e", engine.Evaluate("e", Spanish), Math.E);
        CheckValue("tau", engine.Evaluate("tau", Spanish), Math.Tau);
        CheckValue("número áureo", engine.Evaluate("phi", Spanish), (1 + Math.Sqrt(5)) / 2);
        CheckValue("módulo", engine.Evaluate("10 % 3", Spanish), 1);
        CheckValue("división decimal", engine.Evaluate("1/3", Spanish), 1.0 / 3);
        CheckValue("entrada con coma", engine.Evaluate("2,5 * 4", Spanish), 10);
        CheckValue("entrada con ×", engine.Evaluate("6×7", Spanish), 42);
        CheckValue("entrada con π", engine.Evaluate("π*2", Spanish), Math.PI * 2);
        CheckValue("superíndice", engine.Evaluate("5²", Spanish), 25);
        CheckValue("1e3", engine.Evaluate("1e3", Spanish), 1000);

        Check("expresión vacía da error", !engine.Evaluate("", Spanish).Ok);
        Check("nombre desconocido da error", !engine.Evaluate("foo(2)", Spanish).Ok);
        Check("paréntesis sin cerrar da error", !engine.Evaluate("(2+3", Spanish).Ok);
        Check("error de sintaxis no rompe", !engine.Evaluate("2++", Spanish).Ok);
        Check("división por cero da infinito o error",
            engine.Evaluate("1/0", Spanish) is { } r && (!r.Ok || double.IsInfinity(r.Value)));

        var factorialNegativo = engine.Evaluate("fact(-2)", Spanish);
        Check("factorial negativo se rechaza", !factorialNegativo.Ok);
    }

    private static void AngleTests()
    {
        Console.WriteLine("Ángulos");

        var degrees = new CalculatorEngine { AngleMode = AngleMode.Degrees };
        CheckValue("sin(30) en grados", degrees.Evaluate("sin(30)", Spanish), 0.5);
        CheckValue("cos(60) en grados", degrees.Evaluate("cos(60)", Spanish), 0.5);
        CheckValue("tan(45) en grados", degrees.Evaluate("tan(45)", Spanish), 1);
        CheckValue("asin(0.5) en grados", degrees.Evaluate("asin(0.5)", Spanish), 30);

        var radians = new CalculatorEngine { AngleMode = AngleMode.Radians };
        CheckValue("sin(pi/2) en radianes", radians.Evaluate("sin(pi/2)", Spanish), 1);
        CheckValue("acos(-1) en radianes", radians.Evaluate("acos(-1)", Spanish), Math.PI);
    }

    private static void VariableTests()
    {
        Console.WriteLine("Variables");

        var engine = new CalculatorEngine();
        var result = engine.Evaluate("x = 5", Spanish);
        Check("asignación devuelve el valor", result.Ok && Math.Abs(result.Value - 5) < 1e-9);
        Check("la variable queda guardada", engine.Variables.ContainsKey("x"));
        CheckValue("se usa en otra expresión", engine.Evaluate("x * 2", Spanish), 10);
        CheckValue("se puede redefinir", engine.Evaluate("x = x + 1", Spanish), 6);
        CheckValue("varias variables", engine.Evaluate("y = x * 3", Spanish), 18);

        Check("nombre inválido se rechaza", !engine.Evaluate("2x = 5", Spanish).Ok);
        Check("no se puede redefinir pi", !engine.Evaluate("pi = 3", Spanish).Ok);

        engine.RemoveVariable("x");
        Check("quitar variable", !engine.Variables.ContainsKey("x"));

        engine.ClearVariables();
        Check("limpiar variables", engine.Variables.Count == 0);
    }

    private static void UnitTests()
    {
        Console.WriteLine("Conversión de unidades");

        Check("1 km a m", Math.Abs(UnitConverter.Convert(1, "longitud", "km", "m") - 1000) < 1e-9);
        Check("1 mi a km", Math.Abs(UnitConverter.Convert(1, "longitud", "mi", "km") - 1.609344) < 1e-9);
        Check("100 cm a in", Math.Abs(UnitConverter.Convert(100, "longitud", "cm", "in") - 39.3700787402) < 1e-6);
        Check("1 kg a lb", Math.Abs(UnitConverter.Convert(1, "masa", "kg", "lb") - 2.2046226218) < 1e-6);
        Check("0 C a F", Math.Abs(UnitConverter.Convert(0, "temperatura", "C", "F") - 32) < 1e-9);
        Check("100 C a F", Math.Abs(UnitConverter.Convert(100, "temperatura", "C", "F") - 212) < 1e-9);
        Check("32 F a C", Math.Abs(UnitConverter.Convert(32, "temperatura", "F", "C")) < 1e-9);
        Check("0 C a K", Math.Abs(UnitConverter.Convert(0, "temperatura", "C", "K") - 273.15) < 1e-9);
        Check("1 GB a MB", Math.Abs(UnitConverter.Convert(1, "datos", "GB", "MB") - 1000) < 1e-9);
        Check("1 GiB a MiB", Math.Abs(UnitConverter.Convert(1, "datos", "GiB", "MiB") - 1024) < 1e-9);
        Check("1 h a min", Math.Abs(UnitConverter.Convert(1, "tiempo", "h", "min") - 60) < 1e-9);
        Check("1 kWh a J", Math.Abs(UnitConverter.Convert(1, "energia", "kWh", "J") - 3600000) < 1e-6);
        Check("1 ha a m2", Math.Abs(UnitConverter.Convert(1, "area", "ha", "m2") - 10000) < 1e-9);
        Check("categoría desconocida lanza", Throws(() => UnitConverter.Convert(1, "nada", "a", "b")));
        Check("unidad desconocida lanza", Throws(() => UnitConverter.Convert(1, "longitud", "xx", "m")));
        Check("hay 9 categorías", UnitConverter.Categories.Count == 9);
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void FormatTests()
    {
        Console.WriteLine("Formato del resultado");

        Check("entero", ExpressionNormalizer.FormatResult(42, Spanish) == "42");
        Check("decimal en español", ExpressionNormalizer.FormatResult(2.5, Spanish) == "2,5");
        Check("decimal en inglés", ExpressionNormalizer.FormatResult(2.5, English) == "2.5");
        Check("cero", ExpressionNormalizer.FormatResult(0, Spanish) == "0");
        Check("negativo", ExpressionNormalizer.FormatResult(-3.25, Spanish) == "-3,25");
        Check("cero negativo se limpia", ExpressionNormalizer.FormatResult(-0.0, Spanish) == "0");
        Check("infinito", ExpressionNormalizer.FormatResult(double.PositiveInfinity, Spanish) == "∞");
        Check("NaN", ExpressionNormalizer.FormatResult(double.NaN, Spanish) == "No es un número");
        Check("números grandes en notación científica",
            ExpressionNormalizer.FormatResult(1e20, Spanish).Contains('E'));
        Check("sin ceros de relleno", ExpressionNormalizer.FormatResult(1.5000, Spanish) == "1,5");
    }
}
