<!-- generated:start file:system-map -->
# System Map

```mermaid
graph TD
    build-editor-tooling["Build & Editor Tooling <br/> <small>(CUSTOM)</small>"]
    local-save-file["Local Save File <br/> <small>(STORAGE)</small>"]
    pet-shop-simulator-game-client["Pet Shop Simulator (Game Client) <br/> <small>(FRONTEND)</small>"]
    build-editor-tooling -->|Unity Editor build pipeline (headless build.sh)| pet-shop-simulator-game-client
    pet-shop-simulator-game-client -->|Local filesystem I/O (JsonUtility JSON)| local-save-file
```

## Components

- [Build & Editor Tooling](overview.md) (`build-editor-tooling`, custom)
- [Local Save File](overview.md) (`local-save-file`, storage)
- [Pet Shop Simulator (Game Client)](overview.md) (`pet-shop-simulator-game-client`, frontend)

## Interactions

- [build-editor-tooling → pet-shop-simulator-game-client](interactions/build-editor-tooling--pet-shop-simulator-game-client.md) via `Unity Editor build pipeline (headless build.sh)`
- [pet-shop-simulator-game-client → local-save-file](interactions/pet-shop-simulator-game-client--local-save-file.md) via `Local filesystem I/O (JsonUtility JSON)`
<!-- generated:end file:system-map -->
