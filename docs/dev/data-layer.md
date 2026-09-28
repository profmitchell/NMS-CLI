# Data Layer

## Overview

The Data layer (`Data/`) provides game reference databases, icon management, UI string
localisation, and the key name mapper. Most database classes are `static`; the classes that
need a configured directory or per-instance state are instance classes (`GameItemDatabase`,
`RecipeDatabase`, `WordDatabase`, `LocalisationService`, `IconManager`, `JsonNameMapper`).
The JSON database files consumed here are produced by the NMSE.Extractor tool
(see [extractor.md](extractor.md)).

Coordinate conversion utilities no longer live in this layer: `CoordinateHelper` moved to
`Core/Utilities/CoordinateHelper.cs` (`NMSE.Core.Utilities`), and the game colour palette
JSON is loaded by `NMSE.Core.Utilities.NmsColourPalette`.

Databases fall into two categories:

1. **Hardcoded** - data defined as static arrays/dictionaries in source code
   (`ElementDatabase`, `GalaxyDatabase`, `InventoryStackDatabase`, `TechAdjacencyDatabase`,
   `TechPacks`, `ProceduralStubs`, `BaseStatLimits`). These are updated manually when game
   patches change values.

2. **Dynamic (JSON-loaded)** - loaded from files in the `Resources/json/` directory at
   runtime: `GameItemDatabase` (item files), `RecipeDatabase`, `RewardDatabase`,
   `WordDatabase`, `FrigateTraitDatabase`, `SettlementDatabase`, `WikiGuideDatabase`,
   `TitleDatabase`, `CompanionDatabase`, `CreaturePartDatabase`, `CompanionAccessoryDatabase`,
   `PetBattleMoveDatabase`, `PetBattleMovesetDatabase`, `PetBiomeAffinityMap`,
   `SpacePoiTableDatabase`, and `CatalogueDatabase` (`Catalogue Pack.json`). Most of these
   support localisation via `_LocStr` keys.

JSON output filenames contain spaces (for example `Frigate Traits.json`,
`Settlement Perks.json`, `Wiki Guide.json`, `Space POI.json`). `GameItemDatabase` uses the
filename without extension as the item type, so those names are also item classifications.

Directories referenced by this layer:

