using System.Text;
using System.Text.RegularExpressions;

namespace ServiceBench.App.Utilities;

public static class SlugHelper
{
    private static readonly Regex InvalidChars = new("[^A-Za-z0-9]+", RegexOptions.Compiled);

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "device";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        var sanitized = InvalidChars.Replace(builder.ToString(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "device" : sanitized;
    }
}
