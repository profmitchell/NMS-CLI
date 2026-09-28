# Models

## Overview

The Models layer (`Models/`) defines the domain types used across the application.

The working model for save data is the custom JSON tree: `JsonObject` and `JsonArray`.
Every panel reads and writes saves through this tree, with `JsonParser` and `JsonReader`
providing parsing and serialisation and `JsonNameMapper` handling obfuscated keys.

The repository also contains a set of typed entity wrapper classes (`Ship`, `Multitool`,
`Frigate`, `Companion`, `Inventory`) and a few enums. These are **currently unreferenced
by application code** (and by tests): the wrappers have no instantiations anywhere, and
the panels work on raw `JsonObject` nodes through the Logic classes. They are documented
below as legacy/reserved so the files are not mistaken for the active API. Confirm before
relying on them for new work.

---

## Custom JSON Model

### Why Not System.Text.Json?

The NMS save format has requirements that standard .NET JSON libraries do not satisfy:

1. **Field order matters.** The game may depend on property ordering; reordering fields
   during round-trip could corrupt saves.
2. **Binary data in strings.** Some JSON string values contain raw bytes >= 0x80. These
   are preserved as `BinaryData` objects using Latin-1 encoding.
3. **Exact numeric round-trip.** The game writes floats like `0.30000001192092898`.
   .NET's `double.ToString` might produce `0.30000001192092896` (same IEEE 754 bits,
   different text). `RawDouble` preserves the original text.
4. **Name mapping.** Obfuscated 3-character keys must be translated transparently.
5. **Path-based access with transforms.** `GetValue("PlayerStateData.Health")` must
   resolve through registered context transforms.

`System.Text.Json` is still used for the editor's own configuration/data files (settings,
export config, Discoveries files). Those serialisers are source-generated through
`Core/AppJsonContext.cs` so the trimmed Native AOT build keeps the required metadata.

### JsonObject

| | |
|---|---|
| File | `Models/JsonObject.cs` |
| Purpose | Mutable JSON object with ordered key-value pairs, path access, transforms, and deep cloning |

Stores entries as an ordered list of name-value pairs. Builds a `Dictionary<string, int>`
index lazily when the object has more than 8 properties, giving O(1) key lookup for large
objects while avoiding dictionary overhead for small ones.

| Method | Description |
|--------|-------------|
| `Parse(string)` | Static factory; parses a JSON string into a `JsonObject` |
| `Add(name, value)` | Add with duplicate-key check |
| `AddUnchecked(name, value)` | Fast add without validation (parser hot path) |
| `Set(name, value)` | Set or add |
| `Get(name)` | Get value by name |
| `Contains(name)` | Check if key exists |
| `Remove(name)` | Remove entry |
| `Names()` | All property names (ordered) |
| `Size` | Number of entries |
| `Rename(oldName, newName)` | Rename a key while preserving order |
| `Reorder(name, newIndex)` | Move an entry to a new index |
| `Clear()` | Remove all entries |
| `RemapKeys(map)` | Bulk-rename keys through a mapping |
| `GetObject(name)` | Type-safe: returns `JsonObject?` |
| `GetArray(name)` | Type-safe: returns `JsonArray?` |
| `GetString(name)` | Type-safe: returns `string?` |
| `GetInt(name)` / `GetLong` / `GetDouble` / `GetFloat` / `GetBool` / `GetDecimal` | Numeric getters with automatic `RawDouble` unwrapping |
| `GetDoubleText(name)` | Original JSON text of a numeric value |
| `GetValue(path)` | Resolve dotted paths like `"PlayerStateData.Health"` with transform support |
| `RegisterTransform(key, func)` | Register a dynamic path resolution function |
| `DeepClone()` | Recursive deep clone |
| `ExportToFile(path)` / `ImportFromFile(path)` | Write/read a node as a standalone JSON file |
| `ToString()` / `ToFormattedString()` | Serialise to a compact / formatted JSON string |
| `ToDisplayString()` / `ToLines()` | Serialise for the Raw JSON Editor (text / line array) |

