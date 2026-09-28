using NMSE.Data;

namespace NMSE.Core;

/// <summary>How a Collected Knowledge page tracks progress.</summary>
internal enum KnowledgeRecipe
{
    Counter,
    Bitmask,
    Interaction,
    Words,
}

/// <summary>Static definition of one Collected Knowledge catalogue page.</summary>
internal sealed record KnowledgePage(
    string Id,
    string Category,
    string Name,
    int Slot,
    int PageIndex,
    int TableMax,
    KnowledgeRecipe Recipe,
    string? GlobalStat = null,
    int? SiiIndex = null,
    int? SiiTarget = null);

/// <summary>Status of a Collected Knowledge page against its targets.</summary>
internal sealed record KnowledgePageStatus(
    KnowledgePage Page,
    int? LastSeen,
    int? GlobalValue,
    string Status,
    string Detail);

/// <summary>Kind of a story completer entry.</summary>
internal enum KnowledgeCompleterKind
{
    Global,
    BaseComputer,
    DevNotes,
    SiiPatch,
    Flag,
    Mission,
}

/// <summary>Status of one story completer entry.</summary>
internal sealed record KnowledgeCompleterStatus(
    string Id,
    string Name,
    KnowledgeCompleterKind Kind,
    int Target,
    int? Current,
    bool Complete,
    string Detail,
    CatalogueDatabase.SiiPatch? Patch = null,
    int Data = 0);

/// <summary>
/// Baked definition of the Collected Knowledge pages and story completers,
/// mirroring the verified reference tool catalogue.
/// </summary>
internal static class KnowledgeCatalogue
{
    /// <summary>Race slots patched when completing interaction pages.</summary>
    internal static readonly int[] DefaultRaces = [0, 1, 2, 6, 7];

    /// <summary>LastSeen entries that mark the language/glyph pages as present.</summary>
    internal static readonly (int Slot, int PageIndex, string Label)[] LanguageGlyphPages =
    [
        (0, 3, "Language Records (Gek)"),
        (1, 3, "Language Records (Vy'keen)"),
        (2, 3, "Language Records (Korvax)"),
        (4, 1, "The Words of the Atlas"),
        (4, 2, "Portal Glyphs"),
        (8, 1, "Language Records (Autophage)"),
    ];

    /// <summary>Soft story GLOBAL counters raised by the story completers.</summary>
    internal static readonly (string StatId, int Target)[] StoryCompleterGlobals =
    [
        ("ATLAS_STORY", 6),
        ("ATLAS_PATH", 9),
        ("ATLAS_LOOPS", 7),
        ("BUILDERS_INTRO", 3),
        ("PIRATE_MYSTERY", 4),
    ];

    /// <summary>GLOBAL stats where -1 means empty rather than full.</summary>
    internal static readonly HashSet<string> NegativeOneMeansEmpty = new(StringComparer.OrdinalIgnoreCase)
    {
        "DEV_NOTES",
    };

    internal const int DevNotesPageSlot = 5;
    internal const int DevNotesPageIndex = 13;
    internal const int DevNotesTarget = 39;
    internal const int BaseCompPageSlot = 5;
    internal const int BaseCompPageIndex = 1;

