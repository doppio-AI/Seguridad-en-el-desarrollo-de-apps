using System.Text.RegularExpressions;

namespace VulnerableApp.Security
{
    
    public static class SecurityPatternDetector
    {
        private static readonly Regex SqlInjectionPattern = new(
            @"(\bOR\b\s+['""]?\d|\bUNION\b\s+\bSELECT\b|\bDROP\b\s+\bTABLE\b|--|;--|'\s*=\s*'|\bSELECT\b.*\bFROM\b|xp_cmdshell)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex XssPattern = new(
            @"(<script|onerror\s*=|onload\s*=|javascript:|<img[^>]+src|<iframe|document\.cookie)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool LooksLikeSqlInjection(string? input) =>
            !string.IsNullOrEmpty(input) && SqlInjectionPattern.IsMatch(input);

        public static bool LooksLikeXss(string? input) =>
            !string.IsNullOrEmpty(input) && XssPattern.IsMatch(input);

        /// <summary>Recorta y neutraliza un valor para dejarlo seguro de escribir en el log.</summary>
        public static string SafeSnippet(string? input, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var trimmed = input.Length > maxLength ? input[..maxLength] + "…" : input;
            return System.Net.WebUtility.HtmlEncode(trimmed);
        }
    }
}
