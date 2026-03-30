using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankingCalculator.Models
{
    public static class NameNormalizer
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = input
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                if (Char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString()
                     .Replace(" ", "")
                     .Replace("-", "");
        }
    }
}
