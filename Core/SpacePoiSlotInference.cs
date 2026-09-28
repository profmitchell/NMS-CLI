namespace NMSE.Core;

/// <summary>
/// Per-type spawn rules for the space POI table, loaded from
/// <c>Resources/json/Space POI.json</c>. A system generates POIs in the order of the
/// list this comes from, normal instances first, then the type's forced hidden extras.
/// </summary>
internal sealed class SpacePoiTypeRule
{
    /// <summary>Game type name (e.g. "BasePlatform").</summary>
    public string Type { get; init; } = "";

    /// <summary>Minimum number of normal instances generated.</summary>
    public int MinCount { get; init; }

    /// <summary>Maximum number of normal instances generated.</summary>
    public int MaxCount { get; init; }

    /// <summary>Number of forced hidden extras generated after the normal instances.</summary>
    public int ForcedHiddenExtras { get; init; }

    /// <summary>
    /// Whether the type can spawn in abandoned systems. Types with false are skipped
    /// when inferring a system flagged as abandoned (for example the Outpost).
    /// </summary>
    public bool AllowedInAbandonedSystem { get; init; } = true;

    /// <summary>Whether the type can spawn in empty systems.</summary>
    public bool AllowedInEmptySystem { get; init; } = true;

    /// <summary>Initial discovery level applied to normal instances (the game's floor).</summary>
    public SpaceStationLogic.SpacePoiDiscoveryLevel NormalInitialLevel { get; init; }

    /// <summary>Star map display name localisation key.</summary>
    public string NameLocKey { get; init; } = "";

    /// <summary>English display name from the table.</summary>
    public string Name { get; init; } = "";

    /// <summary>Localised display name, refreshed when the UI language changes.</summary>
    public string DisplayName { get; internal set; } = "";
}

/// <summary>Inference result for a single packed SpacePoiDiscoveries slot.</summary>
internal sealed class SpacePoiSlotInfo
{
    /// <summary>Slot index, 0-31.</summary>
    public int Slot { get; init; }

    /// <summary>True when every consistent layout allocates this slot.</summary>
    public bool DefinitelyAllocated { get; init; }

    /// <summary>True when at least one consistent layout allocates this slot.</summary>
    public bool CoveredByLayouts { get; init; }

    /// <summary>
    /// True when every consistent layout allocates this slot with the same POI type.
    /// This is the only label the editor may trust: the true layout is always among
    /// the consistent ones, so unanimous allocation plus agreement guarantees it.
    /// </summary>
    public bool TypeIsUnique { get; init; }

    /// <summary>Index into the rules list when <see cref="TypeIsUnique"/> is true, otherwise -1.</summary>
    public int TypeIndex { get; init; } = -1;
}

/// <summary>Result of inferring the POI layout from a system's packed discovery data.</summary>
internal sealed class SpacePoiSlotInferenceResult
{
    /// <summary>Per-slot results, 32 entries in slot order.</summary>
    public IReadOnlyList<SpacePoiSlotInfo> Slots { get; }

    /// <summary>Number of spawn-count layouts consistent with the packed values.</summary>
    public int LayoutCount { get; }

    /// <summary>Creates a result.</summary>
    public SpacePoiSlotInferenceResult(IReadOnlyList<SpacePoiSlotInfo> slots, int layoutCount)
    {
        Slots = slots;
        LayoutCount = layoutCount;
    }
}

/// <summary>
/// Works out which POI type each packed discovery slot belongs to by enumerating every
/// spawn-count layout the game could have generated for the system, then keeping the
/// layouts consistent with the stored levels. The game repairs slots below their POI's
/// initial level on load, so the stored values constrain the layout strongly.
/// </summary>
internal static class SpacePoiSlotInference
{
    /// <summary>Total packed slots (16 per PackedData field).</summary>
    public const int SlotCount = SpaceStationLogic.PackedDataSlotCount;

