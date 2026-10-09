using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameTracker.App.Converters;

/// <summary>
/// Loads a local image decoded at the displayed size (ConverterParameter = pixel width), which keeps
/// memory low with many covers. The file is read into memory so it is never locked.
/// </summary>
public sealed class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (parameter is string width && int.TryParse(width, NumberStyles.None, CultureInfo.InvariantCulture, out var pixels))
                image.DecodePixelWidth = pixels;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the value is "truthy" (non-null, non-empty, true, non-zero). Parameter "invert" flips it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var truthy = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            System.Collections.ICollection c => c.Count > 0,
            _ => true,
        };
        if (parameter is "invert")
            truthy = !truthy;
        return truthy ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Deterministic accent-ish color per title, used behind the initials when a game has no cover.</summary>
public sealed class TitleToBrushConverter : IValueConverter
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x3A, 0x2A, 0x5C), Color.FromRgb(0x5C, 0x2A, 0x3A), Color.FromRgb(0x1F, 0x4A, 0x5C),
        Color.FromRgb(0x2A, 0x5C, 0x45), Color.FromRgb(0x5C, 0x45, 0x1F), Color.FromRgb(0x2F, 0x35, 0x5C),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var hash = 0;
        foreach (var c in text)
            hash = (hash * 31 + c) & 0x7FFFFFFF;

        var brush = new LinearGradientBrush(Palette[hash % Palette.Length], Color.FromRgb(0x14, 0x16, 0x1C), 60);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
