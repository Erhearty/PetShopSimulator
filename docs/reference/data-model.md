<!-- generated:start cap:data-model-intro -->
# Data Model

Projected from `schema` widgets on the architecture canvas.
<!-- generated:end cap:data-model-intro -->

<!-- generated:start comp:local-save-file -->
## Local Save File (`local-save-file`)

### Database Table Schema

| Field | Type | Flags | Notes |
|---|---|---|---|
| `Version` | int | - | Save format version; saves below current version are discarded, not migrated |
| `Balance` | float | - | - |
| `Reputation` | float | - | - |
| `Day` | int | - | - |
| `Staff` | int | - | - |
| `PriceMultiplier` | float | - | - |
| `SavedAt` | string | - | ISO-8601 UTC timestamp |
| `Stock` | StockEntry[] | - | id + qty pairs, shelf stock |
| `Warehouse` | StockEntry[] | - | delivered units still in the stockroom, keyed by category |
| `PlacedObjects` | PlacedItem[] | - | catalogId, cellX/cellY, variant, rotation, shelfStock[], pets[] (PetSaveData) |
| `StaffList` | StaffSaveData[] | - | Save v4+: one record per assistant - name, role, skill (int), wage (float), serviceSeconds (float). Empty in pre-v4 saves; SaveMigrator (v3→v4 step) normalises it. Staff int head count is kept alongside it. |
| `SlotFile` | string |  | petshop_save_{1..3}.json; one SaveData per slot |
| `PetSaveData.id` | string |  | stable pet id (short GUID) |
| `PetSaveData.parentAId / parentBId` | string |  | empty for founders |
| `PetSaveData.generation` | int |  | 0 for bought/starter pets |
| `Lineage` | List<LineageEntry> |  | every pet ever owned (id, name, species, rarity, coat rgb, parents, generation, bornDay) so trees survive sales |
| `PetSaveData.ribbons` | int |  | pet show placings won (0-3 count toward price) |
| `ShowEntryPetId` | string |  | pet entered in the next show; empty if none |
| `AutoReorder` | List<ReorderRule> |  | per category: category, enabled, threshold, units |
| `ProgressionTier` | int |  | Highest reputation tier reached; v2 saves migrate from Reputation |
| `EventSeed` | int |  | Seed for deterministic daily event rolls |
| `ActiveEventId` | string |  | None\|SupplierSale\|Heatwave\|StreetFestival |
| `EventDaysLeft` | int |  | Days remaining for the active event |
| `CurrentVersion` | int | - | = 5 (SaveMigrator.cs). SaveMigrator.Migrate upgrades older saves 0->5 in memory via a step chain; they are not discarded |
| `FurnitureInventory` | List<StockEntry> | - | Owned unplaced furniture |
| `PendingFurnitureOrders` | List<FurnitureOrderSave> | - | catalogId, arrivalProgress, arrived |
| `Quests` | QuestProgress | - | CompletedIds, Chapter, Flags |
| `LifetimeRevenue / BestDayProfit` | number | - | Lifetime stats |
| `SpeciesSold / PetsBred / InspectionsPassed / BestInspectionGrade` | stats | - | Lifetime stats |
| `PlacedItem.footprintRotated` | bool | - | Furniture footprint rotation |
| `PetSaveData (pet fields)` | object | - | species, petName, growthStage, rarity, coat_r/g/b, temperament, energyLevel, friendliness, ageDays, daysToMature, basePrice |
| `Lineage` | List<LineageEntrySave> | - | Corrects earlier type |
| `AutoReorder` | List<ReorderRuleSave> | - | Corrects earlier type |
| `ActiveEventId` | string | - | Defaults to "None"; values come from ShopEventKind |
<!-- generated:end comp:local-save-file -->