**Properties:** `Length`, `Parent` (internal), `Listener` (optional
`IPropertyChangeListener`), `NameMapper` (optional `JsonNameMapper`).

> `Listener` exists for a future change-notification system but is never assigned or
> invoked in the current codebase.

---

### JsonArray

| | |
|---|---|
| File | `Models/JsonArray.cs` |
| Purpose | Mutable JSON array with type-safe accessors and deep cloning |

Backed by a list. Provides indexed access with type-safe getters that mirror
`JsonObject`.

| Method | Description |
|--------|-------------|
| `Parse(string)` | Static factory; parses a JSON string into a `JsonArray` |
| `Add(value)` | Append with type validation |
| `AddUnchecked(value)` | Fast append for parser hot path |
| `Insert(index, value)` | Insert at position |
| `Get(index)` / `Set(index, value)` | Indexed access |
| `RemoveAt(index)` | Remove and shift |
| `Clear()` | Remove all elements |
| `IndexOf(value)` | Find first occurrence |
| `GetObject(i)` / `GetArray(i)` / `GetString(i)` / `GetInt(i)` / ... | Type-safe indexed accessors |
| `GetDoubleText(i)` | Original JSON text of a numeric element |
| `DeepClone()` | Recursive deep clone |
| `ToString()` / `ToFormattedString()` | Serialise to a compact / formatted JSON string |

**Properties:** `Length`, `Parent` (internal), `Listener` (optional; unused).

---

### JsonParser

| | |
|---|---|
| File | `Models/JsonParser.cs` |
| Purpose | JSON parsing and serialisation with NMS-specific features |

A static class that provides the parsing engine. Key features:

- **Key interning pool** -- deduplicates repeated key strings across all parsed objects,
  reducing memory usage significantly for large saves with thousands of identical keys.
- **Lazy line/column tracking** -- position is only computed on error (via the private
  `JsonReader.ComputeLine` / `JsonReader.ComputeColumn` helpers), avoiding
  per-character overhead on the hot path.
- **Obfuscated key mapping** -- when a global `JsonNameMapper` is set via
  `SetDefaultMapper`, all parsed objects automatically translate keys.
- **BinaryData support** -- strings containing high bytes become `BinaryData` instances.
- **RawDouble preservation** -- floating-point numbers store both the parsed `double` and
  the original text.

| Method | Description |
|--------|-------------|
| `SetDefaultMapper(mapper)` | Set the global name mapper for all future parsing |
| `GetDefaultMapper()` | Retrieve the current global mapper |
| `Parse(string)` | Parse into a `JsonObject` |
| `ParseValue(string)` / `ParseObject(string)` / `ParseArray(string)` | Typed parse entry points |
| `Serialize(value, formatted, skipReverseMapping)` | Serialise any JSON value to string |
| `SerializeToLines(value, ...)` | Memory-efficient line serialisation used by the diff path |
| `QuoteString(...)` | JSON string quoting helper |

---

### JsonReader

| | |
|---|---|
| File | `Models/JsonReader.cs` |
| Purpose | High-performance character-level JSON reader |

A sealed class that wraps a source string and provides character-by-character reading.
Optimised for speed: no per-character line/column tracking. `Line` and `Column`
properties are computed on demand from the current `Position` by the private
`ComputeLine` / `ComputeColumn` helpers.

| Method | Description |
|--------|-------------|
| `Source` / `Position` | The underlying string and current offset |
| `Read()` | Read next character, advance position |
| `Peek()` | Look ahead without advancing |
| `Advance(newPos)` | Jump to a position |
| `ReadSkipWhitespace()` | Read next non-whitespace (also skips null bytes) |
| `ReadDigit()` / `ReadDigitOrSign()` | Read digits, optionally with a sign |
| `ReadIf(expected)` | Instance conditional single-char read |
| `ReadIf(reader, predicate)` | Static conditional read with a predicate |
| `ReadIfEither(a, b)` | Conditional read for two alternatives |
| `Unread()` | Step back one character |
| `Dispose()` | Release the reader |

