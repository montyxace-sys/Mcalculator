using System.Globalization;

namespace Mcalculator.Core;

/// <summary>
/// Convierte entre unidades: longitud, masa, temperatura, área, volumen, tiempo,
/// velocidad, energía y datos. Todo con factores relativos a una unidad base,
/// salvo la temperatura, que necesita fórmulas propias.
/// </summary>
public static class UnitConverter
{
    public sealed record Unit(string Key, string Name, double Factor, string Symbol);

    public sealed record Category(string Key, string Name, IReadOnlyList<Unit> Units);

    private static Unit U(string key, string name, double factor, string symbol = "") =>
        new(key, name, factor, symbol.Length == 0 ? key : symbol);

    public static IReadOnlyList<Category> Categories { get; } = new[]
    {
        new Category("longitud", "Longitud", new[]
        {
            U("mm", "Milímetro", 0.001), U("cm", "Centímetro", 0.01),
            U("m", "Metro", 1), U("km", "Kilómetro", 1000),
            U("in", "Pulgada", 0.0254), U("ft", "Pie", 0.3048),
            U("yd", "Yarda", 0.9144), U("mi", "Milla", 1609.344),
            U("nmi", "Milla náutica", 1852)
        }),
        new Category("masa", "Masa", new[]
        {
            U("mg", "Miligramo", 0.000001), U("g", "Gramo", 0.001),
            U("kg", "Kilogramo", 1), U("t", "Tonelada", 1000),
            U("oz", "Onza", 0.028349523125), U("lb", "Libra", 0.45359237),
            U("st", "Stone", 6.35029318)
        }),
        new Category("temperatura", "Temperatura", new[]
        {
            U("C", "Celsius", 1), U("F", "Fahrenheit", 1),
            U("K", "Kelvin", 1), U("R", "Rankine", 1)
        }),
        new Category("area", "Área", new[]
        {
            U("mm2", "Milímetro cuadrado", 0.000001), U("cm2", "Centímetro cuadrado", 0.0001),
            U("m2", "Metro cuadrado", 1), U("ha", "Hectárea", 10000),
            U("km2", "Kilómetro cuadrado", 1000000), U("in2", "Pulgada cuadrada", 0.00064516),
            U("ft2", "Pie cuadrado", 0.09290304), U("ac", "Acre", 4046.8564224)
        }),
        new Category("volumen", "Volumen", new[]
        {
            U("ml", "Mililitro", 0.001), U("l", "Litro", 1),
            U("m3", "Metro cúbico", 1000), U("gal", "Galón (US)", 3.785411784),
            U("qt", "Cuarto (US)", 0.946352946), U("pt", "Pinta (US)", 0.473176473),
            U("floz", "Onza líquida (US)", 0.0295735295625), U("cup", "Taza (US)", 0.2365882365)
        }),
        new Category("tiempo", "Tiempo", new[]
        {
            U("ms", "Milisegundo", 0.001), U("s", "Segundo", 1),
            U("min", "Minuto", 60), U("h", "Hora", 3600),
            U("d", "Día", 86400), U("sem", "Semana", 604800),
            U("mes", "Mes (30 d)", 2592000), U("a", "Año (365 d)", 31536000)
        }),
        new Category("velocidad", "Velocidad", new[]
        {
            U("m/s", "Metro por segundo", 1), U("km/h", "Kilómetro por hora", 0.277777777777778),
            U("mph", "Milla por hora", 0.44704), U("kn", "Nudo", 0.514444444444444)
        }),
        new Category("energia", "Energía", new[]
        {
            U("J", "Julio", 1), U("kJ", "Kilojulio", 1000),
            U("cal", "Caloría", 4.184), U("kcal", "Kilocaloría", 4184),
            U("Wh", "Vatio hora", 3600), U("kWh", "Kilovatio hora", 3600000)
        }),
        new Category("datos", "Datos", new[]
        {
            U("B", "Byte", 1), U("KB", "Kilobyte", 1000),
            U("MB", "Megabyte", 1000000), U("GB", "Gigabyte", 1000000000),
            U("TB", "Terabyte", 1000000000000), U("KiB", "Kibibyte", 1024),
            U("MiB", "Mebibyte", 1048576), U("GiB", "Gibibyte", 1073741824)
        })
    };

    public static Category? FindCategory(string key) =>
        Categories.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Convierte un valor entre dos unidades de la misma categoría.</summary>
    public static double Convert(double value, string categoryKey, string fromKey, string toKey)
    {
        var category = FindCategory(categoryKey)
            ?? throw new ArgumentException($"No conozco la categoría «{categoryKey}».");

        if (category.Key == "temperatura")
            return ConvertTemperature(value, fromKey, toKey);

        var from = category.Units.FirstOrDefault(u => u.Key.Equals(fromKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No conozco la unidad «{fromKey}».");
        var to = category.Units.FirstOrDefault(u => u.Key.Equals(toKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No conozco la unidad «{toKey}».");

        return value * from.Factor / to.Factor;
    }

    /// <summary>Pasa la temperatura a Celsius, aplica la conversión y la saca de Celsius.</summary>
    public static double ConvertTemperature(double value, string from, string to)
    {
        var celsius = from.ToUpperInvariant() switch
        {
            "C" => value,
            "F" => (value - 32) * 5 / 9,
            "K" => value - 273.15,
            "R" => (value - 491.67) * 5 / 9,
            _ => throw new ArgumentException($"No conozco la unidad «{from}».")
        };

        return to.ToUpperInvariant() switch
        {
            "C" => celsius,
            "F" => celsius * 9 / 5 + 32,
            "K" => celsius + 273.15,
            "R" => (celsius + 273.15) * 9 / 5,
            _ => throw new ArgumentException($"No conozco la unidad «{to}».")
        };
    }

    public static string Format(double value, CultureInfo? culture = null) =>
        ExpressionNormalizer.FormatResult(value, culture);
}
