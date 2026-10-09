using System.Globalization;
using System.Text;

namespace GameTracker.Core.Common;

public static class TextUtil
{
    /// <summary>
    /// Reduces a title to letters and digits so that "DOOM Eternal™" and "Doom: Eternal" compare equal.
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var c in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
