using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Mcalculator.Core;

namespace Mcalculator.App;

/// <summary>
/// Los dos paneles extra: conversión de unidades y modo programador. Comparten
/// ventana con la calculadora para no abrir otra aplicación.
/// </summary>
public partial class Panels : UserControl
{
    private bool _updating;

    public Panels()
    {
        InitializeComponent();
        LoadCategories();
        LoadBases();
    }

    // ---------- Unidades ----------

    private void LoadCategories()
    {
        _updating = true;
        foreach (var category in UnitConverter.Categories)
            CategoryBox.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.Key });
        CategoryBox.SelectedIndex = 0;
        _updating = false;
        LoadUnits();
    }

    private void LoadUnits()
    {
        if (CategoryBox.SelectedItem is not ComboBoxItem { Tag: string key })
            return;

        var category = UnitConverter.FindCategory(key);
        if (category is null)
            return;

        _updating = true;
        FromBox.Items.Clear();
        ToBox.Items.Clear();
        foreach (var unit in category.Units)
        {
            FromBox.Items.Add(new ComboBoxItem { Content = unit.Name, Tag = unit.Key });
            ToBox.Items.Add(new ComboBoxItem { Content = unit.Name, Tag = unit.Key });
        }
        FromBox.SelectedIndex = 0;
        ToBox.SelectedIndex = Math.Min(1, category.Units.Count - 1);
        _updating = false;

        if (string.IsNullOrWhiteSpace(AmountBox.Text))
            AmountBox.Text = "1";

        ConvertNow();
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        LoadUnits();
    }

    private void Unit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        ConvertNow();
    }

    private void Amount_Changed(object sender, TextChangedEventArgs e) => ConvertNow();

    private void ConvertNow()
    {
        if (_updating)
            return;

        if (CategoryBox.SelectedItem is not ComboBoxItem { Tag: string categoryKey } ||
            FromBox.SelectedItem is not ComboBoxItem { Tag: string from } ||
            ToBox.SelectedItem is not ComboBoxItem { Tag: string to })
            return;

        var text = (AmountBox.Text ?? "").Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            ResultText.Text = "—";
            return;
        }

        try
        {
            var converted = UnitConverter.Convert(amount, categoryKey, from, to);
            ResultText.Text = UnitConverter.Format(converted);
        }
        catch (Exception)
        {
            ResultText.Text = "—";
        }
    }

    // ---------- Programador ----------

    private void LoadBases()
    {
        _updating = true;
        HexBox.Text = "0";
        DecBox.Text = "0";
        OctBox.Text = "0";
        BinBox.Text = "0";
        _updating = false;
    }

    private void Base_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updating)
            return;

        var (text, numberBase) = sender switch
        {
            TextBox box when ReferenceEquals(box, HexBox) => (HexBox.Text, 16),
            TextBox box when ReferenceEquals(box, OctBox) => (OctBox.Text, 8),
            TextBox box when ReferenceEquals(box, BinBox) => (BinBox.Text, 2),
            _ => (DecBox.Text, 10)
        };

        if (!CalculatorSession.TryParseBase(text, numberBase, out var value))
        {
            BaseHint.Text = "Ese texto no es un número válido en esa base.";
            BaseHint.Foreground = (System.Windows.Media.Brush)FindResource("AmberBrush");
            return;
        }

        _updating = true;
        if (!ReferenceEquals(sender, HexBox)) HexBox.Text = CalculatorSession.ToBase(value, 16);
        if (!ReferenceEquals(sender, DecBox)) DecBox.Text = CalculatorSession.ToBase(value, 10);
        if (!ReferenceEquals(sender, OctBox)) OctBox.Text = CalculatorSession.ToBase(value, 8);
        if (!ReferenceEquals(sender, BinBox)) BinBox.Text = CalculatorSession.ToBase(value, 2);
        _updating = false;

        BaseHint.Text = "Escribe en cualquier base y las demás se actualizan.";
        BaseHint.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
    }
}
