<!-- generated:start cap:glossary-intro -->
# System Intent & Glossary

The overall outcome this system exists to achieve, the canonical component names projected from the architecture canvas, and the authoritative business vocabulary. Treat these terms as carrying their defined meaning throughout the project.
<!-- generated:end cap:glossary-intro -->

<!-- generated:start cap:components-heading -->
## Components

Canonical component names projected from the architecture canvas.
<!-- generated:end cap:components-heading -->

<!-- generated:start comp:pet-shop-simulator-game-client -->
- **Pet Shop Simulator (Game Client)** (`pet-shop-simulator-game-client`) - frontend component. Single-player, first-person 3D shop-management sim. The world (shop room and street) is authored in the editor in MainScene.unity - nothing generates it at runtime. GameBootstrapper is the whole entry point: it builds the grid/shop/build/spawner/audio/manager systems on top of the authored scene, assembles the player and UI, then hands control to GameManager for the day loop (customers, stocking, breeding, rent). No backend/server - everything runs client-side in one Unity process; all furniture placement funnels through BuildMode.Place() -> FurnitureFactory.Spawn() and audio is synthesised at startup (no sound files).
<!-- generated:end comp:pet-shop-simulator-game-client -->

<!-- generated:start comp:build-editor-tooling -->
- **Build & Editor Tooling** (`build-editor-tooling`) - custom component. Editor-time / headless tooling (Assets/Editor, driven by root build.sh and Tools/). build.sh targets: setup, linux, windows, run, smoke (90s timeout), test (EditMode), playmode (gating run with -testCategory '!KnownIssue', then non-gating KnownIssue run), soak (400s timeout), spawnverify, playtest, look (not run with -nographics), look-diff, look-approve, assets, kenney, plus a no-op scene. It clears a stale Temp/UnityLockfile before each run. Asset steps use Tools/fetch_kenney.sh, Tools/unpack_unitypackage.py and Tools/organise_packs.py (packs are extracted with Python, not Unity's importer). Editor entry points: GameBuilder.BuildLinux/BuildWindows (builds the hand-authored MainScene.unity), ProjectSetup.ImportTMPEssentials, ShaderInclusion.EnsureIncluded, MaterialRepair.Repair, SpawnVerify.Verify, SceneShot.Capture, ScreenshotDiff.Compare/Approve, AssetStoreImporter.Report/ImportAll, KenneyAssetReport, KenneyBoundsDump, FacingProbe, GfxProbe (not driven by build.sh), KenneyModelPostprocessor. Never shipped in the game.
<!-- generated:end comp:build-editor-tooling -->

<!-- generated:start comp:local-save-file -->
- **Local Save File** (`local-save-file`) - storage component. Up to three JSON save slots (petshop_save_1..3.json under Application.persistentDataPath, a file naming scheme - not a field); a legacy petshop_save.json is migrated into slot 1 on first run. Writes go to a .tmp file which is swapped in, leaving a .bak; Load falls back to the .bak if the main file is bad. Current save version is 5; older saves are migrated in memory by SaveMigrator. The -savepath flag overrides slot 1 only. SlotFile in the schema below is a naming scheme, not a SaveData field; Staff defaults to 1.
<!-- generated:end comp:local-save-file -->

<!-- generated:start comp:player-prefs-store -->
- **PlayerPrefs Settings Store** (`player-prefs-store`) - storage component. Unity PlayerPrefs store for per-machine player preferences, written with PlayerPrefs.SetString + PlayerPrefs.Save(). Keys: settings.autosaveMorning (default "1", GameSettings.cs), settings.showQuestTracker (default "1", GameSettings.cs), and bind.<GameAction> per key binding (InputBindings.cs, PlayerPrefsBindingStore). Separate from the Local Save File, which holds game-progress state.
<!-- generated:end comp:player-prefs-store -->
<!-- generated:start comp:soak-telemetry-log -->
- **Soak Telemetry Log (JSONL)** (`soak-telemetry-log`) - storage component. Dev-only JSON-lines file written by DayTelemetry (Assets/Scripts/Dev/DayTelemetry.cs) during soak runs, via File.AppendAllText; attached by GameBootstrapper.AttachTelemetry only when -telemetry or -quitafterdays is passed. Per-day record (SoakBands.DayRecord): day, seed, sales, revenue, checkouts, walkoutsEmpty, gaveUp, navTimeouts, strandedCheckouts, reputation, balance. Final summary line (DayTelemetry.RunSummary): summary:true, days, seed, violations, exitCode. Exit code 0 when clean, 3 when a SoakBands band is violated (SoakBands.NoSeed = -1). Never read back by the game.
<!-- generated:end comp:soak-telemetry-log -->
