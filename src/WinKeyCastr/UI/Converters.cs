using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinKeyCastr.Settings;

namespace WinKeyCastr.UI;

/// <summary>Binds a radio button to one value of an enum: ConverterParameter is the value's name.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Enum.Parse(targetType, parameter.ToString()!) : Binding.DoNothing;
}

/// <summary>Binds an enum to a ComboBox's SelectedIndex (items listed in declaration order).</summary>
public sealed class EnumIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Enum e ? Array.IndexOf(Enum.GetValues(e.GetType()), e) : -1;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index && index >= 0 ? Enum.GetValues(targetType).GetValue(index)! : Binding.DoNothing;
}

/// <summary>Icon placement (1, 2, 3) ↔ ComboBox index (0, 1, 2).</summary>
public sealed class IconPlacementIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IconPlacement placement ? (int)placement - 1 : 2;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index && index >= 0 ? (IconPlacement)(index + 1) : Binding.DoNothing;
}

/// <summary>Negates a bool; used by the radio button that stands for "false".</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? false : Binding.DoNothing;
}

/// <summary>Visible when the bound string equals ConverterParameter.</summary>
public sealed class NameVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
