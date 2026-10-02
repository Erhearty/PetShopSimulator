using UnityEngine;
using PetShop.Shop;
using PetShop.Commerce;
using PetShop.Customer;
using PetShop.Progression;
using PetShop.Events;
using PetShop.UI;

namespace PetShop.Core
{
    /// <summary>
    /// Single entry point. Lives in MainScene next to the scene-authored <see cref="ShopLayout"/>;
    /// it creates every system and the UI and wires them to the baked world.
    ///
    /// WASD move · RMB orbit · scroll zoom · E interact · 1-8/B furniture catalogue · Enter close day · F5 save
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Shop starting values")]
        public float StartBalance    = 1000f;
        [Range(0f, 100f)] public float StartReputation = 40f;

        private GridManager     _grid;
        private BuildMode       _build;
        private ShopManager     _shop;
        private CustomerSpawner _spawner;
        private CheckoutQueue   _queue;
        private AudioManager    _audio;
        private GameManager     _game;
        private ShopLayout      _layout;

        private GameUI _ui;

        /// <summary>Frame build mode last closed on, so the same build-key press can't reopen the catalogue.</summary>
        private int _buildExitFrame = -1;

        /// <summary>Most pack fallbacks named in the start-up '[Models]' warning.</summary>
        private const int MaxPackFallbacksReported = 10;

        private void Awake()
        {
            PlaytestOptions.Apply(PlaytestOptions.Parse(System.Environment.GetCommandLineArgs()));

            _layout = FindAnyObjectByType<ShopLayout>();
            if (_layout == null)
            {
                Debug.LogError("[Bootstrap] No ShopLayout in the scene — the world is authored in " +
                               "Assets/Scenes/MainScene.unity. Aborting start-up.");
                enabled = false;
                return;
            }

            FurnitureFactory.Prefabs = _layout.FurniturePrefabs;
            if (FurnitureFactory.Prefabs == null)
                Debug.LogError("[Bootstrap] ShopLayout.FurniturePrefabs is not assigned; furniture cannot be spawned.");

            BuildSystems();
            BuildWorld();
            string packFallbacks = ModelLibrary.PackFallbackReport(MaxPackFallbacksReported);
            if (packFallbacks != null) Debug.LogWarning(packFallbacks);
            BuildUI();
            WireEverything();
        }

        private void Start()
        {
            bool touring  = Dev.CameraTour.TryCreate(out _);
            bool headless = touring || Dev.ScreenshotCapture.TryCreate(out _) || Application.isBatchMode;
            SaveSystem.MigrateLegacy();

            if (headless)
            {
                // Nobody is there to click "open the shop".
                _ui?.Title?.Hide();
                _game.Begin();
            }
            else
            {
                _game.SetModalOpen(true);
                _ui.Title.Build(_ui.CanvasRoot, (slot, continueSave) =>
                {
                    SaveSystem.ActiveSlot = slot;
                    if (!continueSave) SaveSystem.Delete();
                    _game.SetModalOpen(false);
                    _game.Begin();
                }, () => _ui.Settings.Show());
            }

            Debug.Log("[Bootstrap] Pet shop ready — WASD move, RMB orbit, E interact, B furniture catalogue, Tab ledger, Enter to close the day.");
        }

