namespace NMSE.Extractor.Data;

/// <summary>
/// Curated display names for catalogue entries whose localisation keys are absent
/// from the shipped game files. Verified against the current game data: the
/// product, technology and base part tables reference Name keys that no language
/// table provides, so no official name can be extracted for these entries.
/// <see cref="Prefix"/> marks them as editor-curated approximations so the UI can
/// flag them and explain the marker to users.
/// </summary>
public static class CuratedItemNames
{
    /// <summary>Prefix applied to every curated name so the UI can flag it.</summary>
    public const string Prefix = "[?] ";

    private static readonly Dictionary<string, (string Name, string Group)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        // Cosmos/Voyagers products with missing localisation keys.
        // The corvette module names are inferred from the game's own icon
        // filenames (PHASEBEAMMOD, HYPERDRIVEMOD, PULSEDRIVEMOD, LANDINGGEARMOD).
        ["CHART_BUILDER"] = ("Starchart Builder", "Cartographic Data"),
        ["WORLDSMB_SOUL"] = ("Soul Seed", "Exotic"),
        ["U_CRFIGHT1"] = ("Phase Beam Module", "Corvette Upgrade"),
        ["U_CRFIGHT2"] = ("Phase Beam Module", "Corvette Upgrade"),
        ["U_CRFIGHT3"] = ("Phase Beam Module", "Corvette Upgrade"),
        ["U_CRFIGHT4"] = ("Phase Beam Module", "Corvette Upgrade"),
        ["U_CRSCI1"] = ("Hyperdrive Module", "Corvette Upgrade"),
        ["U_CRSCI2"] = ("Hyperdrive Module", "Corvette Upgrade"),
        ["U_CRSCI3"] = ("Hyperdrive Module", "Corvette Upgrade"),
        ["U_CRSCI4"] = ("Hyperdrive Module", "Corvette Upgrade"),
        ["U_CRTRADE1"] = ("Pulse Drive Module", "Corvette Upgrade"),
        ["U_CRTRADE2"] = ("Pulse Drive Module", "Corvette Upgrade"),
        ["U_CRTRADE3"] = ("Pulse Drive Module", "Corvette Upgrade"),
        ["U_CRTRADE4"] = ("Pulse Drive Module", "Corvette Upgrade"),
        ["U_CRMINE1"] = ("Landing Gear Module", "Corvette Upgrade"),
        ["U_CRMINE2"] = ("Landing Gear Module", "Corvette Upgrade"),
        ["U_CRMINE3"] = ("Landing Gear Module", "Corvette Upgrade"),
        ["U_CRMINE4"] = ("Landing Gear Module", "Corvette Upgrade"),

        // Technology entry with missing localisation keys.
        ["SPIDERBRAIN"] = ("Spider Brain", "Sentinel Technology"),

        // Base part products whose BLD_* keys are absent from the language tables.
        // Several entries share placeholder keys (for example BLD_PRT_FOUNDATION
        // and BLD_SET_MONUMENT_NAME), so names are curated per item instead.
        ["B_BTRU_A"] = ("Big Buttress A", "Structural"),
        ["B_BTRU_B"] = ("Big Buttress B", "Structural"),
        ["B_BTRU_C"] = ("Big Buttress C", "Structural"),
        ["B_CANOPY_WALL1"] = ("Canopy Wall 1", "Decoration"),
        ["B_CANOPY_WALL2"] = ("Canopy Wall 2", "Decoration"),
        ["B_ROBOTARM"] = ("Robot Arm", "Decoration"),
        ["B_TOWER"] = ("Tower", "Decoration"),
        ["B_TOWER_B"] = ("Tower B", "Decoration"),
        ["B_TOWER_C"] = ("Tower C", "Decoration"),
        ["B_WALL_SUPPORTS"] = ("Wall Supports", "Structural"),
        ["CORRIDOR_WINDOW"] = ("Corridor Window", "Structural"),
        ["GAMETABLE"] = ("Holo-Arena", "Decoration"),
        ["HEALTHPLANT"] = ("Health Plant", "Decoration"),
        ["NPCEXPLORER001"] = ("NPC Explorer", "Decoration"),
        ["SET_B_MONU"] = ("Monument Base", "Decoration"),
        ["SET_B_MONU_FA"] = ("Monument Base (Alternate)", "Decoration"),
        ["SET_CLASS_A"] = ("Construct (Class A)", "Decoration"),
        ["SET_CLASS_B"] = ("Construct (Class B)", "Decoration"),
        ["SET_CLASS_S"] = ("Construct (Class S)", "Decoration"),
        ["SET_CONSTRUCT"] = ("Construct", "Decoration"),
        ["SET_FISHPOND"] = ("Fish Pond", "Decoration"),
        ["SET_F_MONU"] = ("Monument Front", "Decoration"),
        ["SET_F_MONU_FA"] = ("Monument Front (Alternate)", "Decoration"),
        ["SET_GROUNDDECAL"] = ("Ground Decal", "Decoration"),
        ["SET_INT_SHIPSAL"] = ("Ship Salvage Interior", "Decoration"),
        ["SET_INT_SUMMARY"] = ("Interior Summary", "Decoration"),
        ["SET_MAYORTERM"] = ("Mayor's Terminal", "Functional"),
        ["SET_MONUMENT"] = ("Monument", "Decoration"),
        ["SET_MONUMENT_FA"] = ("Monument (Alternate)", "Decoration"),
        ["SET_SFXCONST_S0"] = ("Construct Effect 0", "Decoration"),
        ["SET_SFXCONST_S1"] = ("Construct Effect 1", "Decoration"),
        ["SET_SFXCONST_S2"] = ("Construct Effect 2", "Decoration"),
        ["SET_STAFFBUILD"] = ("Staff Build", "Decoration"),
        ["SET_T_MONU"] = ("Monument Top", "Decoration"),
        ["SET_T_MONU_FA"] = ("Monument Top (Alternate)", "Decoration"),
        ["S_TOWER"] = ("Stone Tower", "Decoration"),
        ["S_TOWER_B"] = ("Stone Tower B", "Decoration"),
        ["S_TOWER_B_FA"] = ("Stone Tower B (Alternate)", "Decoration"),
        ["S_TOWER_C"] = ("Stone Tower C", "Decoration"),
        ["S_TOWER_FA"] = ("Stone Tower (Alternate)", "Decoration"),
    };

    /// <summary>
    /// Returns the curated display name and group for an item whose localisation
    /// keys are missing from the game files, or false when no curation exists.
    /// </summary>
    /// <param name="itemId">Item identifier from the game table.</param>
    /// <param name="name">Curated display name without the marker prefix.</param>
    /// <param name="group">Curated display group (subtitle) for the item.</param>
    public static bool TryGet(string itemId, out string name, out string group)
    {
        if (Names.TryGetValue(itemId, out var entry))
        {
            name = entry.Name;
            group = entry.Group;
            return true;
        }

        name = "";
        group = "";
        return false;
    }
}
