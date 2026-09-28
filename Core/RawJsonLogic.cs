using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Provides utilities for parsing, formatting, and displaying raw JSON save data.
/// </summary>
internal static class RawJsonLogic
{
    /// <summary>
    /// Parses and reformats a JSON string with consistent indentation.
    /// </summary>
    /// <param name="jsonText">The raw JSON text to format.</param>
    /// <returns>The formatted JSON string.</returns>
    internal static string FormatJson(string jsonText)
    {
        var obj = JsonObject.Parse(jsonText);
        return obj.ToFormattedString();
    }

    /// <summary>
    /// Parses a JSON string into a <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="jsonText">The raw JSON text to parse.</param>
    /// <returns>The parsed JSON object.</returns>
    internal static JsonObject ParseJson(string jsonText)
    {
        return JsonObject.Parse(jsonText);
    }

    /// <summary>
    /// Converts save data to a human-readable display string.
    /// </summary>
    /// <param name="saveData">The save data JSON object.</param>
    /// <returns>A display-friendly string representation of the save data.</returns>
    internal static string ToDisplayString(JsonObject saveData)
    {
        return saveData.ToDisplayString();
    }

    /// <summary>
    /// Converts save data to a formatted display string split into individual lines,
    /// without allocating the full intermediate string.
    /// Prefer this over <see cref="ToDisplayString"/> when the result is only needed
    /// for line-level operations (e.g., computing a diff), as it eliminates one large
    /// LOH allocation per call. Returned lines have no trailing <c>'\r'</c>.
    /// </summary>
    internal static string[] ToLines(JsonObject saveData)
    {
        return saveData.ToLines();
    }

    /// <summary>
    /// Serializes any JSON value (object, array, or primitive) to a formatted
    /// display string with human-readable (deobfuscated) keys.
    /// </summary>
    internal static string SerializeValue(object? value)
    {
        return JsonParser.Serialize(value, formatted: true, skipReverseMapping: true);
    }

    /// <summary>
    /// Parses a JSON string into a typed value (JsonObject, JsonArray, or primitive).
    /// </summary>
    internal static object? ParseValue(string jsonText)
    {
        return JsonParser.ParseValue(jsonText);
    }

    /// <summary>
    /// Serializes a single node snippet for the isolated editor. Object properties are
    /// emitted as <c>"key": value,</c>, array elements as <c>value,</c>, and root values
    /// as <c>value</c>. The trailing comma is stripped again when the text is parsed.
    /// </summary>
    /// <param name="key">The node key, or <c>null</c> for the root value.</param>
    /// <param name="value">The node value to serialize.</param>
    /// <returns>The snippet text shown in the isolated editor.</returns>
    internal static string SerializeNodeSnippet(string? key, object? value)
    {
        string valueText = SerializeValue(value);
        // JsonParser formats with Environment.NewLine; the text editor stores lines
        // LF-only, so normalise here to keep snippet comparisons stable.
        if (valueText.Contains('\r'))
            valueText = valueText.Replace("\r\n", "\n").Replace('\r', '\n');

        if (string.IsNullOrEmpty(key))
            return valueText;
        if (key.StartsWith('['))
            return valueText + ",";
        return JsonParser.QuoteString(key) + ": " + valueText + ",";
    }

    /// <summary>
    /// Parses an isolated object-member snippet such as <c>"Key": { ... },</c>.
    /// A single trailing comma is ignored.
    /// </summary>
    /// <param name="snippetText">The snippet text to parse.</param>
    /// <returns>The property name and value from the snippet.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the text is not exactly one JSON property.
    /// </exception>
    internal static (string Key, object? Value) ParseObjectMemberSnippet(string snippetText)
    {
        var parsed = JsonObject.Parse("{" + StripTrailingComma(snippetText) + "}");
        var names = parsed.Names();
        if (names.Count != 1)
            throw new JsonException("Isolated edit must contain exactly one property");
        string key = names[0];
        return (key, parsed.Get(key));
    }

    /// <summary>
    /// Parses an isolated array-element or root snippet (a single JSON value,
    /// optionally followed by one trailing comma).
    /// </summary>
    /// <param name="snippetText">The snippet text to parse.</param>
    /// <returns>The parsed value.</returns>
    internal static object? ParseValueSnippet(string snippetText)
    {
        return JsonParser.ParseValue(StripTrailingComma(snippetText));
    }

