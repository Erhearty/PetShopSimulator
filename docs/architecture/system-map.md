<!-- generated:start file:system-map -->
# System Map

```mermaid
graph TD
    build-editor-tooling["Build & Editor Tooling <br/> <small>(CUSTOM)</small>"]
    local-save-file["Local Save File <br/> <small>(STORAGE)</small>"]
    pet-shop-simulator-game-client["Pet Shop Simulator (Game Client) <br/> <small>(FRONTEND)</small>"]
    player-prefs-store["PlayerPrefs Settings Store <br/> <small>(STORAGE)</small>"]
    build-editor-tooling -->|Unity Editor build pipeline (headless build.sh)| pet-shop-simulator-game-client
    pet-shop-simulator-game-client -->|Local filesystem I/O (JsonUtility JSON)| local-save-file
    pet-shop-simulator-game-client -->|Unity PlayerPrefs API (key bindings, autosave setting)| player-prefs-store
```

## Components

- [Build & Editor Tooling](overview.md) (`build-editor-tooling`, custom)
- [Local Save File](overview.md) (`local-save-file`, storage)
- [Pet Shop Simulator (Game Client)](overview.md) (`pet-shop-simulator-game-client`, frontend)
- [PlayerPrefs Settings Store](overview.md) (`player-prefs-store`, storage)

## Interactions

- [build-editor-tooling → pet-shop-simulator-game-client](interactions/build-editor-tooling--pet-shop-simulator-game-client.md) via `Unity Editor build pipeline (headless build.sh)`
- [pet-shop-simulator-game-client → local-save-file](interactions/pet-shop-simulator-game-client--local-save-file.md) via `Local filesystem I/O (JsonUtility JSON)`
- [pet-shop-simulator-game-client → player-prefs-store](interactions/pet-shop-simulator-game-client--player-prefs-store.md) via `Unity PlayerPrefs API (key bindings, autosave setting)`
<!-- generated:end file:system-map -->