    /// <summary>The Collected Knowledge pages in reference order.</summary>
    internal static readonly KnowledgePage[] Pages =
    [
        // Gek
        new("gek_plaque", "The Gek", "Ancient Plaque (Gek)", 0, 0, 29, KnowledgeRecipe.Interaction, SiiIndex: 18, SiiTarget: 30),
        new("gek_archive", "The Gek", "Planetary Archive (Gek)", 0, 1, 1023, KnowledgeRecipe.Bitmask, "LIB_TRA_LORE"),
        new("gek_mono", "The Gek", "Monolith Visions (Gek)", 0, 2, 19, KnowledgeRecipe.Interaction, SiiIndex: 10, SiiTarget: 20),
        new("gek_words", "The Gek", "Language Records (Gek)", 0, 3, -1, KnowledgeRecipe.Words),
        // Vy'keen
        new("vy_plaque", "The Vy'keen", "Ancient Plaque (Vy'keen)", 1, 0, 29, KnowledgeRecipe.Interaction, SiiIndex: 18, SiiTarget: 30),
        new("vy_archive", "The Vy'keen", "Planetary Archive (Vy'keen)", 1, 1, 1023, KnowledgeRecipe.Bitmask, "LIB_WAR_LORE"),
        new("vy_mono", "The Vy'keen", "Monolith Visions (Vy'keen)", 1, 2, 19, KnowledgeRecipe.Interaction, SiiIndex: 10, SiiTarget: 20),
        new("vy_words", "The Vy'keen", "Language Records (Vy'keen)", 1, 3, -1, KnowledgeRecipe.Words),
        // Korvax
        new("kor_plaque", "The Korvax", "Ancient Plaque (Korvax)", 2, 0, 32, KnowledgeRecipe.Interaction, SiiIndex: 18, SiiTarget: 33),
        new("kor_archive", "The Korvax", "Planetary Archive (Korvax)", 2, 1, 1023, KnowledgeRecipe.Bitmask, "LIB_EXP_LORE"),
        new("kor_mono", "The Korvax", "Monolith Visions (Korvax)", 2, 2, 18, KnowledgeRecipe.Interaction, SiiIndex: 10, SiiTarget: 19),
        new("kor_words", "The Korvax", "Language Records (Korvax)", 2, 3, -1, KnowledgeRecipe.Words),
        // Atlas
        new("atlas_iface", "The Atlas", "Atlas Interface", 4, 0, 11, KnowledgeRecipe.Counter, "ATLAS_LORE"),
        new("atlas_words", "The Atlas", "The Words of the Atlas", 4, 1, -1, KnowledgeRecipe.Words),
        new("atlas_glyphs", "The Atlas", "Portal Glyphs", 4, 2, -1, KnowledgeRecipe.Words),
        // Journey Records
        new("jr_journey", "Journey Records", "The Journey", 5, 0, 51, KnowledgeRecipe.Counter, "CORE_LORE"),
        new("jr_basecomp", "Journey Records", "Base Computer Archives", 5, 1, 21, KnowledgeRecipe.Counter, "BASECOMP_LORE"),
        new("jr_overseer", "Journey Records", "Expanding the Base", 5, 2, 20, KnowledgeRecipe.Counter, "OVERSEER_LORE"),
        new("jr_scientist", "Journey Records", "Scientific Research", 5, 3, 13, KnowledgeRecipe.Counter, "SCIENTIST_LORE"),
        new("jr_weap", "Journey Records", "Weapons Research", 5, 4, 13, KnowledgeRecipe.Counter, "WEAPGUY_LORE"),
        new("jr_farmer", "Journey Records", "Agricultural Research", 5, 5, 10, KnowledgeRecipe.Counter, "FARMER_LORE"),
        new("jr_exo", "Journey Records", "Exocraft Technician", 5, 6, 16, KnowledgeRecipe.Counter, "EXOTUT_LORE"),
        new("jr_water", "Journey Records", "Dreams of the Deep", 5, 7, 16, KnowledgeRecipe.Counter, "WATERSTORY_LORE"),
        new("jr_bio", "Journey Records", "Starbirth", 5, 8, 13, KnowledgeRecipe.Counter, "BIOSHIP_LORE"),
        new("jr_worm", "Journey Records", "Emergence", 5, 9, 13, KnowledgeRecipe.Counter, "WORM_LORE"),
        new("jr_bug", "Journey Records", "Liquidators", 5, 10, 5, KnowledgeRecipe.Counter, "BUG_LORE"),
        new("jr_sent", "Journey Records", "A Trace of Metal", 5, 11, 20, KnowledgeRecipe.Counter, "SENT_MISS_LORE"),
        new("jr_pirate", "Journey Records", "Under a Rebel Star", 5, 12, 20, KnowledgeRecipe.Counter, "PIRATES_LORE"),
        new("jr_devnotes", "Journey Records", "Developer Commentary", 5, 13, 39, KnowledgeRecipe.Counter, "DEV_NOTES"),
        // Other History
        new("oh_aband", "Other History", "Abandoned Building", 7, 0, 33, KnowledgeRecipe.Interaction, SiiIndex: 20, SiiTarget: 34),
        new("oh_crash", "Other History", "Crashed Freighter", 7, 1, 19, KnowledgeRecipe.Interaction, SiiIndex: 54, SiiTarget: 20),
        new("oh_grave", "Other History", "Unknown Grave", 7, 2, 15, KnowledgeRecipe.Interaction, SiiIndex: 55, SiiTarget: 16),
        new("oh_bound", "Other History", "Boundary Failure", 7, 3, 29, KnowledgeRecipe.Interaction, SiiIndex: 56, SiiTarget: 30),
        new("oh_water", "Other History", "Water Relic", 7, 4, 7, KnowledgeRecipe.Interaction, SiiIndex: 76, SiiTarget: 8),
        new("oh_derelict", "Other History", "Derelict Freighter", 7, 5, 524287, KnowledgeRecipe.Bitmask, "ABAND_LORE"),
        new("oh_pillar", "Other History", "Sentinel Pillar", 7, 6, 21, KnowledgeRecipe.Interaction, SiiIndex: 123, SiiTarget: 22),
        new("oh_epilogue", "Other History", "New Beginnings", 7, 7, 3, KnowledgeRecipe.Interaction, SiiIndex: 61, SiiTarget: 4),
        // Autophage
        new("ap_mono", "The Autophage", "Monolith Visions (Autophage)", 8, 0, 11, KnowledgeRecipe.Interaction, SiiIndex: 10, SiiTarget: 12),
        new("ap_words", "The Autophage", "Language Records (Autophage)", 8, 1, -1, KnowledgeRecipe.Words),
        new("ap_robo", "The Autophage", "They Who Returned", 8, 2, 16, KnowledgeRecipe.Counter, "ROBOMISS_LORE"),
        new("ap_purp", "The Autophage", "In Stellar Multitudes", 8, 3, 12, KnowledgeRecipe.Counter, "PURPM_LORE"),
    ];
}
