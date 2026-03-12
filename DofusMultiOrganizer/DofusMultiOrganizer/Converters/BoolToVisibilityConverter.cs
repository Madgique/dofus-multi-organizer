using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DofusOrganizer.Converters;

/// <summary>
/// Convertit un bool en Visibility.
/// Paramètre "Inverse" → true devient Collapsed, false devient Visible.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool boolValue = value is bool b && b;
        bool inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        return (boolValue ^ inverse) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
