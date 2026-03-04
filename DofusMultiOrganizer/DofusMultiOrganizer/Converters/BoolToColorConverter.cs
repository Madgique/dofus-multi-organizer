using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace DofusOrganizer.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? Color.FromArgb(255, 34, 197, 94)   // vert
            : Color.FromArgb(255, 239, 68, 68);  // rouge
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
