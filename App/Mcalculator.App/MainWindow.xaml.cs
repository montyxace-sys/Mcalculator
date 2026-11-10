using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mcalculator.Core;

namespace Mcalculator.App;

public partial class MainWindow : Window
{
    private readonly CalculatorStore _store = new();
    private readonly CalculatorSession _session = new();
    private AppPreferences _prefs = new();
    private string _expression = "";
    private bool _justEvaluated;

    public MainWindow()
    {
        InitializeComponent();
        _prefs = _store.LoadPreferences();
        _session.Engine.AngleMode = _prefs.AngleMode;

        _session.AddHistory(_store.LoadHistory());

        Loaded += OnLoaded;
        PreviewKeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Width = Math.Max(MinWidth, _prefs.WindowWidth);
        Height = Math.Max(MinHeight, _prefs.WindowHeight);
        Topmost = _prefs.AlwaysOnTop;
        UpdateAngleButton();
        RefreshHistory();
        RefreshAll();
        ExpressionBox.Focus();
    }

    // ---------- Entrada ----------

    private void Insert(string text)
    {
        if (_justEvaluated)
        {
            // Tras "=", escribir un número empieza de cero; un operador continúa con Ans.
            var continues = text.Length > 0 && "+-*/^%)".Contains(text[0]);
            if (!continues)
                _expression = "";
            _justEvaluated = false;
        }

        _session.PushUndo(_expression);
        _expression += text;
        RefreshAll();
    }

    private void Clear()
    {
        _session.PushUndo(_expression);
        _expression = "";
        _justEvaluated = false;
        RefreshAll();
    }

    private void Backspace()
    {
        if (_expression.Length == 0)
            return;
        _session.PushUndo(_expression);
        _expression = _expression[..^1];
        RefreshAll();
    }