| Path | Contents |
|------|----------|
| `Resources/json/` | Extractor-generated item and lookup databases: 34 `.json` files plus `Egg Modifiers.json.tbc` and the `lang/` subfolder |
| `Resources/json/lang/` | Per-language game string dumps, 16 BCP 47 files (see [LocalisationService](#localisationservice)) |
| `Resources/ui/lang/` | Editor UI string tables, 16 BCP 47 files (see [UiStrings](#uistrings)) |
| `Resources/map/mapping.json` | Obfuscated save key mapping (see [JsonNameMapper](#jsonnamemapper)) |
| `Resources/images/` | Item and UI icon images (over 5,200 files) |

---

## Database Classes

### CompanionDatabase / CreaturePartDatabase

| | |
|---|---|
| File | `Data/CompanionDatabase.cs` |
| Purpose | Companion species, creature descriptor parts, pet accessories, pet battle moves, and biome affinities |

`CompanionDatabase` is JSON-loaded from `Creature Species.json` (86 entries) via
`LoadFromFile()`. Each `CompanionEntry` has an `Id` (stored with a `^` prefix), a `Species`
display name (derived from the ID by replacing underscores with spaces and title-casing, so
the JSON does not store names), an optional `ForcedAffinity` (empty for biome-based
creatures) and optional `AccessoryVariants`. Provides `Entries` (read-only list) and `ById`
(case-insensitive dictionary). The `BiomeTypes`, `CreatureTypes` and `BiomeTypeLocKeys`
arrays remain hardcoded UI lists. Loading also builds the descriptor to variant reverse
index used by `CompanionAccessoryDatabase`.

`CreaturePartDatabase` (same file) is JSON-loaded from `Creature Descriptors.json`
(64 creature entries) via `LoadFromFile()`. Each `CreaturePartEntry` has `CreatureId`,
`FriendlyName` and `Details`, a list of `DescriptorGroup` instances, each holding
`DescriptorOption` entries that may themselves expose child groups, forming a recursive
part-selection tree. `GetForCreatureId()` strips the `^` prefix; `GetFlatGroups()` flattens
the tree in dependency order based on the current selections; `NewDescriptorId()` generates
the 10-digit descriptor IDs used by the Creature Builder. The class starts empty and is
populated from JSON, with no hardcoded fallback.

---

### CompanionAccessoryDatabase

| | |
|---|---|
| File | `Data/CompanionDatabase.cs` |
| JSON | `Resources/json/Companion Accessories.json` (32 entries) |
| Purpose | Pet accessory definitions, per-slot filtering, and accessory name localisation |

A static database loaded from `Companion Accessories.json` via `LoadFromFile()`. Each
`CompanionAccessoryEntry` has `Id` (for example `PET_ACC_0`), `Name`, `NameLocStr`,
`Descriptor` and `LinkedProduct`. Provides `Entries` and `ById`.

The `AccessorySlot` enum models the save layout: `Right` (index 0), `Left` (index 1),
`Front` (index 2) and `Back` (shares index 2). `GetEntriesForSlot()` filters accessories
through a per-slot allow list, `GroupNameToSlot()` maps game group names
(`"RIGHT"`, `"LEFT"`, `"FRONT"`, `"BACK"`) to slots, and `GetSlotLayoutForCreature()`
resolves a creature's ordered slots from its own variants, falling back to the
`VariantByDescriptor` reverse index for creatures that share another species' rig.
`GetSlotLabelLocKey()` returns UI string keys for slot labels. Supports
`ApplyLocalisation()` / `RevertLocalisation()` for accessory names.

---

### PetBattleMoveDatabase / PetBattleMovesetDatabase / PetBiomeAffinityMap

| | |
|---|---|
| File | `Data/CompanionDatabase.cs` |
| JSON | `Resources/json/Pet Battle Moves.json` (61 moves), `Pet Battle Movesets.json` (6 movesets), `Game Table Globals.json` |
| Purpose | Pet battle move, moveset, and affinity data for the Companion panel battle editor |

`PetBattleMoveDatabase` loads individual moves. Each `PetBattleMoveEntry` has `Id`,
`DebugDescription`, `Target`, `MultiTurnMove`, `BasicMove`, `IconStyle`, `NameStub`,
`LocIDToDescribeStat` and `Phases`. Display helpers (`LocalisedDescription`,
`TargetDisplay`, `IconStyleDisplay`) resolve through `UiStrings` with English fallbacks.

`PetBattleMovesetDatabase` loads movesets made up of five slots, each with weighted move
options. `GetAllowedMovesForSlot()` returns the templates valid for a slot and
`FindMovesetsContainingMove()` finds which movesets use a move.

`PetBiomeAffinityMap` loads the `PetBiomeAffinities`, `PetAffinityLoc`,
`PetAffinityLocStub` and `PetTargetLoc` sections from `Game Table Globals.json`.
`BiomeToAffinity()` maps a biome to its affinity, `ResolveAffinity()` prefers a species'
forced affinity and falls back to biome affinity, and the remaining helpers resolve display
names, game names, and weak/strong type matchups (the corrected game names and matchup
tables are hardcoded).

---

### ElementDatabase

| | |
|---|---|
| File | `Data/ElementDatabase.cs` |
| Purpose | Map NMS resource names to chemical element symbols |

A static dictionary of 53 entries that maps resource names like `"Carbon"` to symbols like
`"C"`. Includes exotic elements such as Di-hydrogen as `H`, Tritium as `H3`, and Chromatic
Metal as `Ch`. Used for compact display in inventory grids and tooltips.

---

### FrigateTraitDatabase

| | |
|---|---|
| File | `Data/FrigateTraitDatabase.cs` |
| JSON | `Resources/json/Frigate Traits.json` (178 entries) |
| Purpose | Frigate traits with stat effects, loaded from JSON with localisation support |

All trait data is loaded from `Frigate Traits.json` at startup via `LoadFromFile()`.
Each `FrigateTrait` record has: `Id`, display `Name`, `NameLocStr` (localisation key from
`FRIGATETRAITTABLE.MXML`'s `DisplayName` field, e.g. `"FLEET_TRAIT_SEC_FUEL_1"`),
stat `Type`, numeric `Strength`, `Beneficial` flag, and `Primary`/`Secondary` fleet class
associations. `DisplayName` formats the strength and type for combo boxes. Supports
`ApplyLocalisation()` / `RevertLocalisation()` for language switching. A `None` sentinel
(`Id = "^"`) represents the empty dropdown entry. The class starts empty and is populated
from JSON, with no hardcoded fallback data.

---

### GalaxyDatabase

| | |
|---|---|
| File | `Data/GalaxyDatabase.cs` |
| Purpose | Catalog of all 256 NMS galaxies plus one UI-only sentinel |

A static array of named 6-tuples: `(int Number, string Hex, string Name, string Type,
string Description, string Core)` in the `Galaxies` field, with 257 entries. Entries 0 to
255 are the real galaxies (Euclid is Normal #1; Eissentam is Lush #10); index 256 is a
non-game sentinel used only for UI listing and must never be used for coordinate or portal
hex generation. `RealGalaxyCount` is 256, `IsSpecialGalaxyIndex()` detects the sentinel,
and `GetGalaxyName()`, `GetGalaxyType()`, `GetGalaxyCore()`, `GetGalaxyHex()`,
`GetGalaxyDisplayName()` and `GetGalaxyCoreColor()` provide accessors for galaxy selection
and coordinate display.

---

### GameItemDatabase / GameItem

| | |
|---|---|
| File | `Data/GameItemDatabase.cs`, `Data/GameItem.cs` |
| Purpose | Load and manage all game items from extractor-generated JSON database files |

An instance class (not static) because it is initialized after the application locates the
database directory. `LoadItemsFromJsonDirectory(dir)` parses every `*.json` file in the
directory in parallel with `Parallel.ForEach`, skipping files that are loaded by their own
databases: `Rewards`, `Recipes`, `Words`, `Frigate Traits`, `Settlement Perks`,
`Wiki Guide`, `Titles`, and `none`. Files whose root is not a JSON array are also skipped,
which excludes `Catalogue Pack.json`. The directory currently holds 34 JSON files, of which
26 are parsed as item databases. Each file becomes an item type named after the filename
without extension.

`GameItem` properties: `Id`, `Name`, `NameLower`, `Subtitle`, `Description`, `NameLocStr`,
`NameLowerLocStr`, `SubtitleLocStr`, `DescriptionLocStr`, `Category`, `ProductCategory`,
`SubstanceCategory`, `WikiCategory`, `Icon`, `Symbol`, `MaxStackSize`, `ChargeValue`,
`IsChargeable`, `BuildFullyCharged`, `IsCooking`, `ItemType`, `BuildableShipTechID`,
`Rarity`, `Quality`, `TechnologyCategory`, `IsUpgrade`, `IsCore`, `IsProcedural`,
`IsCraftable`, `TradeCategory`, `GiveRewardOnSpecialPurchase`, `CanPickUp`, `IsTemporary`,
`IsBuilding`, `DeploysInto`, and `SourceTable`. Helper methods: `QualityToClass()`,
`RarityToClass()` and `IsValidForOwner(owner)`.

**Localisation keys:** `GameItem` stores nullable `_LocStr` properties for localised
string lookups loaded from the matching fields in the extractor-produced JSON files. These
are raw NMS localisation keys (e.g. `"UI_FUEL_1_NAME"`) resolved against the active
language file by `LocalisationService`. `ApplyLocalisation(service)` swaps the display
fields and backs up the English originals internally. For `Name`, it tries
`{LocStr}`, `{LocStr}_NAME`, then a level-specific key extracted from the item ID suffix
(`{LocStr}3_NAME` for `UP_SHLD3`, `{LocStr}_X_NAME` for `UP_BOLTX`), then `{LocStr}1_NAME`
as the final fallback. **When the `Name_LocStr` chain fails** (common for procedural tech
whose `Name_LocStr` base differs from the language-file base, e.g. `UP_HYPERDRIVE` versus
`UP_HYPER4_NAME`), it derives the name key from `DescriptionLocStr` by replacing `_DESC`
with `_NAME`; it also tries inserting `_` before a terminal `X` for illegal variants
(`UP_SHOTGUNX_DESC` becomes `UP_SHOTGUN_X_NAME`). `NameLower` and `Subtitle` are derived
from `DescriptionLocStr` in the same way (`_NAME_L` and `_SUB`, plus `X`-underscore
variants) when the primary `_LocStr` fields are empty; `NameLower` also falls back to
`{LocStr}_L`. `Description` additionally tries the `X`-underscore variant.
`RevertLocalisation()` restores all four fields from the backup.

| Method | Description |
|--------|-------------|
| `LoadItemsFromJsonDirectory(dir)` | Parallel-loads all JSON files; returns true if at least one item loaded |
| `GetItem(itemId)` | Case-insensitive lookup by ID, also handling `^` and `T_` prefixed save IDs |
| `Items` | Read-only dictionary of all items keyed by ID |
| `CorvetteBasePartTechMap` | Maps `CV_` tech IDs to possible base part product IDs |
| `GetItemsByCategory(category)` | Items whose `Category` matches |
| `GetItemsByType(type)` | Items whose `ItemType` (JSON filename) matches |
| `Search(query)` | Case-insensitive search across name, ID, and description |
| `ApplyLocalisation(service)` | Swaps display fields for localised values; backs up English originals |
| `RevertLocalisation()` | Restores all display fields to their original English values |

**Picker exclusions and type helpers:**

| Member | Description |
|--------|-------------|
| `IsPickerExcluded(id)` | True for internal/reward IDs: `PickerExcludedIds` (season and pirate IDs), `PickerExcludedPrefixes` (`U_TECH`, `SPEC_*`, `TWITCH`, `EXPD`, `OBSOLETE`, etc.) and any `_DMG` variant |
| `IsTechnologyRelatedType(itemType)` | True for `Technology`, `Upgrades`, `Technology Module`, `Constructed Technology`, `Exocraft`, `Starships`, `Others`, where `Category` is a real Technology Category |
| `LoadProceduralStubs()` | Merges `ProceduralStubs` entries into the dictionary without overwriting JSON items |
| `ResolveTechModuleCategories()` | Resolves `TechnologyCategory` for `Technology Module` items from their `DeploysInto` target |
| `BuildCorvetteBasePartTechMap()` | Builds the corvette tech to base-part product mapping |

**Design choice:** Parallel JSON parsing with `Parallel.ForEach` keeps startup fast across
the item database files, which together contain thousands of items.

---

### IconManager

| | |
|---|---|
| File | `Data/IconManager.cs` |
| Purpose | Load, cache, and downscale item icon images for inventory grids |

An `IDisposable` instance class initialized with the path to the icons directory
(`Resources/images`). Icons are downscaled to a maximum dimension of 128 px on load (the
originals are 256x256) using bilinear interpolation, saving roughly 75% memory while
remaining sharp at the 96 px display size used in grids.

`PreloadIcons(database)` loads all database icons in parallel on background threads, capped
at `Environment.ProcessorCount` to avoid GDI+ contention. `GetIcon(filename)` returns the
cached image or loads on demand. `GetIconForItem(itemId, database)` combines database lookup
with icon retrieval.

**Design choice:** Bilinear (not bicubic) interpolation is used because the quality
difference is imperceptible at 128 px and bilinear is measurably faster during bulk preload.

---

### InventoryStackDatabase

| | |
|---|---|
| File | `Data/InventoryStackDatabase.cs` |
| Purpose | Stack size limits per difficulty level and inventory group |

Contains `StackSizeEntry` records organised by 3 difficulty levels (High, Normal, Low).
Each entry specifies `Group` (inventory type), `Product` max stack, and `Substance` max
stack. There are 13 inventory groups per difficulty: Default, Personal, PersonalCargo,
Ship, ShipCargo, Freighter, FreighterCargo, Vehicle, Chest, BaseCapsule,
MaintenanceObject, UIPopup, and SeasonTransfer.

Used by inventory editing to enforce maximum quantities when the user modifies stack
counts. `GetStackSize()` and `GetDefaultStackSize()` resolve limits, `GetMaxAmount()`
computes the save-file `MaxAmount` for an item, and `ResolveInventoryTypeForItem()` /
`CanAddItemToInventory()` decide the save `InventoryType` and whether an item is valid for
a tech-only or cargo inventory.

---

### JsonNameMapper

| | |
|---|---|
| File | `Data/JsonNameMapper.cs` |
| Purpose | Bidirectional translation between obfuscated 3-character JSON keys and human-readable names |

NMS save files use obfuscated keys (e.g. `"F2P"` instead of `"PlayerStateData"`). The
mapper loads a mapping table from `Resources/map/mapping.json`, a JSON file with a
`"Mapping"` array of `{Key, Value}` pairs (currently 1,471 mappings). Legacy tab-separated
mapping files are no longer supported.

| Method | Description |
|--------|-------------|
| `Load(stream)` / `Load(filePath)` | Parse the mapping file |
| `ToName(key)` | Obfuscated -> human-readable (returns key unchanged if unknown) |
| `ToKey(name)` | Human-readable -> obfuscated (returns name unchanged if unknown) |
| `IsObfuscatedKey(key)` | Check if a string is a known obfuscated key |
| `Count` | Total number of loaded mappings |

The mapper is set globally on `JsonParser` via `SetDefaultMapper()` so that all parsed
`JsonObject` instances automatically translate keys during access. When serializing back to
disk, keys are reverse-mapped to their obfuscated form so the game can read the file.

**Design choice:** The mapper is separate from the JSON model so that the same `JsonObject`
code works with both obfuscated and clear-text saves (different platforms use different
formats).

---

### LeveledStatDatabase / BaseStatLimits

| | |
|---|---|
| File | `Data/LeveledStatDatabase.cs`, `Data/BaseStatLimits.cs` |
| Purpose | Stat progression tables and validation ranges for ships, weapons, and freighters |

`LeveledStatDatabase` is currently dormant. It can parse a `LeveledStats.json` array into
`LeveledStat` records (`Name`, `Id`, `Icon`, `IsFloat`, and `LeveledStatLevel` entries
holding value dictionaries per class), but `LoadFromFile()` has no callers and no
`LeveledStats.json` ships with the editor, so the class stays empty.

`BaseStatLimits` is hardcoded. It provides three static dictionaries (`ShipStats`,
`WeaponStats`, `FreighterStats`) mapping entity types and stat IDs to `BaseStatRange`
(min/max), with every range currently `0` to `int.MaxValue`. `ClampStatValue()` and
`ConditionalClampStatValue()` validate user input (the latter preserves an unchanged raw
value), and `GetRange()` exposes the ranges. Used by `StarshipLogic`, `FreighterLogic` and
`MultitoolLogic`. `LoadFromFile()` for a `BaseStatLimits.json` file exists but has no
callers and no such file ships with the editor.

---

### ProceduralStubs

| | |
|---|---|
| File | `Data/ProceduralStubs.cs` |
| Purpose | Lookup data for procedural product items not in the main database |

Some items (prefixed with `PROC_` or `UP_FR`) are procedurally generated and do not appear
in the extractor output. This static class provides 29 hardcoded `Entry` records with `Id`,
`Name`, `Icon`, `Category`, `Subtitle`, and `Description`. Accessed via `Items` (list) or
`ById` (case-insensitive dictionary). `GameItemDatabase.LoadProceduralStubs()` merges them
into the item dictionary without overwriting real JSON items.

---

### RecipeDatabase

| | |
|---|---|
| File | `Data/RecipeDatabase.cs` |
| JSON | `Resources/json/Recipes.json` (1,684 entries) |
| Purpose | Crafting and refining recipe lookup |

An instance class loaded from `Recipes.json`. Stores `Recipe` objects (see
[models.md](models.md)) indexed by result item and by ingredient item for fast bidirectional
lookup.

| Method | Description |
|--------|-------------|
| `LoadFromFile(jsonPath)` | Parse recipes JSON; returns true on success |
| `GetRecipesForResult(itemId)` | What recipes produce this item? |
| `GetRecipesUsingIngredient(itemId)` | What recipes consume this item? |
| `GetRecipe(id)` | Single recipe by ID |
| `GetRefiningRecipes()` | Filter to refining-type recipes |
| `GetCookingRecipes()` | Filter to cooking-type recipes |
| `ApplyLocalisation(service)` / `RevertLocalisation()` | Swap or restore recipe names/types via `RecipeName_LocStr` and `RecipeType_LocStr` |

---

### RewardDatabase

| | |
|---|---|
| File | `Data/RewardDatabase.cs` |
| JSON | `Resources/json/Rewards.json` (734 entries) |
| Purpose | Expedition, Twitch, and platform reward catalog |

A static class with lazy-loaded lists. Loads from `Rewards.json` in the database directory.
Each `RewardEntry` has `Id`, `Name`, `Category` (season / twitch / platform), `Unlock` flag,
`ProductId`, `SeasonId`, `StageId`, `NameLocStr`, and `SubtitleLocStr`. Provides filtered
views: `SeasonRewards`, `TwitchRewards`, `PlatformRewards`, plus `Rewards` and `Count`.

| Method | Description |
|--------|-------------|
| `LoadFromJsonDirectory(dir)` | Loads rewards from `Rewards.json` in the specified directory |
| `ApplyLocalisation(service)` | Swaps reward names with localised values; backs up English originals |
| `RevertLocalisation()` | Restores reward names to English defaults |

---

### SettlementDatabase

| | |
|---|---|
| File | `Data/SettlementDatabase.cs` |
| JSON | `Resources/json/Settlement Perks.json` (90 entries) |
| Purpose | Settlement perk definitions with stat effects, loaded from JSON with localisation support |

This class was renamed from `SettlementPerkDatabase`; it contains 90 `SettlementPerk`
records loaded from `Settlement Perks.json` (both starter and procedural). Each perk has
`Id`, `Name`, `NameLocStr` (from `SETTLEMENTPERKSTABLE.MXML`'s `Name` field),
`Description`, `DescriptionLocStr`, `Beneficial`, `Procedural` and `Starter` flags, and an
array of `PerkStatChange` entries. `PerkStatType` covers Population, Happiness, Production,
Upkeep, Sentinels, Debt, Alert and BugAttack; `PerkStatStrength` covers the positive and
negative strength bands. A `PerkStatRanges` dictionary maps each stat type to integer ranges
per strength level, and `KnownMilestones` lists the empirical building-state values.
Supports `ApplyLocalisation()` / `RevertLocalisation()` for language switching. The class
starts empty and is populated from JSON, with no hardcoded fallback.

---

### SpacePoiTableDatabase

| | |
|---|---|
| File | `Data/SpacePoiTableDatabase.cs` |
| JSON | `Resources/json/Space POI.json` (14 type rules) |
| Purpose | Space POI type rules, counts, and localised display names |

An `internal static` database loaded from `Space POI.json`. Each `SpacePoiTypeRule` (defined
in `Core/SpacePoiSlotInference.cs`) has `Type`, `MinCount`, `MaxCount`,
`ForcedHiddenExtras`, `NormalInitialLevel`, `AllowedInAbandonedSystem`,
`AllowedInEmptySystem`, `NameLocKey`, `Name` and resolved `DisplayName`. The list order is
the game's generation order: normal instances first, then each type's forced hidden extras.
`ApplyLocalisation()` / `RevertLocalisation()` swap and restore display names. When the file
is missing or invalid, `LoadFromFile()` returns false and callers fall back to generic slot
numbers. Consumed by `UI/Panels/SpaceStationPanel.cs`.

---

### SpacePoiLayoutCache

| | |
|---|---|
| File | `Data/SpacePoiLayoutCache.cs` |
| Purpose | Remember inferred space POI slot layouts per system so labels survive later edits |

An `internal static` cache keyed by system address (UA). Layout tokens are remembered in
`space-poi-layouts.json` inside the `Discoveries` folder next to the executable, managed
by `Config/DiscoveriesStore.cs` (atomic writes, versioned format). `Get(ua)` returns the
remembered tokens, `Remember(ua, tokens)` merges freshly inferred tokens without
downgrading known values, and `SaveIfDirty()` writes the file when anything changed.
`ImportLegacyLayouts()` performs the one-time migration of versioned
`spacepoi.layout.v2.*` properties out of `NMSE.conf`. Consumed by
`UI/Panels/SpaceStationPanel.cs`.

---

### StarshipDatabase / ShipCustomisationDatabase

| | |
|---|---|
| File | `Data/StarshipDatabase.cs` |
| JSON | `Resources/json/Ship Customisation.json` (5 configs) |
| Purpose | Corvette optimiser sort data and starship customisation slot data |

`StarshipDatabase` (internal static) holds corvette part category and sort data for the
optimiser: reactors, thrusters, wings/boosters, landing gears, then landing bays and
cockpits preserving original save order, and everything else (`int.MaxValue`) sorted
alphabetically by display name. It exposes `GetOptimizerPriority()`, `GetSubOrder()`,
`GetDisplayName()`, `GetPartDisplayName()` and `LoadFromDatabase()` (stores a
`GameItemDatabase` reference for name fallbacks). A hardcoded 2,136-entry `DisplayNameMap`
matches the in-game sorting order. Consumed by `StarshipLogic`.

`ShipCustomisationDatabase` (same file, public static) loads `Ship Customisation.json` at
startup. It exposes `AllConfigs`, `AllResourcePaths` and `GetConfigByResource()` for ship
model customisation slots, texture groups and palette IDs. Consumed by `StarshipPanel`.

---

### TechAdjacencyDatabase

| | |
|---|---|
| File | `Data/TechAdjacencyDatabase.cs` |
| Purpose | Technology adjacency/synergy grouping data for inventory grid rendering |

A static dictionary mapping item IDs (without `^` prefix) to `TechAdjacencyInfo` records.
Each record has `BaseStatType` (adjacency group ordinal), `TechnologyCategory` (category
ordinal), and `LinkColourHex` (synergy border colour, e.g. `"#44FDFF"`).

Items with the same `BaseStatType` value are considered adjacent and receive coloured
synergy borders in the inventory grid. Contains 802 entries.

---

### TechPacks

| | |
|---|---|
| File | `Data/TechPackDatabase.cs`, `Data/TechPackDatabase.Generated.cs` |
| Purpose | Predefined technology packs with unique hash keys |

A static partial class named `TechPacks` (renamed from `TechPackDatabase`). Its `Dictionary`
maps hash strings to `TechPack` records. Each pack has `Id` (e.g. `"^T_TOX"`), `Hash`
(unique hash like `"^808001BC85F7"`), `Icon` filename, and `Class` (C/B/A/S/NONE). The base
file contains 736 entries.

`Data/TechPackDatabase.Generated.cs` is written by the extractor and regenerated against each
game version. It currently contains an empty `_generatedPacks` dictionary, a
`RegisterGeneratedPacks()` method that merges any discovered packs into `Dictionary` at
startup, and a 328-entry `TechCatalogEntry` catalog (`Id`, `IconPath`, `Category`,
`InferredClass`, `IsUpgrade`, `IsCore`, `IsProcedural`) with `GetTechCatalogEntry()` for
looking up icon paths and class info for new packs.

---

### TitleDatabase

| | |
|---|---|
| File | `Data/TitleDatabase.cs` |
| JSON | `Resources/json/Titles.json` (346 entries) |
| Purpose | Player titles loaded from JSON with localisation support |

Contains 346 `TitleEntry` records loaded from `Titles.json`. Each entry has `Id`, `Name`
(with `{0}` placeholder for the player's name), `NameLocStr`, `UnlockDescription`,
`UnlockDescriptionLocStr`, `AlreadyUnlockedDescription`, `AlreadyUnlockedDescriptionLocStr`,
`UnlockedByStat`, and `UnlockedByStatValue`. Supports `ApplyLocalisation()` /
`RevertLocalisation()` for language switching. The `{0}` placeholder is preserved across all
localised translations. The class starts empty and is populated from JSON, with no hardcoded
fallback; the retained `InitializeStaticTitles()` method is a no-op.

---

### WikiGuideDatabase

| | |
|---|---|
| File | `Data/WikiGuideDatabase.cs` |
| JSON | `Resources/json/Wiki Guide.json` (58 entries) |
| Purpose | Wiki guide topics loaded from JSON with localisation support |

Contains 58 `WikiGuideTopic` records loaded from `Wiki Guide.json`. Each topic has `Id`
(with `^` prefix), `Name`, `NameLocStr` (the topic's localisation key from `WIKI.MXML`),
`Category`, `CategoryLocStr`, and `IconKey` (extracted from the MXML's `Icon > Filename`
texture path, e.g. `"SURVIVALBASICS"`). Topics are grouped into categories (Survival
Basics, Getting Around, Making Discoveries, etc.) with localised category names. Supports
`ApplyLocalisation()` / `RevertLocalisation()` for language switching.
`GetEnglishCategory(topicId)` returns the original English category (from backup when
localisation is active) for stable grid placement regardless of active language. The class
starts empty and is populated from JSON, with no hardcoded fallback.

---

### WordDatabase

| | |
|---|---|
| File | `Data/WordDatabase.cs` |
| JSON | `Resources/json/Words.json` (2,151 entries) |
| Purpose | Alien word translations for language knowledge management |

An instance class loaded from `Words.json` (produced by NMSE.Extractor from the game's
`NMS_DIALOG_GCALIENSPEECHTABLE.MXML`). Contains 2,151 `WordEntry` records. Each entry has
`Id`, `Text`, a `TextLocStr` key for localisation lookups (derived from the first group key
by race ordinal, e.g. `"TRA_ABOMINATION"` for word `"ABOMINATION"`), and a `Groups`
dictionary mapping group names to race ordinals. Race mapping: 0=Gek, 1=Vy'keen, 2=Korvax,
4=Atlas, 8=Autophage. Entries are sorted alphabetically by display text after loading; the
`TextLocStr` allows looking up the translated word text from per-language JSON files (e.g.
Japanese `"嫌悪"` for `"abomination"`).

| Method | Description |
|--------|-------------|
| `LoadFromFile(jsonPath)` | Loads word data from Words.json |
| `ApplyLocalisation(service)` | Swaps word text with localised values; backs up English originals. Tries the primary `TextLocStr` key first, then falls back to all group keys (stripped of `^` prefix) if the primary lookup returns untranslated English text. This handles cases like `"TRA_ACCESS"` returning `"access"` while `"BUI_ACCESS"` contains the actual Japanese `"アクセス"`. |
| `RevertLocalisation()` | Restores word text to English defaults |

Used by the Catalogue panel to display and edit learned alien words.

---

### CatalogueDatabase / CatalogueKnownValues

| | |
|---|---|
| File | `Data/CatalogueDatabase.cs` |
| JSON | `Resources/json/Catalogue Pack.json` (7 sections) |
| Purpose | Catalogue completion data for the Catalogue panel |

An `internal sealed` instance class constructed with the `Resources/json` directory
(`CataloguePanel.SetCatalogueDatabase`). Candidate completion values are derived from
`Catalogue Pack.json`, which contains the `KnownRefinerRecipes`, `KnownWordGroups`,
`Fossils`, `KnownPortalRunes`, `CatalogueMaterials`, `CatalogueBuilding` and
`CatalogueCrafting` sections. `HasDerivedData` reports whether that file is present;
`IsAvailable` is always true because the compiled fallbacks in `CatalogueKnownValues` are
always present.

Exposed data includes `KnownProducts`, `KnownTech`, `KnownSpecials`,
`KnownRefinerRecipes`, `KnownWordGroups`, `Fishing`, `KnownPortalRunes`, `Fossils`,
`SeenSubstances`, `DiscoveryStats`, `StoryCompleters`, `WonderFallbackSlots`,
`WonderTreasureRecords` and `WonderWeirdBasePartRecords`.

`CatalogueKnownValues` (same file) holds editor-known values that cannot be derived from the
game files: `PortalRunes` (65,535 for a complete save), residual product/tech/special ID
lists, fishing species with pack counts and largest catches, word groups, discovery stat
targets, story completer data (Base Computer maximum, SavedInteractionIndicies patches and
lore missions), account Seen lists, and baked wonder slot and treasure JSON.

---

### UiStrings

| | |
|---|---|
| File | `Data/UiStrings.cs` |
| JSON | `Resources/ui/lang/*.json` (16 BCP 47 files) |
| Purpose | Editor UI string localisation, separate from game string localisation |

A static class that loads dot-delimited key tables such as `"menu.file"` or `"tab.player"`
from `Resources/ui/lang/`. `en-GB.json` is always loaded as the fallback (currently 2,238
keys), then the selected language file is overlaid. `Get(key)` falls back to English and
then returns the raw key, which makes missing translations easy to spot;
`GetOrNull(key)` distinguishes a missing key from an empty value. `Format(key, args)`
substitutes arguments using `InvariantCulture` and returns the unformatted template if
placeholders do not match.

| Member | Description |
|--------|-------------|
| `SetDirectory(dir)` | Sets the `Resources/ui/lang` directory |
| `Load(bcp47Tag)` | Loads the English fallback plus the requested language; null or `"en-GB"` selects English |
| `Get(key)` / `GetOrNull(key)` | Localised lookup with English fallback |
| `Format(key, args)` | Formatted localised string |
| `TranslatedCount` | Number of strings in the active translation (0 for English) |
| `TotalKeyCount` | Number of keys in the English fallback |
| `IsActive` | Whether a non-English UI language is loaded |

These files are editor-authored and are not produced by the extractor, unlike the game
language dumps in `Resources/json/lang/`. Accelerator ampersands (`&`) are preserved for
WinForms.

---

### LocalisationService

| | |
|---|---|
| File | `Data/LocalisationService.cs` |
| Purpose | Per-language game string lookups for localised item display |

An instance class that loads one language JSON file at a time from the
`Resources/json/lang/` directory. Each file (e.g. `ja-JP.json`) is a flat
`Dictionary<string, string>` mapping NMS localisation keys (like `"UI_FUEL_1_NAME"`) to
translated strings.

Language JSON files use **unescaped UTF-8** so characters display as natural language text
(e.g. `"炭素"` not `"\u70AD\u7D20"`). The `LocalisationBuilder` serialises with
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to achieve this.

`SupportedLanguages` is a static dictionary of 16 NMS language names to IETF BCP 47 tags:
en-GB, fr-FR, it-IT, de-DE, es-ES, ru-RU, pl-PL, nl-NL, pt-PT, es-419, pt-BR, zh-CN,
zh-TW, ko-KR, ja-JP, en-US. (TencentChinese excluded.)

| Method | Description |
|--------|-------------|
| `SetLangDirectory(dir)` | Sets the `lang/` directory path |
| `LoadLanguage(bcp47Tag)` | Loads a language file; pass `null` to revert to English defaults |
| `Lookup(locKey)` | Raw key lookup; returns null if not found or no language active |
| `GetName(item)` | Localised name via `NameLocStr`, with `{LocStr}_NAME` and `{LocStr}1_NAME` fallbacks, then `item.Name` |
| `GetDescription(item)` | Localised description via `DescriptionLocStr`, falls back to `item.Description` |
| `GetSubtitle(item)` | Localised subtitle via `SubtitleLocStr`, falls back to `item.Subtitle` |
| `ActiveLanguageTag` | Currently active BCP 47 tag (null when using defaults) |
| `IsActive` | Whether a non-default language is loaded |

**Language switching flow:** `AppConfig.Instance.Language` (default `"en-GB"`) is the single
source of truth. `ApplyStartupLanguage()` runs after `LoadDatabase()` and applies the saved
language without the loading dialog; selecting a language from the Language menu runs the
same sequence with a progress dialog and then persists the choice.

1. `UiStrings.Load(tag)` loads the editor UI string table, always loading `en-GB.json` as the
   fallback first.
2. `LocalisationService.LoadLanguage(tag)` loads the game string dump.
3. If the language file is missing, `LoadLanguage(null)` reverts to English and
   `RevertLocalisation()` is called on `GameItemDatabase`, `RewardDatabase`, `WordDatabase`,
   `RecipeDatabase`, `TitleDatabase`, `FrigateTraitDatabase`, `SettlementDatabase`,
   `WikiGuideDatabase`, and `SpacePoiTableDatabase`. `CompanionAccessoryDatabase` is not
   reverted in this branch, so accessory names keep their last localised values until the
   next apply or revert. (On startup, a missing language file simply leaves the English
   defaults in place.)
4. If loaded, `ApplyLocalisation()` is called on the databases listed above plus
   `CompanionAccessoryDatabase` and `SpacePoiTableDatabase`.
5. The UI refreshes: `RefreshLoadedPanels()`, `AccountPanel.RefreshRewardNames()`,
   `RecipePanel.RefreshLanguage()`, `FrigatePanel.RefreshTraitCombos()`,
   `SettlementPanel.RefreshPerkCombos()` and `ApplyUiLocalisation()`.
6. `AppConfig.Instance.Language` is saved so the selection is remembered on next startup.

**Design choice:** All internal editor logic runs against the default English values from the
DB JSON files. The in-place swap approach is used for display: when localisation is active,
display fields (Name, Description, Subtitle) are overwritten with localised strings. When
reverted, the original English values are restored from backup. This avoids modifying panel
code while keeping string comparison logic safe (comparisons use IDs, not display names).

---

## Dependency Graph

```
GameItemDatabase  <--  IconManager (icon filenames from items)
                  <--  inventory panels (item lookup for pickers)
                  <--  StarshipDatabase.LoadFromDatabase (display name fallback)
                  <--  Catalogue/Settlement/Base/Account panels (item metadata)

RecipeDatabase    <--  RecipePanel, CataloguePanel (recipe display)
RewardDatabase    <--  AccountPanel (reward unlock management)
WordDatabase      <--  Catalogue panel (known words)

JsonNameMapper    <--  JsonParser (global default mapper)
                  <--  JsonObject (per-instance mapper for key translation)

CompanionDatabase          <--  CompanionPanel (species lookup)
CreaturePartDatabase       <--  CompanionPanel (part/descriptor editor)
CompanionAccessoryDatabase <--  CompanionPanel (accessory slots)
PetBattleMoveDatabase / PetBattleMovesetDatabase / PetBiomeAffinityMap
                           <--  CompanionPanel (pet battle editor)

FrigateTraitDatabase <--  FrigatePanel
SettlementDatabase   <--  SettlementPanel
TitleDatabase        <--  MainStatsPanel
WikiGuideDatabase    <--  MainStatsPanel
CatalogueDatabase    <--  CataloguePanel (completion tabs)

SpacePoiTableDatabase / SpacePoiLayoutCache <-- SpaceStationPanel

TechAdjacencyDatabase <-- InventoryGridPanel (synergy border rendering)
TechPacks             <-- InventoryGridPanel (hash-based tech lookup)

StarshipDatabase          <-- StarshipLogic (corvette optimiser sort)
ShipCustomisationDatabase <-- StarshipPanel

BaseStatLimits    <--  StarshipLogic, FreighterLogic, MultitoolLogic (stat clamping)
UiStrings         <--  menus, panels and dialogs (UI string localisation)
LocalisationService <-- MainForm (applies localisation to the databases above)
LeveledStatDatabase - currently unused (no callers)
```