---

### JsonException

| | |
|---|---|
| File | `Models/JsonException.cs` |
| Purpose | JSON parsing/serialisation error with optional position information |

Extends `Exception` with `Line` and `Column` properties (0 if unknown). Three
constructor overloads support message-only, message with position, and message with
inner exception and position.

---

## Value Types

### BinaryData

| | |
|---|---|
| File | `Models/BinaryData.cs` |
| Purpose | Wrapper for binary data found in JSON strings |

NMS JSON sometimes contains strings with bytes >= 0x80. Since JSON is text, these bytes
are preserved by loading with Latin-1 encoding and wrapping in `BinaryData`. The class
provides `ToByteArray()`, `IndexOf(byte)`, `Substring(start, len)` (Latin-1 decoded),
`ToHexString()`, and value-based equality.

---

### RawDouble

| | |
|---|---|
| File | `Models/RawDouble.cs` |
| Purpose | Double-precision float with original JSON text for lossless round-trip |

A readonly struct with `Value` (the parsed `double`) and `Text` (the original JSON text).
`ToString()` returns `Text`, not a reformatted number. An implicit conversion to `double`
allows arithmetic. This prevents unnecessary diffs when saving: the game might write
`0.30000001192092898` but `double.ToString` would produce a slightly different decimal
representation of the same IEEE 754 bits.

---

### IPropertyChangeListener

| | |
|---|---|
| File | `Models/IPropertyChangeListener.cs` |
| Purpose | Reserved observer interface for JSON property changes |

Single method: `PropertyChanged(path, oldValue, newValue)`. Can be attached to
`JsonObject` or `JsonArray` via the `Listener` property, but nothing in the current
codebase implements the interface or assigns a listener; treat it as reserved for future
dirty-state/undo work.

---

## Entity Wrapper Classes (legacy / unused)

These classes provide typed accessors over `JsonObject` instances from the save file.
They do not copy data; they hold a reference to the underlying JSON node and read/write
through it.

> **Current status:** none of the wrapper classes are instantiated by application or test
> code. Panels use raw `JsonObject` nodes and the Core Logic classes instead. The tables
> below are retained as reference for their JSON key mappings.

### Ship

| | |
|---|---|
| File | `Models/Ship.cs` |
| Purpose | Wrapper for a player-owned starship |

| Property | Type | JSON Key |
|----------|------|----------|
| `Index` | int | (constructor arg) |
| `Name` | string | `"Name"` |
| `Seed` | string? | `"Seed"` |
| `Type` | `ShipType` | `"ShipType"` (enum-parsed) |
| `Class` | `ShipClass` | `"ShipClass"` (defaults to C) |
| `Inventory` | `JsonObject?` | `"Inventory"` |
| `TechInventory` | `JsonObject?` | `"Inventory_TechOnly"` |
| `CargoInventory` | `JsonObject?` | `"Inventory_Cargo"` |
| `Data` | `JsonObject` | (underlying node) |

---

### Multitool

| | |
|---|---|
| File | `Models/Multitool.cs` |
| Purpose | Wrapper for a player-owned multitool weapon |

| Property | Type | JSON Key |
|----------|------|----------|
| `Index` | int | (constructor arg) |
| `Name` | string | `"Name"` (defaults to `"Multitool {Index+1}"`) |
| `Seed` | string? | `"Seed"` |
| `Type` | `MultitoolType` | `"MultitoolType"` (enum-parsed) |
| `Class` | `ShipClass` | `"MultitoolClass"` (defaults to C) |
| `Inventory` | `JsonObject?` | `"Inventory"` |
| `Data` | `JsonObject` | (underlying node) |

---

### Frigate

| | |
|---|---|
| File | `Models/Frigate.cs` |
| Purpose | Wrapper for a fleet frigate |

