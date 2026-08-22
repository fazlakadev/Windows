using System;
using System.Globalization;
using System.Windows.Input;
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

    public static string PinStatusText(bool enabled) => enabled ? "القفل مفعّل — ستحتاج الرمز عند فتح التطبيق." : "القفل معطّل.";

    public static string TwoFaStatusText(bool enabled) => enabled ? "المصادقة الثنائية مفعّلة." : "المصادقة الثنائية معطّلة.";

    public static ICommand RevokeCommand(object? command, object? tag)
        => new RevokeProxyCommand(command, tag);
}

internal sealed class RevokeProxyCommand : ICommand
{
    private readonly ICommand? _inner;
    private readonly object? _session;

    public RevokeProxyCommand(object? inner, object? session)
    {
        _inner = inner as ICommand;
        _session = session;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => _inner?.CanExecuteChanged += value;
        remove => _inner?.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter) => _inner?.CanExecute(_session) ?? false;

    public void Execute(object? parameter) => _inner?.Execute(_session);
}
