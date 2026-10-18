using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EasyUninstall.Converters;

/// <summary>
/// Collapses the emoji placeholder when an icon path is present, and shows it
/// only when the icon could not be resolved (empty path).
/// </summary>
public sealed class IconFallbackVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var path = value as string;
        return string.IsNullOrWhiteSpace(path) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
