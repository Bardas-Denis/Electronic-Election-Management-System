using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// The comparison key for place names typed by hand. Two spellings that fold to the same key
    /// are the same place: "Timișoara", "Timişoara", "TIMISOARA" and "timisoara" all do.
    /// </summary>
    /// <remarks>
    /// A key is only ever compared - never stored, never shown. The name keeps the spelling it
    /// arrived with. Localities are the one level of the tree that users type rather than pick,
    /// which is why they need this and countries and counties do not.
    /// </remarks>
    public static partial class GeographicNames
    {
        public static string Key(string name)
        {
            // FormD splits "ș" into "s" plus a combining comma, so dropping the combining marks
            // leaves the base letters. It handles the cedilla "ş" the same way.
            var decomposed = name.Normalize(NormalizationForm.FormD);
            var letters = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    letters.Append(c);
            }

            return Separators().Replace(letters.ToString(), " ").Trim().ToUpperInvariant();
        }

        // A hyphen counts as a space, so "Cluj-Napoca" and "Cluj Napoca" share a key.
        [GeneratedRegex(@"[\s\-]+")]
        private static partial Regex Separators();
    }
}