        private void Update()
        {
            if (_game == null || _game.IsGameOver) return;
            if (_ui != null && _ui.Catalogue != null && _ui.Catalogue.IsOpen)
            {
                // The build key toggles the catalogue shut; this is its only owner, so one press
                // cannot close and reopen it in the same frame.
                if (InputBindings.GetKeyDown(GameAction.BuildMode)) _ui.Catalogue.Hide();
                return;
            }
            if (_ui != null && _ui.AnyModalOpen) return;
            // BuildMode closes itself on the build key; don't reopen the catalogue on that press.
            if (Time.frameCount == _buildExitFrame) return;

            var ids = BuildCatalog.HotkeyOrder;
            for (int i = 0; i < ids.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    ToggleBuild(ids[i]);

            if (InputBindings.GetKeyDown(GameAction.BuildMode) && !_build.IsActive)
                ToggleBuild(null);
        }

        /// <summary>
        /// Closes build mode when it is open; otherwise opens the furniture catalogue, on the tab
        /// holding <paramref name="catalogId"/> when one is given.
        /// </summary>
        private void ToggleBuild(string catalogId)
        {
            if (_build.IsActive) { _build.ExitBuildMode(); return; }
            _ui?.Catalogue?.Open(catalogId);
        }

        // ── Systems ─────────────────────────────────────────────────────────────

        private void BuildSystems()
        {
            _grid = gameObject.AddComponent<GridManager>();

            var shopGO = new GameObject("ShopManager");
            _shop = shopGO.AddComponent<ShopManager>();
            _shop.StartingBalance    = StartBalance;
            _shop.StartingReputation = StartReputation;

            var buildGO = new GameObject("BuildMode");
            _build = buildGO.AddComponent<BuildMode>();
            _build.GridManager = _grid;
            _build.Shop        = _shop;
            _build.ObjectRoot  = null;   // set once the shop root exists

            var spawnGO = new GameObject("CustomerSpawner");
            _spawner = spawnGO.AddComponent<CustomerSpawner>();
            _spawner.ShopManager = _shop;

            var queueGO = new GameObject("CheckoutQueue");
            _queue = queueGO.AddComponent<CheckoutQueue>();

            var audioGO = new GameObject("AudioManager");
            _audio = audioGO.AddComponent<AudioManager>();

            var gmGO = new GameObject("GameManager");
            _game = gmGO.AddComponent<GameManager>();
            _game.Grid    = _grid;
            _game.Build   = _build;
            _game.Shop    = _shop;
            _game.Spawner = _spawner;
            _game.Audio   = _audio;
            _game.Queue   = _queue;
            _build.Supply = _game.Furniture;   // placing draws on, and removal returns to, the inventory
            gmGO.AddComponent<AutoReorderRunner>().Attach(_game);
        }

        /// <summary>Wires the scene-authored player and camera; creates the furniture root if missing.</summary>
        private void BuildSceneWorld()
        {
            if (_layout.FurnitureRoot == null)
            {
                var root = new GameObject("Furniture").transform;
                root.SetParent(_layout.ShopRoot != null ? _layout.ShopRoot : _layout.transform, false);
                _layout.FurnitureRoot = root;
            }

            if (_layout.Player == null)
            {
                var found = GameObject.Find("Player");
                if (found != null) _layout.Player = found.transform;
                else Debug.LogWarning("[Bootstrap] No 'Player' object in the scene.");
            }

            if (RenderSettings.skybox != null) DynamicGI.UpdateEnvironment();

            if (_layout.Player != null)
            {
                // The shopkeeper's body comes from the character pack, so the baked scene omits it.
                if (_layout.Player.Find("Body") == null)
                {
                    var controller = _layout.Player.GetComponent<CharacterController>();
                    CharacterFactory.Attach(_layout.Player.gameObject, () => controller != null ? controller.velocity : Vector3.zero, variant: 1);
                }

                if (_layout.PlayerStartAnchor != null)
                {
                    var cc = _layout.Player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    _layout.Player.position = _layout.PlayerStartPosition;
                    if (cc != null) cc.enabled = true;
                }

                Camera cam = Camera.main;
                if (cam != null)
                {
                    var fpc = cam.GetComponent<Player.FirstPersonCamera>() ?? cam.gameObject.AddComponent<Player.FirstPersonCamera>();
                    fpc.Body = _layout.Player;
                }
            }
        }

        private void BuildWorld()
        {
            BuildSceneWorld();

            _game.Layout = _layout;
            _layout.FillFloorGrid(_grid);
            _build.ObjectRoot = _layout.FurnitureRoot;

            var points = new GameObject("CustomerWaypoints");
            points.transform.SetParent(_spawner.transform, false);

            Vector3 pavement = _layout.PavementCentre;

            _spawner.SpawnPoint    = MakePoint(points.transform, "SpawnPoint",    pavement);
            _spawner.ExitPoint     = MakePoint(points.transform, "ExitPoint",     pavement);
            _spawner.EntryPoint    = MakePoint(points.transform, "EntryPoint",    _layout.ForecourtPosition);
            Vector3 till = _layout.TillPosition;
            _spawner.RegisterPoint = MakePoint(points.transform, "RegisterPoint", till);

            // The queue runs from the till back towards the door.
            // Staff stand just behind the till, facing the queue.
            var staffGo = new GameObject("StaffStation");
            staffGo.transform.SetParent(points.transform, false);
            staffGo.transform.position = till + new Vector3(0f, 0f, -1.1f);
            staffGo.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _game.StaffStation = staffGo.transform;

            _queue.TillPoint      = _spawner.RegisterPoint;
            _queue.QueueDirection = Vector3.forward;
            _spawner.Queue        = _queue;
            _spawner.SpawnSpreadX = _layout.PavementSpread;
        }

        private static Transform MakePoint(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        // ── UI ──────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            _ui = new GameObject("GameUI").AddComponent<GameUI>();
            _ui.Build(_game, _shop, _build, _audio);
        }

        // ── Wiring ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds the progression director to the game object and hands it the layout and
        /// build mode, so tiers can open lot stages and pen species.
        /// </summary>
        private void WireProgression()
        {
            var progression = _game.gameObject.AddComponent<ProgressionDirector>();
            progression.Init(_game, _layout, _build);
            _game.Progression = progression;
        }

        /// <summary>
        /// Adds the seasonal event director and hands it the shop and spawner it tunes.
        /// </summary>
        private void WireEvents()
        {
            var events = _game.gameObject.AddComponent<ShopEventDirector>();
            events.Init(_game, _shop, _spawner);
            _game.Events = events;
        }

        private void WireEverything()
        {
            WireProgression();
            WireEvents();

            _build.OnFurnitureSpawned.AddListener(_game.RegisterFurniture);
            _build.OnFurnitureDespawning.AddListener(_game.UnregisterFurniture);

            // Walls block movement, so the NavMesh has to follow them immediately — waiting
            // until build mode exits lets customers walk through a wall you just built.
            _build.OnObjectPlacedVisually.AddListener((_, def) =>
            {
                if (BuildCatalog.IsBuildingPiece(def.Id)) _layout.BakeNavMesh();
            });
            _build.OnObjectRemovedVisually.AddListener(_ => _layout.BakeNavMesh());
            _build.OnBuildModeEntered.AddListener(_ => _audio.PlaySfx("click"));
            _build.OnBuildModeExited.AddListener(() =>
            {
                _buildExitFrame = Time.frameCount;
                _audio.PlaySfx("build");
                _layout.BakeNavMesh();
            });

            _game.OnDayEnded.AddListener(_ui.Results.Show);
            _game.OnInfoPanel.AddListener(_ui.Info.Show);
            _game.OnGameOver.AddListener(_ui.GameOver.Show);

            var player = _layout.Player;
            if (player != null)
            {
                var interaction = player.GetComponent<Player.InteractionSystem>();
                if (interaction != null) interaction.PromptText = _ui.HUD.PromptLabel;
            }

            AttachTelemetry();
        }

        /// <summary>Adds the soak-run recorder, but only when -telemetry or -quitafterdays asked for it.</summary>
        private void AttachTelemetry()
        {
            string path = PlaytestOptions.TelemetryPath;
            int?   days = PlaytestOptions.QuitAfterDays;
            if (string.IsNullOrEmpty(path) && !days.HasValue) return;

            gameObject.AddComponent<Dev.DayTelemetry>().Init(_game, _shop, path, days);
        }
    }
}
