<!-- generated:start cap:glossary-intro -->
# System Intent & Glossary

The overall outcome this system exists to achieve, the canonical component names projected from the architecture canvas, and the authoritative business vocabulary. Treat these terms as carrying their defined meaning throughout the project.
<!-- generated:end cap:glossary-intro -->

<!-- generated:start cap:components-heading -->
## Components

Canonical component names projected from the architecture canvas.
<!-- generated:end cap:components-heading -->

<!-- generated:start comp:pet-shop-simulator-game-client -->
- **Pet Shop Simulator (Game Client)** (`pet-shop-simulator-game-client`) - frontend component. Single-player, first-person 3D shop-management sim. GameBootstrapper is the whole entry point: it builds the grid/shop/build/spawner/audio/manager systems, generates the shop room and street via procedural + Kenney-kit geometry, assembles the player and UI, then hands control to GameManager for the day loop (customers, stocking, breeding, rent). No backend/server - everything runs client-side in one Unity process; all furniture placement funnels through BuildMode.Place() -> FurnitureFactory.Spawn() and audio is synthesised at startup (no sound files).
<!-- generated:end comp:pet-shop-simulator-game-client -->

<!-- generated:start comp:build-editor-tooling -->
- **Build & Editor Tooling** (`build-editor-tooling`) - custom component. Editor-time / headless tooling (Assets/Editor, driven by root build.sh) that sets up the project (TextMesh Pro essentials, Always-Included Shaders), generates the scene (SceneBuilder, GameBuilder), imports/reports on Kenney and Asset Store art, verifies spawns/materials/shaders, and produces the Linux player build plus smoke-test screenshots. Runs only at build/dev time - never shipped in the game itself.
<!-- generated:end comp:build-editor-tooling -->

<!-- generated:start comp:local-save-file -->
- **Local Save File** (`local-save-file`) - storage component. Up to three JSON save slots (petshop_save_1..3.json under Application.persistentDataPath); a legacy petshop_save.json is migrated into slot 1 on first run.
<!-- generated:end comp:local-save-file -->

<!-- generated:start comp:player-prefs-store -->
- **PlayerPrefs Settings Store** (`player-prefs-store`) - storage component. Unity PlayerPrefs store for per-machine player preferences: key bindings (InputBindings.cs, PlayerPrefsBindingStore) and settings such as the autosave preference (GameSettings.cs). Separate from the Local Save File, which holds game-progress state.
<!-- generated:end comp:player-prefs-store -->
