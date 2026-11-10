using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcalculator.Core;

/// <summary>
/// Guarda historial y preferencias en %AppData%\Mcalculator. Un fallo al escribir
/// nunca puede tumbar la aplicación: el historial en memoria manda.
/// </summary>
public sealed class CalculatorStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<string> _folder;

    public CalculatorStore() : this(() => DefaultFolder) { }

    /// <summary>Permite apuntar a una carpeta temporal en las pruebas.</summary>
    public CalculatorStore(Func<string> folderProvider)
    {
        _folder = folderProvider;
    }

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mcalculator");

    private string SettingsFile => Path.Combine(_folder(), "settings.json");
    private string HistoryFile => Path.Combine(_folder(), "history.json");

    public AppPreferences LoadPreferences()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(SettingsFile), Options)
                       ?? new AppPreferences();
        }
        catch (Exception)
        {
        }
        return new AppPreferences();
    }

    public void SavePreferences(AppPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(_folder());
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(preferences, Options));
        }
        catch (Exception)
        {
        }
    }

    public List<HistoryEntry> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryFile))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryFile), Options)
                       ?? new List<HistoryEntry>();
        }
        catch (Exception)
        {
        }
        return new List<HistoryEntry>();
    }

    public void SaveHistory(IEnumerable<HistoryEntry> history)
    {
        try
        {
            Directory.CreateDirectory(_folder());
            File.WriteAllText(HistoryFile, JsonSerializer.Serialize(history.Take(200).ToList(), Options));
        }
        catch (Exception)
        {
        }
    }
}

/// <summary>Lo que se recuerda entre sesiones.</summary>
public sealed class AppPreferences
{
    public AngleMode AngleMode { get; set; } = AngleMode.Degrees;
    public bool KeepHistory { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
    public bool SoundEnabled { get; set; }
    public bool ShowThousands { get; set; }
    public int HistoryLimit { get; set; } = 200;
    public double WindowWidth { get; set; } = 720;
    public double WindowHeight { get; set; } = 880;
}
