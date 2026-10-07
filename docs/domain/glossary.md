<!-- generated:start cap:glossary-intro -->
# System Intent & Glossary

The overall outcome this system exists to achieve, the canonical component names projected from the architecture canvas, and the authoritative business vocabulary. Treat these terms as carrying their defined meaning throughout the project.
<!-- generated:end cap:glossary-intro -->

<!-- generated:start cap:components-heading -->
## Components

Canonical component names projected from the architecture canvas.
<!-- generated:end cap:components-heading -->

<!-- generated:start comp:pet-shop-simulator-game-client -->
- **Pet Shop Simulator (Game Client)** (`pet-shop-simulator-game-client`) - frontend component. Single-player, first-person 3D shop-management sim. The world (shop room and street) is authored in the editor in MainScene.unity; the one runtime-generated exception is the shop roof, which RoofBuilder builds as a collider-free mesh (Scenery layer) over the shell footprint and the cells under/enclosed by player-built walls, rebuilding whenever a wall is placed or removed. GameBootstrapper is the whole entry point: it builds the grid/shop/build/spawner/audio/manager systems (including the Roof) on top of the authored scene, assembles the player and UI, then hands control to GameManager for the day loop (customers, stocking, breeding, rent). No backend/server - everything runs client-side in one Unity process; all furniture placement funnels through BuildMode.Place() -> FurnitureFactory.Spawn() and audio is synthesised at startup (no sound files).
<!-- generated:end comp:pet-shop-simulator-game-client -->

<!-- generated:start comp:build-editor-tooling -->
- **Build & Editor Tooling** (`build-editor-tooling`) - custom component. Editor-time / headless tooling (Assets/Editor, driven by root build.sh) that sets up the project (TextMesh Pro essentials, Always-Included Shaders), builds the player from the hand-authored, checked-in Assets/Scenes/MainScene.unity (GameBuilder - the tooling no longer generates the scene), imports/reports on Kenney and Asset Store art, verifies spawns/materials/shaders, and produces the Linux player build plus smoke-test screenshots. Runs only at build/dev time - never shipped in the game itself.
<!-- generated:end comp:build-editor-tooling -->

<!-- generated:start comp:local-save-file -->
- **Local Save File** (`local-save-file`) - storage component. Up to three JSON save slots (petshop_save_1..3.json under Application.persistentDataPath); a legacy petshop_save.json is migrated into slot 1 on first run.
<!-- generated:end comp:local-save-file -->

<!-- generated:start comp:player-prefs-store -->
- **PlayerPrefs Settings Store** (`player-prefs-store`) - storage component. Unity PlayerPrefs store for player preferences that belong to one machine. It holds key bindings (InputBindings.cs, PlayerPrefsBindingStore) and settings such as the autosave preference (GameSettings.cs) and the UI language (`language`: en or uk). It is separate from the Local Save File, which holds game progress.
<!-- generated:end comp:player-prefs-store -->
<!-- generated:start comp:soak-telemetry-log -->
- **Soak Telemetry Log (JSONL)** (`soak-telemetry-log`) - storage component. Dev-only JSON-lines file written by DayTelemetry (Assets/Scripts/Dev/DayTelemetry.cs) during soak runs: one JSON record appended per in-game day plus a final run-summary record, via File.AppendAllText. Only produced when GameBootstrapper.AttachTelemetry attaches the component because -telemetry or -quitafterdays was passed on the command line; normal play never writes it. Separate from the Local Save File and never read back by the game.
<!-- generated:end comp:soak-telemetry-log -->