| Property | Type | JSON Key |
|----------|------|----------|
| `Index` | int | (constructor arg) |
| `IsEnabled` | bool | `"Enabled"` |
| `NpcType` | string? | `"Type"` |
| `ShipType` | string? | `"ShipType"` |
| `Rank` | string? | `"Rank"` |
| `Data` | `JsonObject` | (underlying node) |

---

### Companion

| | |
|---|---|
| File | `Models/Companion.cs` |
| Purpose | Wrapper for a companion creature (pet) |

| Property | Type | JSON Key |
|----------|------|----------|
| `Index` | int | (constructor arg) |
| `Type` | string? | `"Type"` |
| `Seed` | string? | `"Seed"` |
| `Data` | `JsonObject` | (underlying node) |

---

### Inventory

| | |
|---|---|
| File | `Models/Inventory.cs` |
| Purpose | Wrapper for an inventory grid |

| Property | Type | JSON Key |
|----------|------|----------|
| `Type` | `InventoryType` | (constructor arg) |
| `Width` | int | `"Width"` |
| `Height` | int | `"Height"` |
| `Slots` | `JsonArray?` | `"Slots"` |
| `ValidSlotIndices` | `JsonArray?` | `"ValidSlotIndices"` |
| `Data` | `JsonObject` | (underlying node) |

---

## Recipe / RecipeElement

| | |
|---|---|
| File | `Models/Recipe.cs` |
| Purpose | Crafting/refining recipe with ingredients and result |

`Recipe` properties: `Id`, `Category`, `RecipeType`, `RecipeName`, `RecipeNameLocStr`,
`RecipeTypeLocStr`, `Result` (RecipeElement?),
`Ingredients` (RecipeElement[]), `TimeToMake` (seconds), `Cooking` (bool).

`RecipeElement` properties: `Id` (item ID), `Type` (inventory type), `Amount`
(quantity).

Unlike the wrapper classes above, `Recipe` and `RecipeElement` are actively used by
`Data/RecipeDatabase.cs` and the Catalogue panel.

---

## SaveFileMetadata

| | |
|---|---|
| File | `Models/SaveFileMetadata.cs` |
| Purpose | File-level metadata about a save (not in-save metadata) |

Properties: `Version`, `PlayTime` (seconds), `IsCompressed`, `Name`, `Description`,
`PlayTimeFormatted` (computed `H:MM:SS` or `MM:SS`). Provides `Clone()` for shallow copy.

> This class currently has no references outside its own file; it is effectively unused
> and reserved for future save-metadata work.

---

## Enums

| Enum | File | Values |
|------|------|--------|
| `ShipType` | `Models/ShipType.cs` | Shuttle, Hauler, Fighter, Explorer, Exotic, Freighter, LivingShip, Solar, Robot, Interceptor, Titan, Ironclad, Unknown |
| `ShipClass` | `Models/ShipClass.cs` | C, B, A, S |
| `MultitoolType` | `Models/MultitoolType.cs` | Standard, Royal, Sentinel, Switch, Staff, Atlas, Rifle, Pistol, Unknown |
| `InventoryType` | `Models/InventoryType.cs` | Personal, PersonalTech, PersonalCargo, Ship, ShipTech, ShipCargo, Freighter, FreighterTech, Vehicle, VehicleTech, Weapon, WeaponTech |
| `DifficultyLevel` | `Models/DifficultyLevel.cs` | Normal, Survival, Permadeath, Creative, Custom, Relaxed, Hardcore |

> `ShipClass` is used by the active Logic classes. `ShipType`, `MultitoolType`,
> `InventoryType`, and `DifficultyLevel` are currently referenced only by the unused
> wrapper classes (and, for `DifficultyLevel`, a test), because the active panels resolve
> types through the string/dictionary lookups in `StarshipLogic`, `MultitoolLogic`, and
> the Data layer. They are kept as reserved typed APIs.