    private void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(_expression))
            return;

        var result = _session.Evaluate(_expression);
        if (result.Ok)
        {
            ExpressionBox.Text = _expression;
            ResultBox.Text = result.Display;
            _expression = result.Display;
            _justEvaluated = true;
            PreviewBox.Text = "";
            ShowError("");
            RefreshHistory();
            RefreshAll(skipExpression: true);
            SaveHistory();
        }
        else
        {
            ShowError(result.Error ?? "No se pudo calcular.");
        }
    }

    // ---------- Teclado ----------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key;
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (key)
        {
            case Key.Enter:
            case Key.OemPlus when shift:
                Evaluate();
                e.Handled = true;
                return;
            case Key.Escape:
                Clear();
                e.Handled = true;
                return;
            case Key.Back:
                Backspace();
                e.Handled = true;
                return;
            case Key.Delete:
                Clear();
                e.Handled = true;
                return;
        }

        // Ctrl+Z deshace, Ctrl+C copia el resultado.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (key == Key.Z)
            {
                var previous = _session.PopUndo();
                if (previous is not null)
                {
                    _expression = previous;
                    RefreshAll();
                }
                e.Handled = true;
            }
            else if (key == Key.C)
            {
                CopyResult();
                e.Handled = true;
            }
            return;
        }

        var character = KeyToCharacter(key, shift);
        if (character is null)
            return;

        Insert(character);
        e.Handled = true;
    }

    private static string? KeyToCharacter(Key key, bool shift) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        Key.Add => "+",
        Key.Subtract => "-",
        Key.Multiply => "*",
        Key.Divide => "/",
        Key.Decimal => ".",
        Key.OemPlus => shift ? "+" : "=",
        Key.OemMinus => "-",
        Key.OemPeriod or Key.OemComma => ",",
        Key.Oem2 => "/",
        Key.OemOpenBrackets => "(",
        Key.OemCloseBrackets => ")",
        Key.Oem5 or Key.OemPipe => shift ? "|" : "\\",
        _ => null
    };

    // ---------- Teclas de la interfaz ----------

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string text })
            Insert(text);
    }

    private void Function_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string template })
            Insert(template);
    }

    private void Operator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string op })
            return;

        // Un operador detrás de otro sustituye al anterior.
        if (_expression.Length > 0 && "+-*/^".Contains(_expression[^1]) && op.Length == 1)
            _expression = _expression[..^1];

        Insert(op);
    }

    private void Equals_Click(object sender, RoutedEventArgs e) => Evaluate();

    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();

    private void Backspace_Click(object sender, RoutedEventArgs e) => Backspace();

    private void Angle_Click(object sender, RoutedEventArgs e)
    {
        _session.Engine.AngleMode = _session.Engine.AngleMode == AngleMode.Degrees
            ? AngleMode.Radians
            : AngleMode.Degrees;
        _prefs.AngleMode = _session.Engine.AngleMode;
        _store.SavePreferences(_prefs);
        UpdateAngleButton();
        RefreshAll();
    }

    private void UpdateAngleButton()
    {
        var degrees = _session.Engine.AngleMode == AngleMode.Degrees;
        AngleButton.Content = degrees ? "DEG" : "RAD";
        AngleButton.Foreground = degrees
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("GreenBrush");
    }

    // ---------- Memoria ----------

    private void Memory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action })
            return;

        var value = _session.LastResult;
        switch (action)
        {
            case "MC":
                _session.MemoryClear();
                break;
            case "MR":
                if (_session.HasMemory)
                    Insert(_session.Memory.ToString(System.Globalization.CultureInfo.CurrentCulture));
                break;
            case "M+":
                if (!double.IsNaN(value)) _session.MemoryAdd(value);
                break;
            case "M-":
                if (!double.IsNaN(value)) _session.MemorySubtract(value);
                break;
            case "MS":
                if (!double.IsNaN(value)) _session.MemoryStore(value);
                break;
        }

        MemoryIndicator.Visibility = _session.HasMemory ? Visibility.Visible : Visibility.Collapsed;
        MemoryIndicator.Text = _session.HasMemory
            ? "M " + ExpressionNormalizer.FormatResult(_session.Memory)
            : "";
    }

    // ---------- Refresco de la pantalla ----------

    private void RefreshAll(bool skipExpression = false)
    {
        if (!skipExpression)
            ExpressionBox.Text = _expression;

        var preview = _session.Engine.EvaluateCore(_expression);
        if (_expression.Length == 0)
        {
            PreviewBox.Text = "";
            if (!_justEvaluated) ResultBox.Text = "0";
        }
        else if (preview.Ok && !_justEvaluated)
        {
            PreviewBox.Text = "= " + preview.Display;
        }
        else if (!preview.Ok)
        {
            PreviewBox.Text = "";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CopyResult()
    {
        try
        {
            Clipboard.SetText(string.IsNullOrWhiteSpace(ResultBox.Text) ? _expression : ResultBox.Text);
        }
        catch (Exception)
        {
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => CopyResult();

    // ---------- Historial ----------

    private void RefreshHistory()
    {
        HistoryList.Items.Clear();
        foreach (var entry in _session.History.Take(100))
        {
            var expression = new TextBlock
            {
                Text = entry.Expression,
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var value = new TextBlock
            {
                Text = "= " + entry.Display,
                FontSize = 15,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            var stack = new StackPanel();
            stack.Children.Add(expression);
            stack.Children.Add(value);

            HistoryList.Items.Add(new ListBoxItem
            {
                Tag = entry,
                Content = stack,
                Style = (Style)FindResource("ListRow")
            });
        }

        HistoryEmpty.Visibility = _session.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void History_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is ListBoxItem { Tag: HistoryEntry entry })
        {
            _expression = entry.Display;
            _justEvaluated = false;
            ShowError("");
            RefreshAll();
            ExpressionBox.Focus();
        }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _session.ClearHistory();
        RefreshHistory();
        SaveHistory();
    }

    private void SaveHistory()
    {
        if (_prefs.KeepHistory)
            _store.SaveHistory(_session.History);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Graph_Expanded(object sender, RoutedEventArgs e)
    {
        if (Height < 1080)
            Height = 1080;
        Graph_Draw(sender, e);
    }

    private void Graph_Draw(object sender, RoutedEventArgs e)
    {
        var text = (GraphInput.Text ?? "").Trim();
        if (text.Length == 0)
            return;

        // Varias funciones separadas por ";".
        var expressions = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        GraphCanvas.SetFunctions(expressions);
    }

    private void Graph_Fit(object sender, RoutedEventArgs e) => GraphCanvas.FitY();

    private void Graph_Reset(object sender, RoutedEventArgs e) => GraphCanvas.ResetView();

    private void Extras_Expanded(object sender, RoutedEventArgs e)
    {
        // Al desplegar los paneles la ventana crece un poco para que se vean enteros.
        if (Height < 980)
            Height = 980;
    }
}
