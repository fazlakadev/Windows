using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Fazlaka.Windows.Views;

public static class Ui
{
    public static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToCollapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility StringToVisibility(string? value)
        => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ObjectToVisibility(object? value)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public static bool ToEnabled(bool busy) => !busy;

    public static bool ToEnabledWhen(object? target, bool busy) => target is not null && !busy;

    public static bool PromptVisible(bool hasSearched, bool isSearching) => !hasSearched && !isSearching;

    public static Visibility PromptVisibility(bool hasSearched, bool isSearching)
        => !hasSearched && !isSearching ? Visibility.Visible : Visibility.Collapsed;

    public static bool ResultsHeaderVisible(bool hasSearched, bool isSearching, bool hasError)
        => hasSearched && !isSearching && !hasError;

    public static Visibility ResultsHeaderVisibility(bool hasSearched, bool isSearching, bool hasError)
        => hasSearched && !isSearching && !hasError ? Visibility.Visible : Visibility.Collapsed;

    public static bool InfoStatusVisible(bool hasError, string? message)
        => !hasError && !string.IsNullOrWhiteSpace(message);

    public static Visibility InfoStatusVisibility(bool hasError, string? message)
        => !hasError && !string.IsNullOrWhiteSpace(message) ? Visibility.Visible : Visibility.Collapsed;

    public static bool ErrorStatusVisible(bool hasError, string? message)
        => hasError && !string.IsNullOrWhiteSpace(message);

    public static Visibility ErrorStatusVisibility(bool hasError, string? message)
        => hasError && !string.IsNullOrWhiteSpace(message) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility InitialsVisibility(string? avatarUrl)
        => string.IsNullOrWhiteSpace(avatarUrl) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility AvatarVisibility(string? avatarUrl)
        => string.IsNullOrWhiteSpace(avatarUrl) ? Visibility.Collapsed : Visibility.Visible;

    public static ImageSource? ImageFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        try
        {
            return new BitmapImage(uri);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string FormatCount(long count)
    {
        if (count >= 1_000_000)
        {
            return $"{count / 1_000_000.0:0.#}M";
        }

        if (count >= 1_000)
        {
            return $"{count / 1_000.0:0.#}K";
        }

        return count.ToString(CultureInfo.InvariantCulture);
    }
}
