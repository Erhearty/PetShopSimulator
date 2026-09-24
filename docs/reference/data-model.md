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
<!-- generated:end comp:local-save-file -->
