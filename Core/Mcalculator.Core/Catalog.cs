namespace Mcalculator.Core;

/// <summary>Una función disponible en las expresiones, con su nombre para la interfaz.</summary>
public sealed record FunctionInfo(string Name, string Signature, string Description);

/// <summary>Una constante disponible en las expresiones.</summary>
public sealed record ConstantInfo(string Name, string Description);

/// <summary>Una variable definida por el usuario, con su valor ya evaluado.</summary>
public sealed record VariableInfo(string Name, double Value);

/// <summary>Resultado de evaluar una expresión.</summary>
public sealed record EvalResult(bool Ok, double Value, string Display, string? Error)
{
    public static EvalResult Success(double value, string display) => new(true, value, display, null);

    public static EvalResult Failure(string error) => new(false, double.NaN, "", error);
}
