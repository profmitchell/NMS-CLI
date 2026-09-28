using System.Globalization;
using NMSE.Data;

namespace NMSE.Core;

/// <summary>
/// Query parsing and matching for the Database Search panel.
/// <para>
/// A query is split into space-separated terms which must all match (AND).
/// A term without wildcard characters is matched as a case-insensitive substring.
/// A term containing <c>*</c> (any sequence) or <c>?</c> (single character) is
/// matched as a case-insensitive glob against the whole field value.
/// </para>
/// </summary>
internal static class DatabaseSearchLogic
{
    private static readonly char[] TermSeparators = { ' ', '\t', '\r', '\n' };
    private static readonly char[] WildcardChars = { '*', '?' };

    /// <summary>A parsed database search query.</summary>
    internal sealed class DatabaseSearchQuery
    {
        /// <summary>The space-separated search terms, in input order.</summary>
        public IReadOnlyList<string> Terms { get; }

        /// <summary>True when the query has no terms and therefore matches everything.</summary>
        public bool IsEmpty => Terms.Count == 0;

        internal DatabaseSearchQuery(IReadOnlyList<string> terms)
        {
            Terms = terms;
        }
    }

    /// <summary>An empty query that matches every item.</summary>
    internal static readonly DatabaseSearchQuery EmptyQuery = new(Array.Empty<string>());

    /// <summary>
    /// Parses raw search input into terms. Whitespace separates terms; an empty
    /// or whitespace-only input produces <see cref="EmptyQuery"/>.
    /// </summary>
    /// <param name="input">The raw text typed into the search box.</param>
    /// <returns>The parsed query.</returns>
    internal static DatabaseSearchQuery ParseQuery(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return EmptyQuery;
        string[] terms = input.Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);
        return terms.Length == 0 ? EmptyQuery : new DatabaseSearchQuery(terms);
    }

    /// <summary>
    /// Determines whether every term in the query matches at least one searchable
    /// field of the item.
    /// </summary>
    /// <param name="item">The database item to test.</param>
    /// <param name="query">The parsed query.</param>
    /// <returns>True when the item matches all query terms.</returns>
    internal static bool MatchesItem(GameItem item, DatabaseSearchQuery query)
    {
        if (query.IsEmpty) return true;

        foreach (string term in query.Terms)
        {
            if (!MatchesAnyField(item, term))
                return false;
        }
        return true;
    }

    private static bool MatchesAnyField(GameItem item, string term)
    {
        if (Matches(item.Id, term)) return true;
        if (Matches(item.Name, term)) return true;
        if (Matches(item.NameLower, term)) return true;
        if (Matches(item.Subtitle, term)) return true;
        if (Matches(item.Description, term)) return true;
        if (Matches(item.Category, term)) return true;
        if (Matches(item.ProductCategory, term)) return true;
        if (Matches(item.SubstanceCategory, term)) return true;
        if (Matches(item.WikiCategory, term)) return true;
        if (Matches(item.TechnologyCategory, term)) return true;
        if (Matches(item.ItemType, term)) return true;
        if (Matches(item.SourceTable, term)) return true;
        if (Matches(item.Rarity, term)) return true;
        if (Matches(item.Quality, term)) return true;
        if (Matches(item.TradeCategory, term)) return true;
        if (Matches(item.DeploysInto, term)) return true;
        if (Matches(item.BuildableShipTechID, term)) return true;
        if (Matches(item.GiveRewardOnSpecialPurchase, term)) return true;
        if (Matches(item.Icon, term)) return true;
        if (Matches(item.Symbol, term)) return true;
        if (Matches(item.MaxStackSize.ToString(CultureInfo.InvariantCulture), term)) return true;
        if (Matches(item.ChargeValue.ToString(CultureInfo.InvariantCulture), term)) return true;
        return false;
    }

    /// <summary>
    /// Matches a single value against a term, using substring semantics when the
    /// term has no wildcard characters and glob semantics when it does.
    /// </summary>
    /// <param name="value">The field value to test.</param>
    /// <param name="term">The search term.</param>
    /// <returns>True when the value matches the term.</returns>
    internal static bool Matches(string? value, string term)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(term)) return false;

        return term.IndexOfAny(WildcardChars) >= 0
            ? GlobMatch(value, term)
            : value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Case-insensitive glob match supporting <c>*</c> (any sequence, including
    /// empty) and <c>?</c> (exactly one character). Uses an iterative two-pointer
    /// algorithm with backtracking, so it allocates nothing per call.
    /// </summary>
    /// <param name="text">The value to match.</param>
    /// <param name="pattern">The glob pattern.</param>
    /// <returns>True when the whole value matches the pattern.</returns>
    internal static bool GlobMatch(string text, string pattern)
    {
        int textIndex = 0;
        int patternIndex = 0;
        int starPatternIndex = -1;
        int starTextIndex = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length
                && (pattern[patternIndex] == '?' || CharsEqualIgnoreCase(pattern[patternIndex], text[textIndex])))
            {
                patternIndex++;
                textIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starPatternIndex = patternIndex;
                starTextIndex = textIndex;
                patternIndex++;
            }
            else if (starPatternIndex >= 0)
            {
                patternIndex = starPatternIndex + 1;
                starTextIndex++;
                textIndex = starTextIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }

    private static bool CharsEqualIgnoreCase(char a, char b) =>
        a == b || char.ToUpperInvariant(a) == char.ToUpperInvariant(b);

    /// <summary>
    /// Returns the most meaningful category label for an item, since the raw JSON
    /// only populates the generic Category field for technology, substances and
    /// a few special cases. The fallback order is:
    /// <list type="number">
    /// <item>TechnologyCategory (Suit, Ship, Weapon, ...)</item>
    /// <item>Category (substance/corvette grouping)</item>
    /// <item>Group (the item's in-game subtitle, e.g. "Decoration")</item>
    /// <item>ProductCategory (e.g. "BuildingPart")</item>
    /// <item>SubstanceCategory (e.g. "Fuel")</item>
    /// </list>
    /// Returns an empty string only when the item has no category data at all.
    /// </summary>
    /// <param name="item">The database item.</param>
    /// <returns>The display category label.</returns>
    internal static string GetDisplayCategory(GameItem item)
    {
        if (IsUsable(item.TechnologyCategory)) return item.TechnologyCategory;
        if (IsUsable(item.Category)) return item.Category;
        if (IsUsable(item.Subtitle)) return item.Subtitle;
        if (IsUsable(item.ProductCategory)) return item.ProductCategory;
        if (IsUsable(item.SubstanceCategory)) return item.SubstanceCategory;
        return "";
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrEmpty(value) && !value.Equals("None", StringComparison.OrdinalIgnoreCase);
}
