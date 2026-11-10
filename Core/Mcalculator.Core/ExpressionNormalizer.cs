using System.Globalization;
using System.Text;

namespace Mcalculator.Core;

/// <summary>
/// Convierte lo que el usuario teclea en una expresión que el motor entiende:
/// símbolos bonitos (×, ÷, √, π, ², ³), separadores de miles y el formato de
/// números de la cultura activa. También va en la otra dirección: de número a
/// texto que se pueda volver a teclear.
/// </summary>
public static class ExpressionNormalizer
{
    /// <summary>
    /// Reescribe la entrada para el motor. No evalúa nada, así que se puede probar
    /// con cadenas sin depender de NCalc.
    /// </summary>
    public static string Normalize(string? input, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        culture ??= CultureInfo.CurrentCulture;
        var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
        var groupSeparator = culture.NumberFormat.NumberGroupSeparator;

        var text = input.Trim();

        // Símbolos tipográficos que se pegan o se escriben con el teclado.
        text = text
            .Replace('\u00A0', ' ')          // espacio duro
            .Replace('\u2212', '-')          // menos tipográfico
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('×', '*')
            .Replace('\u00B7', '*')
            .Replace('÷', '/')
            .Replace('\u2215', '/')
            .Replace("π", "pi")
            .Replace("Π", "pi")
            .Replace("√", "sqrt")
            .Replace("∞", "inf");

        // Potencias escritas con superíndices: x² -> (x)^2, x³ -> (x)^3.
        text = ExpandSuperscripts(text);

        var sb = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            // El separador de miles ("." en español) se descarta cuando va entre un
            // dígito y tres dígitos ("1.234,5" -> "1234,5"), pero NO cuando es el punto
            // decimal de "2.5": ahí el grupo que sigue no tiene tres cifras.
            if (groupSeparator.Length == 1 && ch == groupSeparator[0] &&
                decimalSeparator != groupSeparator &&
                i > 0 && i + 1 < text.Length &&
                char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]) &&
                CountDigitsAfter(text, i + 1) == 3)
            {
                continue;
            }

            // Separador decimal de la cultura -> punto, que es lo que espera el motor.
            // Solo dentro de un número: si viene tras una coma o un paréntesis, es un
            // separador de argumentos (min(3, 8)) y debe quedarse como coma.
            if (decimalSeparator.Length == 1 && ch == decimalSeparator[0] && decimalSeparator != ".")
            {
                var previous = i > 0 ? text[i - 1] : ' ';
                var next = i + 1 < text.Length ? text[i + 1] : ' ';
                var isDecimal = char.IsDigit(previous) && char.IsDigit(next);
                sb.Append(isDecimal ? '.' : ch);
                continue;
            }

            sb.Append(ch);
        }

        return PowerToDoubleStar(sb.ToString().Trim());
    }

    /// <summary>
    /// NCalc 7 interpreta «^» como XOR a nivel de bits, así que la potencia se escribe
    /// «**». Aquí se convierte la notación de todo el mundo (2^10) a la del motor.
    /// El «^» dentro de una expresión también se usa a veces como XOR: no se puede
    /// distinguir, y para una calculadora la potencia es lo que se espera.
    /// </summary>
    public static string PowerToDoubleStar(string text)
    {
        if (!text.Contains('^'))
            return text;

        var sb = new StringBuilder(text.Length + 4);
        foreach (var ch in text)
        {
            if (ch == '^')
                sb.Append("**");
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static int CountDigitsAfter(string text, int start)
    {
        var count = 0;
        for (var i = start; i < text.Length && char.IsDigit(text[i]); i++)
            count++;
        return count;
    }

    /// <summary>
    /// Convierte "5²" en "(5)^2" y "2³" en "(2)^3", agrupando el operando anterior
    /// completo cuando hace falta.
    /// </summary>
    public static string ExpandSuperscripts(string text)
    {
        if (text.IndexOfAny(new[] { '²', '³', '\u2074', '\u2075' }) < 0)
            return text;

        var sb = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var power = ch switch
            {
                '²' => "2",
                '³' => "3",
                '\u2074' => "4",
                '\u2075' => "5",
                _ => null
            };

            if (power is null)
            {
                sb.Append(ch);
                continue;
            }

            // Se busca el operando que hay justo antes del superíndice.
            TakeLastOperand(sb);
            // "**" en vez de "^": en NCalc 7 es el operador de potencia.
            sb.Append("**").Append(power);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Quita del final del acumulador el último operando (un número, un paréntesis
    /// equilibrado o un nombre) y lo devuelve, dejando en su lugar "(operando)".
    /// </summary>
    private static string TakeLastOperand(StringBuilder sb)
    {
        if (sb.Length == 0)
            return "";

        var end = sb.Length - 1;

        // Número (con decimales).
        if (char.IsDigit(sb[end]) || sb[end] == '.')
        {
            var start = end;
            while (start > 0 && (char.IsDigit(sb[start - 1]) || sb[start - 1] == '.'))
                start--;
            return Wrap(sb, start, end + 1);
        }

        // Paréntesis equilibrado.
        if (sb[end] == ')')
        {
            var depth = 0;
            var start = end;
            for (; start >= 0; start--)
            {
                if (sb[start] == ')') depth++;
                else if (sb[start] == '(')
                {
                    depth--;
                    if (depth == 0) break;
                }
            }
            if (start >= 0)
                return Wrap(sb, start, end + 1);
        }

        // Nombre de función o constante.
        if (char.IsLetter(sb[end]))
        {
            var start = end;
            while (start > 0 && char.IsLetter(sb[start - 1]))
                start--;
            return Wrap(sb, start, end + 1);
        }

        return "";
    }

    private static string Wrap(StringBuilder sb, int start, int endExclusive)
    {
        var text = sb.ToString(start, endExclusive - start);
        sb.Remove(start, endExclusive - start);
        sb.Append('(').Append(text).Append(')');
        return text;
    }

    /// <summary>
    /// Da formato a un resultado para mostrarlo: hasta 12 cifras significativas, sin
    /// ceros de relleno y con el separador decimal de la cultura activa.
    /// </summary>
    public static string FormatResult(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (double.IsNaN(value))
            return "No es un número";
        if (double.IsPositiveInfinity(value))
            return "∞";
        if (double.IsNegativeInfinity(value))
            return "-∞";

        if (value == 0)
            return "0";

        var abs = Math.Abs(value);

        // Números muy grandes o muy pequeños: notación científica.
        if (abs >= 1e15 || abs < 1e-9)
        {
            var scientific = value.ToString("0.##########E+0", CultureInfo.InvariantCulture);
            return scientific.Replace('.', culture.NumberFormat.NumberDecimalSeparator[0]);
        }

        var rounded = Math.Round(value, 12);
        var text = rounded.ToString("0.###########", CultureInfo.InvariantCulture);

        // Se quita el signo menos de los ceros negativos que salen de redondeos.
        if (text == "-0")
            text = "0";

        return text.Replace('.', culture.NumberFormat.NumberDecimalSeparator[0]);
    }
}
