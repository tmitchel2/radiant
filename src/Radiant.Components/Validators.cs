using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Radiant.Components;

/// <summary>
/// Common checks for <see cref="Form"/> fields: each takes the field's text and gives an error
/// message, or null when the text is fine. Checks other than <see cref="Required"/> pass empty text,
/// so an optional field can be left blank.
/// </summary>
public static class Validators
{
    /// <summary>Fails blank text.</summary>
    public static Func<string, string?> Required(string message = "Required") =>
        text => string.IsNullOrWhiteSpace(text) ? message : null;

    /// <summary>Fails text that isn't an email address (something@something.something).</summary>
    public static Func<string, string?> Email(string message = "Enter an email address, like name@example.com") =>
        text => text.Length == 0 || Regex.IsMatch(text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) ? null : message;

    /// <summary>Fails text shorter than <paramref name="length"/> characters.</summary>
    public static Func<string, string?> MinLength(int length, string? message = null) =>
        text => text.Length == 0 || text.Length >= length ? null : message ?? $"Use at least {length} characters";

    /// <summary>Fails text longer than <paramref name="length"/> characters.</summary>
    public static Func<string, string?> MaxLength(int length, string? message = null) =>
        text => text.Length <= length ? null : message ?? $"Use at most {length} characters";

    /// <summary>Fails text that doesn't match <paramref name="pattern"/> (a regular expression, matched in full).</summary>
    public static Func<string, string?> Pattern(string pattern, string message) =>
        text => text.Length == 0 || Regex.IsMatch(text, "^(?:" + pattern + ")$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) ? null : message;

    /// <summary>Fails text that isn't a number from <paramref name="min"/> to <paramref name="max"/>, in the current culture.</summary>
    public static Func<string, string?> Number(double min = double.MinValue, double max = double.MaxValue, string? message = null) =>
        text =>
        {
            if (text.Length == 0)
            {
                return null;
            }
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var number))
            {
                return message ?? "Enter a number";
            }
            return number < min || number > max
                ? message ?? string.Create(CultureInfo.CurrentCulture, $"Enter a number from {min:#,0.##} to {max:#,0.##}")
                : null;
        };
}
