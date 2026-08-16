namespace Ratchet.Parsing;

internal static class SqlFileTokens
{
    private const string HeaderMarker = "/* Migration";
    private const string SeparatorToken = "NewStatement";

    public static bool TryStartHeader(string line, out string remainder)
    {
        var trimmedStart = line.AsSpan().TrimStart();
        if (!trimmedStart.StartsWith(HeaderMarker, StringComparison.OrdinalIgnoreCase))
        {
            remainder = "";
            return false;
        }

        var after = trimmedStart.Length > HeaderMarker.Length
            ? trimmedStart[HeaderMarker.Length..]
            : ReadOnlySpan<char>.Empty;

        var afterTrimStart = after.TrimStart();
        if (afterTrimStart.IsEmpty || afterTrimStart[0] == '{')
        {
            remainder = after.ToString();
            return true;
        }

        remainder = "";
        return false;
    }

    public static bool IsStatementSeparator(string line)
    {
        var trimmed = line.AsSpan().Trim();
        if (!trimmed.StartsWith("--", StringComparison.Ordinal))
            return false;

        var afterDashes = trimmed[2..].TrimStart();
        if (!afterDashes.StartsWith(SeparatorToken, StringComparison.OrdinalIgnoreCase))
            return false;

        return afterDashes.Length == SeparatorToken.Length
            || char.IsWhiteSpace(afterDashes[SeparatorToken.Length]);
    }
}