    /// <summary>
    /// Replaces the contents of the root object in place from an isolated root snippet,
    /// preserving the root reference held by the rest of the application.
    /// </summary>
    /// <param name="root">The root object to update.</param>
    /// <param name="snippetText">The snippet text to parse.</param>
    internal static void ApplyRootSnippet(JsonObject root, string snippetText)
    {
        if (ParseValueSnippet(snippetText) is not JsonObject parsed)
            throw new JsonException("Root snippet must be a JSON object");
        root.Clear();
        var names = parsed.Names();
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            root.Set(name, parsed.Get(name));
        }
    }

    /// <summary>
    /// Trims whitespace from snippet text and removes a single trailing comma.
    /// </summary>
    /// <param name="text">The snippet text to normalize.</param>
    /// <returns>The normalized snippet text.</returns>
    private static string StripTrailingComma(string text)
    {
        string trimmed = text.Trim();
        return trimmed.EndsWith(',') ? trimmed[..^1] : trimmed;
    }

    /// <summary>
    /// Formats a JSON value for display in an edit dialog.
    /// Unlike tree-display formatting, this does NOT wrap strings in quotation marks
    /// so users edit the raw value without needing to handle surrounding quotes.
    /// </summary>
    internal static string FormatValueForEdit(object? value) => value switch
    {
        null => "null",
        string s => s,
        bool b => b ? "true" : "false",
        BinaryData bd => $"<binary:{bd.ToHexString()}>",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
        _ => value.ToString() ?? "null"
    };

    /// <summary>
    /// Parses user input from the edit dialog back into a typed value.
    /// When <paramref name="originalValue"/> is a <see cref="string"/>, the input is always
    /// returned as a string to preserve the original type and prevent corruption from
    /// values like "true", "42", or "null" being reinterpreted as non-string types.
    /// </summary>
    internal static object? ParseInputValue(string input, object? originalValue)
    {
        // If the original value was a string, always return the input as a string.
        // This prevents type corruption when editing string values that happen to
        // look like booleans, numbers, or null (e.g. "true", "42", "null").
        if (originalValue is string)
            return input;

        if (input == "null") return null;
        if (input == "true") return true;
        if (input == "false") return false;
        if (long.TryParse(input, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long l))
            return l >= int.MinValue && l <= int.MaxValue ? (int)l : l;
        if (double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            return d;
        return input;
    }

    /// <summary>
    /// Overload for adding new values where there is no original value to preserve.
    /// Applies standard type inference (null, bool, number, string).
    /// </summary>
    internal static object? ParseInputValue(string input) => ParseInputValue(input, null);

    /// <summary>
    /// Computes a simple line-by-line diff between two JSON strings.
    /// Lines only in the original are prefixed with "- ", lines only in the
    /// current version are prefixed with "+ ", unchanged lines with "  ".
    /// Uses the Myers diff algorithm for minimal, correct diffs.
    /// </summary>
    internal static string ComputeSimpleDiff(string original, string current)
    {
        if (original == current)
            return "No changes detected.";

        var rawDiff = ComputeRawDiff(original.Split('\n'), current.Split('\n'));

        var sb = new System.Text.StringBuilder();
        foreach (var (type, text) in rawDiff)
        {
            switch (type)
            {
                case DiffLineType.Added:   sb.Append("+ ").AppendLine(text); break;
                case DiffLineType.Removed: sb.Append("- ").AppendLine(text); break;
                default:                   sb.Append("  ").AppendLine(text); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>The type of a diff line: unchanged context, added, removed, a hunk separator, or a context header.</summary>
    internal enum DiffLineType { Context, Added, Removed, Separator, Header }

    /// <summary>A single line in a compact diff output.</summary>
    internal readonly record struct DiffLine(DiffLineType Type, string Text, int OldLineNum = 0, int NewLineNum = 0);

    /// <summary>
    /// Computes a compact diff showing only changed hunks with surrounding context lines.
    /// Unchanged regions between hunks are collapsed into a single separator line.
    /// This avoids building a huge string for large files where only a few values changed.
    /// Uses the Myers diff algorithm for minimal, correct diffs.
    /// Includes line numbers and JSON path context headers above each hunk.
    /// </summary>
    /// <param name="original">The original JSON text.</param>
    /// <param name="current">The current (modified) JSON text.</param>
    /// <param name="contextLines">Number of unchanged lines to show before/after each change.</param>
    /// <returns>A list of diff lines. Empty list means no changes.</returns>
    internal static List<DiffLine> ComputeCompactDiff(string original, string current, int contextLines = 3)
    {
        if (original == current)
            return [];

        // Split once and reuse for both diff computation and context headers
        // to avoid doubling the string[] memory allocation.
        var oldLines = original.Split('\n');
        var newLines = current.Split('\n');
        return ComputeCompactDiffFromLines(oldLines, newLines, contextLines);
    }

    /// <summary>
    /// Computes a compact diff from pre-split line arrays.
    /// Use this overload when the caller has already split the strings (e.g., after
    /// nulling the original strings to allow early GC of large JSON text).
    /// </summary>
    internal static List<DiffLine> ComputeCompactDiffFromLines(string[] oldLines, string[] newLines, int contextLines = 3)
    {
        var rawDiff = ComputeRawDiff(oldLines, newLines);

        // Detect the MaxDiffDistance fallback: when Myers exceeds its edit-distance cap it
        // returns every line of oldMid as Removed followed by every line of newMid as Added
        // (no Context lines in between).  ComputeRawDiff prepends the common prefix as
        // Context *before* calling MyersDiff, so contextCount is never zero even in the
        // fallback case - checking contextCount == 0 was always incorrect.
        // The reliable invariant is: a successful Myers run produces at most MaxDiffDistance
        // total edits; anything larger means the fallback fired.
        int contextCount = 0, removedCount = 0, addedCount = 0;
        foreach (var (type, _) in rawDiff)
        {
            if (type == DiffLineType.Context) contextCount++;
            else if (type == DiffLineType.Removed) removedCount++;
            else addedCount++;
        }
        if (removedCount + addedCount > MaxDiffDistance)
        {
            // Too many differences to display line-by-line: return a single informational
            // header.  (The raw removedCount/addedCount figures come from the fallback
            // array which equals the entire middle section of the file - not the true edit
            // count - so we don't show them to avoid confusing the user.)
            return [new DiffLine(DiffLineType.Header,
                $"Changes exceed the line-by-line display limit ({MaxDiffDistance} edits). Save the file and compare externally for a full diff.")];
        }

        // Assign line numbers, collapse unchanged regions, then insert context headers.
        var numberedDiff = AssignLineNumbers(rawDiff);
        var collapsed = CollapseContext(numberedDiff, contextLines);
        return InsertContextHeaders(collapsed, oldLines, newLines);
    }

    /// <summary>
    /// Assigns old-file and new-file line numbers to each raw diff entry.
    /// Removed lines only increment the old counter; added lines only increment the new counter;
    /// context lines increment both.
    /// </summary>
    internal static List<DiffLine> AssignLineNumbers(List<(DiffLineType Type, string Text)> rawDiff)
    {
        var result = new List<DiffLine>(rawDiff.Count);
        int oldLine = 1;
        int newLine = 1;

        foreach (var (type, text) in rawDiff)
        {
            switch (type)
            {
                case DiffLineType.Removed:
                    result.Add(new DiffLine(type, text, oldLine, 0));
                    oldLine++;
                    break;
                case DiffLineType.Added:
                    result.Add(new DiffLine(type, text, 0, newLine));
                    newLine++;
                    break;
                default: // Context
                    result.Add(new DiffLine(type, text, oldLine, newLine));
                    oldLine++;
                    newLine++;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Collapses large unchanged regions in a diff, keeping only <paramref name="contextLines"/>
    /// lines before and after each changed hunk. Collapsed regions become a Separator line.
    /// </summary>
    internal static List<DiffLine> CollapseContext(List<DiffLine> numberedDiff, int contextLines)
    {
        // Mark which lines are "near" a change
        var nearChange = new bool[numberedDiff.Count];
        for (int k = 0; k < numberedDiff.Count; k++)
        {
            if (numberedDiff[k].Type != DiffLineType.Context)
            {
                int start = Math.Max(0, k - contextLines);
                int end = Math.Min(numberedDiff.Count - 1, k + contextLines);
                for (int m = start; m <= end; m++)
                    nearChange[m] = true;
            }
        }

        var result = new List<DiffLine>();
        bool inSkip = false;

        for (int k = 0; k < numberedDiff.Count; k++)
        {
            if (nearChange[k])
            {
                if (inSkip)
                {
                    result.Add(new DiffLine(DiffLineType.Separator, ""));
                    inSkip = false;
                }
                result.Add(numberedDiff[k]);
            }
            else
            {
                inSkip = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Inserts JSON path context header lines above each hunk in the collapsed diff.
    /// The header shows the nearest enclosing JSON object/array key for the changed lines,
    /// similar to how GitHub shows class/method names above code diff hunks.
    /// </summary>
    internal static List<DiffLine> InsertContextHeaders(List<DiffLine> collapsed, string[] oldLines, string[] newLines)
    {
        var result = new List<DiffLine>(collapsed.Count + 10);
        bool needHeader = true; // first hunk always gets a header

        for (int i = 0; i < collapsed.Count; i++)
        {
            var dl = collapsed[i];

            if (dl.Type == DiffLineType.Separator)
            {
                result.Add(dl);
                needHeader = true;
                continue;
            }

            if (needHeader && (dl.Type == DiffLineType.Added || dl.Type == DiffLineType.Removed || dl.Type == DiffLineType.Context))
            {
                // Find the nearest enclosing JSON context for this line.
                // For removed/context lines, use the old file line number (both files have context lines).
                // For added lines, use the new file line number.
                int refLine;
                string[] refLines;
                if (dl.Type == DiffLineType.Added)
                {
                    refLine = dl.NewLineNum;
                    refLines = newLines;
                }
                else
                {
                    refLine = dl.OldLineNum;
                    refLines = oldLines;
                }
                string context = refLine > 0 ? FindJsonContext(refLines, refLine - 1) : ""; // convert 1-based to 0-based
                if (!string.IsNullOrEmpty(context))
                    result.Add(new DiffLine(DiffLineType.Header, context));
                needHeader = false;
            }

            result.Add(dl);
        }

        return result;
    }

    /// <summary>
    /// Walks backward from the given line to find the nearest enclosing JSON key context.
    /// Returns a path like <c>PlayerStateData > KnownProducts</c> showing the hierarchy
    /// of JSON objects enclosing the given line.
    /// </summary>
    internal static string FindJsonContext(string[] lines, int lineIndex)
    {
        var segments = WalkJsonContext(lines, lineIndex, includeOwnKey: false);
        if (segments.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0) sb.Append(" > ");
            sb.Append(segments[i].Key);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns the enclosing JSON key path for the given line as an ordered list of
    /// key/line pairs, from the root to the innermost key. When the line itself is a
    /// key line, that key is included as the final segment. Walks backward from the
    /// line, so prefer the owner map overload for live caret tracking on large documents.
    /// </summary>
    /// <param name="lines">The formatted JSON document lines.</param>
    /// <param name="lineIndex">The zero-based line index whose context is requested.</param>
    /// <returns>The ordered key path with the source line index of each key.</returns>
    internal static List<(string Key, int LineIndex)> FindJsonContextSegments(
        IReadOnlyList<string> lines, int lineIndex)
        => WalkJsonContext(lines, lineIndex, includeOwnKey: true);

    /// <summary>
    /// Returns the enclosing JSON key path for the given line using a precomputed
    /// per-line owner map (innermost enclosing container line index, or -1 at the
    /// root). This avoids a full backward scan, making live caret breadcrumbs cheap
    /// on very large documents.
    /// </summary>
    /// <param name="lines">The formatted JSON document lines.</param>
    /// <param name="lineIndex">The zero-based line index whose context is requested.</param>
    /// <param name="contextOwnerLines">Per-line owner map parallel to <paramref name="lines"/>.</param>
    /// <returns>The ordered key path with the source line index of each key.</returns>
    internal static List<(string Key, int LineIndex)> FindJsonContextSegments(
        IReadOnlyList<string> lines, int lineIndex, IReadOnlyList<int> contextOwnerLines)
    {
        var result = new List<(string Key, int LineIndex)>();
        if (lines.Count == 0 || lineIndex < 0 || lineIndex >= lines.Count) return result;

        // The caret line's own key (when present) is the innermost context.
        string? ownKey = ExtractKeyName(lines[lineIndex].TrimStart());
        if (ownKey != null)
            result.Add((ownKey, lineIndex));

        int owner = lineIndex < contextOwnerLines.Count ? contextOwnerLines[lineIndex] : -1;
        int guard = lines.Count; // Safety limit against malformed owner maps.
        while (owner >= 0 && owner < lines.Count && guard-- > 0)
        {
            string? key = ExtractKeyName(lines[owner].TrimStart());
            if (key != null)
                result.Add((key, owner));

            int parent = contextOwnerLines[owner];
            if (parent == owner) break;
            owner = parent;
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Shared backward walk that collects the key hierarchy enclosing a line.
    /// </summary>
    /// <param name="lines">The formatted JSON document lines.</param>
    /// <param name="lineIndex">The zero-based line index whose context is requested.</param>
    /// <param name="includeOwnKey">When true, a key on the starting line is included.</param>
    /// <returns>The ordered key path with the source line index of each key.</returns>
    private static List<(string Key, int LineIndex)> WalkJsonContext(
        IReadOnlyList<string> lines, int lineIndex, bool includeOwnKey)
    {
        var contextParts = new List<(string Key, int LineIndex)>();
        if (lines.Count == 0 || lineIndex < 0) return contextParts;

        int start = Math.Min(lineIndex, lines.Count - 1);
        int depth = 0;

        // Walk backward from the given line to find enclosing key names.
        for (int i = start; i >= 0; i--)
        {
            string rawLine = lines[i];
            string trimmed = rawLine.TrimStart();

            // Track nesting depth, ignoring braces inside string values.
            depth += CountClosingMinusOpening(trimmed);

            // If we've moved up a nesting level, look for the key name.
            if (depth < 0)
            {
                string? keyName = ExtractKeyName(trimmed);
                int keyLine = i;
                if (keyName == null && i > 0)
                {
                    // The key might be on the previous line (e.g., "key": \n {).
                    keyName = ExtractKeyName(lines[i - 1].TrimStart());
                    keyLine = i - 1;
                    // The previous line's opening token has been attributed to this
                    // level, so skip it to avoid adding the same key twice.
                    if (keyName != null) i--;
                }

                if (keyName != null)
                {
                    contextParts.Add((keyName, keyLine));
                    depth = 0; // Reset for the next level up.
                }
                else if (rawLine.Length > 0 && (rawLine[0] == '{' || rawLine[0] == '['))
                {
                    // Reached an opening token at column zero: the document root.
                    break;
                }
                else
                {
                    depth = 0; // Anonymous container (array element), keep going.
                }
            }
            else if (i == start && includeOwnKey)
            {
                // The starting line has no unmatched opening token of its own,
                // so its own key (when present) is the innermost context.
                string? keyName = ExtractKeyName(trimmed);
                if (keyName != null)
                    contextParts.Add((keyName, i));
            }
        }

        contextParts.Reverse();
        return contextParts;
    }

    /// <summary>
    /// Counts closing braces/brackets minus opening braces/brackets on a line,
    /// ignoring characters inside JSON strings.
    /// </summary>
    /// <param name="line">The trimmed line to inspect.</param>
    /// <returns>The net nesting delta for the line.</returns>
    private static int CountClosingMinusOpening(string line)
    {
        int delta = 0;
        bool inString = false;
        for (int c = 0; c < line.Length; c++)
        {
            char ch = line[c];
            if (inString)
            {
                if (ch == '\\') c++;
                else if (ch == '"') inString = false;
            }
            else if (ch == '"')
            {
                inString = true;
            }
            else if (ch == '}' || ch == ']')
            {
                delta++;
            }
            else if (ch == '{' || ch == '[')
            {
                delta--;
            }
        }
        return delta;
    }

    /// <summary>
    /// Extracts a JSON key name from a line like <c>"KeyName": {</c> or <c>"KeyName": [</c>.
    /// Returns null if the line doesn't contain a key assignment.
    /// </summary>
    private static string? ExtractKeyName(string trimmedLine)
    {
        if (!trimmedLine.StartsWith('"')) return null;
        int endQuote = trimmedLine.IndexOf('"', 1);
        if (endQuote <= 0) return null;

        // Verify this is a key (followed by colon)
        int afterQuote = endQuote + 1;
        while (afterQuote < trimmedLine.Length && char.IsWhiteSpace(trimmedLine[afterQuote]))
            afterQuote++;

        if (afterQuote < trimmedLine.Length && trimmedLine[afterQuote] == ':')
            return trimmedLine[1..endQuote];

        return null;
    }

    #region Myers Diff Algorithm

    /// <summary>
    /// Maximum edit distance (number of changed lines) before falling back to a summary diff.
    /// Capping this bounds both the V-array allocation and the history snapshots to
    /// O(MaxDiffDistance) rather than O(N+M). Memory cost: each of the D+1 history snapshots
    /// is (2*MaxDiffDistance+3) ints (~16 KB at 2000); total ~= 32 MB worst case. Time cost is
    /// O(N + D^2) where N ~= file length in lines - at D=2000 this is well under a second for
    /// typical NMS saves (~500 K lines). Files whose diff exceeds this limit receive a single
    /// informational header line instead of an unusable all-removed-then-all-added wall.
    /// 2000 comfortably covers all practical bulk inventory operations (RefillAllStacks on
    /// a fully-loaded save produces at most ~1500 single-line Amount edits; RepairAllSlots
    /// produces slot-removal line-blocks that stay well under this limit).
    /// </summary>
    private const int MaxDiffDistance = 2000;

    /// <summary>
    /// Computes the raw diff between two line arrays using the Myers diff algorithm
    /// with common prefix/suffix trimming for efficiency.
    /// Produces the minimal edit script, correctly handling duplicate lines like
    /// <c>},</c>, <c>{</c>, <c>]</c> that are common in JSON.
    /// </summary>
    internal static List<(DiffLineType Type, string Text)> ComputeRawDiff(string[] oldLines, string[] newLines)
    {
        // Normalize line endings once
        var oldNorm = new string[oldLines.Length];
        var newNorm = new string[newLines.Length];
        for (int i = 0; i < oldLines.Length; i++) oldNorm[i] = oldLines[i].TrimEnd('\r');
        for (int i = 0; i < newLines.Length; i++) newNorm[i] = newLines[i].TrimEnd('\r');

        // Trim common prefix
        int prefix = 0;
        while (prefix < oldNorm.Length && prefix < newNorm.Length && oldNorm[prefix] == newNorm[prefix])
            prefix++;

        // Trim common suffix
        int suffix = 0;
        while (suffix < oldNorm.Length - prefix && suffix < newNorm.Length - prefix &&
               oldNorm[oldNorm.Length - 1 - suffix] == newNorm[newNorm.Length - 1 - suffix])
            suffix++;

        var result = new List<(DiffLineType, string)>();

        // Add common prefix as context
        for (int i = 0; i < prefix; i++)
            result.Add((DiffLineType.Context, oldNorm[i]));

        // Compute diff on the middle (non-matching) section
        int oldMidLen = oldNorm.Length - prefix - suffix;
        int newMidLen = newNorm.Length - prefix - suffix;

        if (oldMidLen > 0 || newMidLen > 0)
        {
            var oldMid = new string[oldMidLen];
            var newMid = new string[newMidLen];
            Array.Copy(oldNorm, prefix, oldMid, 0, oldMidLen);
            Array.Copy(newNorm, prefix, newMid, 0, newMidLen);

            result.AddRange(MyersDiff(oldMid, newMid));
        }

        // Add common suffix as context
        for (int i = oldNorm.Length - suffix; i < oldNorm.Length; i++)
            result.Add((DiffLineType.Context, oldNorm[i]));

        return result;
    }

    /// <summary>
    /// Myers diff algorithm - computes the shortest minimal
	/// diff between two arrays of pre-normalized lines. 
	/// O(N*D) time where D is the edit distance.
    /// V array is bounded to O(MaxDiffDistance) rather than O(N+M) so that each
    /// per-step history snapshot is tiny regardless of file size.
    /// </summary>
    private static List<(DiffLineType Type, string Text)> MyersDiff(string[] oldArr, string[] newArr)
    {
        int N = oldArr.Length;
        int M = newArr.Length;

        if (N == 0 && M == 0) return [];
        if (N == 0) return newArr.Select(l => (DiffLineType.Added, l)).ToList();
        if (M == 0) return oldArr.Select(l => (DiffLineType.Removed, l)).ToList();

        int max = N + M;
        // Size the V array by MaxDiffDistance, not by N+M. Diagonals k only ever
        // fall in [-d, d] where d <= MaxDiffDistance, so this covers all accessed
        // indices. Each history snapshot shrinks from O(N+M) to O(MaxDiffDistance),
        // eliminating hundreds of MB of allocations for large but mostly-unchanged files.
        int offset = MaxDiffDistance + 1;
        int vSize = 2 * MaxDiffDistance + 3;

        var V = new int[vSize];
        Array.Fill(V, -1);
        V[1 + offset] = 0;

        // Save snapshots of V at the start of each d-step for backtracking
        var history = new List<int[]>(Math.Min(max, MaxDiffDistance) + 1);

        for (int d = 0; d <= Math.Min(max, MaxDiffDistance); d++)
        {
            history.Add((int[])V.Clone());

            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && V[k - 1 + offset] < V[k + 1 + offset]))
                    x = V[k + 1 + offset];       // Down (insert from new)
                else
                    x = V[k - 1 + offset] + 1;   // Right (delete from old)

                int y = x - k;

                // Follow diagonal (matching lines)
                while (x < N && y < M && oldArr[x] == newArr[y])
                {
                    x++;
                    y++;
                }

                V[k + offset] = x;

                if (x >= N && y >= M)
                    return BacktrackMyers(history, d, oldArr, newArr, offset);
            }
        }

        // Exceeded MaxDiffDistance: fall back to all-removed + all-added
        var fallback = new List<(DiffLineType, string)>();
        foreach (var l in oldArr) fallback.Add((DiffLineType.Removed, l));
        foreach (var l in newArr) fallback.Add((DiffLineType.Added, l));
        return fallback;
    }

    /// <summary>
    /// Backtracks through saved Myers algorithm states to reconstruct the edit script.
    /// </summary>
    private static List<(DiffLineType Type, string Text)> BacktrackMyers(
        List<int[]> history, int D, string[] oldArr, string[] newArr, int offset)
    {
        var result = new List<(DiffLineType, string)>();
        int x = oldArr.Length, y = newArr.Length;

        for (int d = D; d > 0; d--)
        {
            var Vprev = history[d]; // V state at start of step d (= end of step d-1)
            int k = x - y;

            // Determine which diagonal we came from in step d-1
            int prevK;
            if (k == -d || (k != d && Vprev[k - 1 + offset] < Vprev[k + 1 + offset]))
                prevK = k + 1; // Came from above (down = insert)
            else
                prevK = k - 1; // Came from left (right = delete)

            int prevX = Vprev[prevK + offset];
            int prevY = prevX - prevK;

            // Compute where the edit step lands
            int editX, editY;
            if (prevK == k + 1)
            {
                // Down move: x unchanged, y = y+1
                editX = prevX;
                editY = prevY + 1;
            }
            else
            {
                // Right move: x increased by 1, y = y
                editX = prevX + 1;
                editY = prevY;
            }

            // Add diagonal matches from (editX, editY) to (x, y) in reverse
            while (x > editX && y > editY)
            {
                x--;
                y--;
                result.Add((DiffLineType.Context, oldArr[x]));
            }

            // Add the edit operation
            if (prevK == k + 1)
            {
                // Insert: added line from new
                y--;
                result.Add((DiffLineType.Added, newArr[y]));
            }
            else
            {
                // Delete: removed line from old
                x--;
                result.Add((DiffLineType.Removed, oldArr[x]));
            }
        }

        // Remaining diagonal from (0, 0) to current pos
        while (x > 0 && y > 0)
        {
            x--;
            y--;
            result.Add((DiffLineType.Context, oldArr[x]));
        }

        // Any remaining lines (shouldn't happen for input that isn't malformed, but you never know...)
        while (x > 0)
        {
            x--;
            result.Add((DiffLineType.Removed, oldArr[x]));
        }
        while (y > 0)
        {
            y--;
            result.Add((DiffLineType.Added, newArr[y]));
        }

        result.Reverse();
        return result;
    }

    #endregion
}
