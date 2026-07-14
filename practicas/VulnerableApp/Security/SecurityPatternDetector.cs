using System.Text.RegularExpressions;

namespace VulnerableApp.Security;

public static class SecurityPatternDetector
{
    private static readonly Regex SqlInjectionPattern = new(
        @"(?ix)
        (?:\b(?:union\s+select|select|insert|update|delete|drop|alter|exec(?:ute)?)\b)
        |(?:--|/\*|\*/|;\s*(?:drop|select|delete|update|insert))
        |(?:\b(?:or|and)\b\s+['""\d][^=]{0,20}=)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XssPattern = new(
        @"(?ix)
        <\s*/?\s*(?:script|iframe|object|embed|svg|img|style)\b
        |javascript\s*:
        |\bon[a-z]+\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool LooksLikeSqlInjection(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SqlInjectionPattern.IsMatch(value);

    public static bool LooksLikeXss(string? value) =>
        !string.IsNullOrWhiteSpace(value) && XssPattern.IsMatch(value);

    public static string? SanitizeForLog(string? value, int maxLength = 200)
    {
        if (value is null)
        {
            return null;
        }

        var sanitized = value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..maxLength];
    }
}
