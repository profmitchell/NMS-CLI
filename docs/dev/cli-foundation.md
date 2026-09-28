# NMS CLI Foundation (Phase 1)

This fork adds a new executable project (`NMS.CLI`) while keeping NMSE's existing GUI and save pipeline unchanged.

## Project Structure

- `NMS.CLI/NMS.CLI.csproj` (new executable)
- Project reference to `NMSE.csproj` for shared IO/core logic
- GUI remains in `NMSE.csproj` (WinForms), unmodified

## Phase 1 Commands

- `nms save list [--dir <path>]`
- `nms save info --slot <number> [--dir <path>]`

The CLI reads save metadata through NMSE's existing classes (`SaveFileManager`, `SaveSlotManager`, `ContainersIndexManager`, `MemoryDatManager`) and does not reimplement save parsing, compression, encryption, or metadata formats.

## GUI-to-Logic Mapping Used for CLI Planning

### Loading / Saving
- `UI/MainForm.cs:LoadSaveData` -> `IO/SaveFileManager.LoadSaveFile`
- `UI/MainForm.cs:OnSave` -> `IO/SaveFileManager.SaveToFile`
- `UI/MainForm.cs:LoadXboxSaveData` -> `IO/SaveFileManager.LoadXboxSave`
- `UI/MainForm.cs:LoadPS4MemoryDatSaveData` -> `IO/SaveFileManager.LoadPS4MemoryDatSave`

### Backups
- `UI/MainForm.cs:OnSave` -> `IO/SaveFileManager.BackupSaveDirectory`
- `UI/MainForm.cs:OnSave` -> `IO/SaveFileManager.ResolveBackupRoot`
- Restore paths use `IO/SaveFileManager.FindBackupZips`

### Known Tech / Products / Recipes / Base Parts
- `UI/Panels/CataloguePanel.cs` add-all actions -> `Core/CatalogueCompletionLogic.GetCompletion`
- `UI/Panels/CataloguePanel.cs` add-all actions -> `Core/CatalogueCompletionLogic.AddMissingIds`

### Catalogue Completion
- `UI/Controls/CompletionGridPanel.cs` -> `Core/CatalogueCompletionLogic.GetCompletion`
- `UI/Panels/WondersCompletionPanel.cs` -> `Core/CatalogueCompletionLogic.GetCompletion`
- `UI/Panels/KnowledgeCompletionPanel.cs` -> `Core/CatalogueCompletionLogic.GetWordGroupCompletion`

### Discoveries / Glyphs / Words
- `UI/Panels/CataloguePanel.cs` word actions -> `Core/CatalogueCompletionLogic.AddMissingWordGroups`
- `UI/Panels/CataloguePanel.cs` glyph handling -> `Core/CatalogueLogic.LoadGlyphBitfield` / `SaveGlyphBitfield`

### Reward Synchronization
- `UI/Panels/AccountPanel.cs` reward save flow -> `Core/AccountLogic.SaveRedeemedRewards`
- `UI/Panels/AccountPanel.cs` reward save flow -> `Core/AccountLogic.SyncKnownArraysForChangedRewards`

### Space Station “Set Claimable Values”
- `UI/Panels/SpaceStationPanel.cs:OnSetClaimableValues` -> `Core/SpaceStationLogic.ApplyClaimableMinimums`