    /// <summary>
    /// Infers the slot layout for one system. Returns null when no layout is consistent
    /// with the values (for example hand-edited data) or when no type rules are loaded.
    /// </summary>
    /// <param name="packed0">The system's PackedData0 value.</param>
    /// <param name="packed1">The system's PackedData1 value.</param>
    /// <param name="types">Ordered type rules from the space POI table.</param>
    /// <param name="abandonedSystems">
    /// True when the system follows abandoned-system spawn rules, which exclude types
    /// whose <see cref="SpacePoiTypeRule.AllowedInAbandonedSystem"/> is false.
    /// </param>
    /// <returns>The inference result, or null when it cannot be determined.</returns>
    public static SpacePoiSlotInferenceResult? Infer(ulong packed0, ulong packed1, IReadOnlyList<SpacePoiTypeRule> types, bool abandonedSystems = false)
    {
        if (types.Count == 0) return null;

        int[] values = Decode(packed0, packed1);

        // Prefer the strict pass (game-written saves hold zero in unallocated slots).
        // Fall back to floors only so hand-edited values still produce labels.
        return Evaluate(values, types, requireZeroBeyondAllocation: true, abandonedSystems)
            ?? Evaluate(values, types, requireZeroBeyondAllocation: false, abandonedSystems);
    }

    /// <summary>Decodes the two packed values into 32 two-bit slot levels.</summary>
    private static int[] Decode(ulong packed0, ulong packed1)
    {
        var values = new int[SlotCount];
        for (int i = 0; i < 16; i++)
        {
            values[i] = (int)((packed0 >> (2 * i)) & 3);
            values[16 + i] = (int)((packed1 >> (2 * i)) & 3);
        }
        return values;
    }

    /// <summary>
    /// Enumerates every spawn-count combination, accumulates coverage per slot and
    /// returns the per-slot result. Null when no layout fits.
    /// </summary>
    private static SpacePoiSlotInferenceResult? Evaluate(int[] values, IReadOnlyList<SpacePoiTypeRule> types, bool requireZeroBeyondAllocation, bool abandonedSystems)
    {
        var coverage = new int[SlotCount];
        var firstType = new int[SlotCount];
        var sameType = new bool[SlotCount];
        Array.Fill(firstType, -1);
        Array.Fill(sameType, true);

        var floor = new int[SlotCount];
        var slotType = new int[SlotCount];
        int layouts = 0;
        int length = 0;

        void Recurse(int typeIndex)
        {
            if (typeIndex == types.Count)
            {
                // Check values beyond the allocation first when strict.
                if (requireZeroBeyondAllocation)
                {
                    for (int i = length; i < SlotCount; i++)
                        if (values[i] != 0) return;
                }

                layouts++;
                for (int i = 0; i < length; i++)
                {
                    coverage[i]++;
                    if (firstType[i] < 0)
                        firstType[i] = slotType[i];
                    else if (firstType[i] != slotType[i])
                        sameType[i] = false;
                }
                return;
            }

            var rule = types[typeIndex];

            // Types disallowed in abandoned systems spawn zero instances there
            // (including their forced hidden extras).
            bool typeAllowed = !abandonedSystems || rule.AllowedInAbandonedSystem;
            int minCount = typeAllowed ? rule.MinCount : 0;
            int maxCount = typeAllowed ? rule.MaxCount : 0;
            int extras = typeAllowed ? rule.ForcedHiddenExtras : 0;

            // Normal instances, then the forced hidden extras.
            for (int count = minCount; count <= maxCount; count++)
            {
                int startLength = length;
                bool valid = true;

                for (int n = 0; n < count; n++)
                {
                    if (length >= SlotCount || values[length] < (int)rule.NormalInitialLevel)
                    {
                        valid = false;
                        break;
                    }
                    floor[length] = (int)rule.NormalInitialLevel;
                    slotType[length] = typeIndex;
                    length++;
                }

                for (int e = 0; e < extras && valid; e++)
                {
                    if (length >= SlotCount)
                    {
                        valid = false;
                        break;
                    }
                    floor[length] = (int)SpaceStationLogic.SpacePoiDiscoveryLevel.Hidden;
                    slotType[length] = typeIndex;
                    length++;
                }

                if (valid)
                    Recurse(typeIndex + 1);

                length = startLength;
            }
        }

        Recurse(0);

        if (layouts == 0) return null;

        var slots = new SpacePoiSlotInfo[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            bool covered = firstType[i] >= 0;
            bool unique = coverage[i] == layouts && sameType[i];
            slots[i] = new SpacePoiSlotInfo
            {
                Slot = i,
                DefinitelyAllocated = coverage[i] == layouts,
                CoveredByLayouts = covered,
                TypeIsUnique = unique,
                TypeIndex = unique ? firstType[i] : -1
            };
        }
        return new SpacePoiSlotInferenceResult(slots, layouts);
    }
}
