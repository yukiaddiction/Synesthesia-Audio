using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Synesthesia_Audio.Converters;

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value ?? false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class EmptyStringToTrueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class EmptyStringToFalseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NotNullToTrueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NotNullToFalseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class PlayPauseTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying) return isPlaying ? "⏸" : "▶";
        return "▶";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Avoids decoding full-size artwork for small surfaces; falls back to the original until a thumbnail exists.
/// </summary>
public sealed class CoverThumbnailConverter : IValueConverter
{
    public int Edge { get; init; } = Synesthesia_Audio.Core.CoverThumbnails.CardEdge;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return value;

        // Remote artwork has nothing on disk to downscale.
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;

        var thumbnail = Synesthesia_Audio.Core.CoverThumbnails.For(path, Edge);
        return File.Exists(thumbnail) ? thumbnail : path;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class BoolToArrowConverter : IValueConverter
{
    // Shared so bindings don't allocate a converter per row.
    public static readonly BoolToArrowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool expanded && expanded ? "▼" : "▶";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Compares a card's folder with the folder that is currently playing, so only the active work's jacket is marked.
/// </summary>
public sealed class FolderPathMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string playing || values[1] is not string folder)
            return false;

        return !string.IsNullOrEmpty(playing)
            && !string.IsNullOrEmpty(folder)
            && string.Equals(playing, folder, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Turns the stored "SFW"/"NSFW" codes into the words the shelf uses.</summary>
public sealed class AgeRatingLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Synesthesia_Audio.Core.AgeRatings.IsNsfw(value as string)) return "R-18";
        if (Synesthesia_Audio.Core.AgeRatings.IsSfw(value as string)) return "All Age";
        return "Unrated";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

