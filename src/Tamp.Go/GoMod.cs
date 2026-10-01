using System.Globalization;

namespace Tamp.Go;

/// <summary>
/// Minimal, read-only <c>go.mod</c> parsing. Extracts the <c>module</c> path and the <c>go</c>
/// language-version directive without a full go.mod grammar or shelling out to <c>go mod edit</c>.
/// </summary>
/// <remarks>
/// This is deliberately not a round-tripping editor. Go modules have no package-version field to
/// stamp (version == git tag), so there is no symmetric writer the way <c>Tamp.Cargo</c> ships a
/// Cargo.toml version editor. The reader exists for discovery — deriving a module path or the
/// minimum language version for conditional build logic.
/// </remarks>
internal static class GoMod
{
    /// <summary>
    /// Return the value of the <c>module</c> directive, handling both the single-line form
    /// (<c>module example.com/foo</c>) and the parenthesized block form. Returns <c>null</c>
    /// when no module directive is present.
    /// </summary>
    public static string? GetModulePath(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        foreach (var raw in SplitLines(content))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("module ", StringComparison.Ordinal) || line == "module")
            {
                var rest = line.Length > "module".Length ? line["module".Length..].Trim() : string.Empty;
                // Block form: `module (` then the path on the next non-empty line.
                if (rest is "" or "(")
                    continue;
                return Unquote(rest);
            }
        }
        return null;
    }

    /// <summary>
    /// Return the value of the <c>go</c> directive (e.g. <c>1.23</c> or <c>1.23.4</c>), or
    /// <c>null</c> when absent. The leading token must be a version number so we don't mistake
    /// the <c>module example.com/go-thing</c> path for a <c>go</c> directive.
    /// </summary>
    public static string? GetGoDirective(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        foreach (var raw in SplitLines(content))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("go ", StringComparison.Ordinal))
            {
                var rest = line[3..].Trim();
                if (rest.Length > 0 && char.IsDigit(rest[0]))
                    return rest;
            }
        }
        return null;
    }

    private static IEnumerable<string> SplitLines(string content)
        => content.Split('\n');

    private static string StripComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line[..idx] : line;
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s[1..^1];
        return s;
    }
}
