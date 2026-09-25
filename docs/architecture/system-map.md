<!-- generated:start file:system-map -->
# System Map

```mermaid
graph TD
    build-editor-tooling["Build & Editor Tooling <br/> <small>(CUSTOM)</small>"]
    local-save-file["Local Save File <br/> <small>(STORAGE)</small>"]
    pet-shop-simulator-game-client["Pet Shop Simulator (Game Client) <br/> <small>(FRONTEND)</small>"]
    soak-telemetry-log["Soak Telemetry Log (JSONL) <br/> <small>(STORAGE)</small>"]
    build-editor-tooling -->|Unity Editor build pipeline (headless build.sh)| pet-shop-simulator-game-client
    pet-shop-simulator-game-client -->|Local filesystem I/O (JsonUtility JSON)| local-save-file
    pet-shop-simulator-game-client -->|Local filesystem append (File.AppendAllText, JSON lines)| soak-telemetry-log
```

## Components

- [Build & Editor Tooling](overview.md) (`build-editor-tooling`, custom)
- [Local Save File](overview.md) (`local-save-file`, storage)
- [Pet Shop Simulator (Game Client)](overview.md) (`pet-shop-simulator-game-client`, frontend)
- [Soak Telemetry Log (JSONL)](overview.md) (`soak-telemetry-log`, storage)

## Interactions

- [build-editor-tooling → pet-shop-simulator-game-client](interactions/build-editor-tooling--pet-shop-simulator-game-client.md) via `Unity Editor build pipeline (headless build.sh)`
- [pet-shop-simulator-game-client → local-save-file](interactions/pet-shop-simulator-game-client--local-save-file.md) via `Local filesystem I/O (JsonUtility JSON)`
- [pet-shop-simulator-game-client → soak-telemetry-log](interactions/pet-shop-simulator-game-client--soak-telemetry-log.md) via `Local filesystem append (File.AppendAllText, JSON lines)`
<!-- generated:end file:system-map -->
