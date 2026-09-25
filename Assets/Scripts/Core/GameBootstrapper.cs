using UnityEngine;
using PetShop.Shop;
using PetShop.Commerce;
using PetShop.Customer;
using PetShop.UI;

namespace PetShop.Core
{
    /// <summary>
    /// Single entry point. Drop this on an empty GameObject in an otherwise blank scene
    /// and press Play — it creates every system, the shop, the player and the UI.
    ///
    /// WASD move · RMB orbit · scroll zoom · E interact · 1-4/B build · Enter close day · F5 save
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Shop starting values")]
        public float StartBalance    = 1000f;
        [Range(0f, 100f)] public float StartReputation = 40f;

        [Header("Shop building")]
        public float RoomWidth  = 16f;
        public float RoomDepth  = 12f;
        public float WallHeight = 4f;

        [Header("Yard — the open lot around the shop")]
        public float YardWidth = 64f;
        public float YardDepth = 34f;

        private GridManager     _grid;
        private BuildMode       _build;
        private ShopManager     _shop;
        private CustomerSpawner _spawner;
        private CheckoutQueue   _queue;
        private AudioManager    _audio;
        private GameManager     _game;
        private ShopGenerator   _generator;

        private GameUI _ui;

        private void Awake()
        {
            PlaytestOptions.Apply(PlaytestOptions.Parse(System.Environment.GetCommandLineArgs()));
            BuildSystems();
            BuildWorld();
            BuildUI();
            WireEverything();
        }

        private void Start()
        {
            bool touring  = Dev.CameraTour.TryCreate(out _);
            bool headless = touring || Dev.ScreenshotCapture.TryCreate(out _) || Application.isBatchMode;

            if (headless)
            {
                // Nobody is there to click "open the shop".
                _ui?.Title?.Hide();
                _game.Begin();
            }
            else
            {
                _game.SetModalOpen(true);
                _ui.Title.Build(_ui.CanvasRoot, SaveSystem.HasSave(), continueSave =>
                {
                    if (!continueSave) SaveSystem.Delete();
                    _game.SetModalOpen(false);
                    _game.Begin();
                });
            }

            Debug.Log("[Bootstrap] Pet shop ready — WASD move, RMB orbit, E interact, 1-4 build, Tab ledger, Enter to close the day.");
        }

        private void Update()
        {
            if (_game == null || _game.IsGameOver) return;
            if (_ui != null && _ui.AnyModalOpen) return;

            var ids = BuildCatalog.HotkeyOrder;
            for (int i = 0; i < ids.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    ToggleBuild(ids[i]);

            if (Input.GetKeyDown(KeyCode.B))
                ToggleBuild(_build.CurrentItem != null ? _build.CurrentItem.Id : BuildCatalog.ShelfSmall);
        }

        private void ToggleBuild(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null) return;

            if (_build.IsActive && _build.CurrentItem != null && _build.CurrentItem.Id == def.Id)
                _build.ExitBuildMode();
            else
                _build.EnterBuildMode(def);
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

            _generator = gameObject.AddComponent<ShopGenerator>();
            _generator.RoomWidth  = RoomWidth;
            _generator.RoomDepth  = RoomDepth;
            _generator.WallHeight = WallHeight;
            _generator.YardWidth  = YardWidth;
            _generator.YardDepth  = YardDepth;
            _game.Generator = _generator;
        }

        private void BuildWorld()
        {
            _generator.Generate(_grid);
            _build.ObjectRoot = _generator.FurnitureRoot;

            var points = new GameObject("CustomerWaypoints");
            points.transform.SetParent(_spawner.transform, false);

            Vector3 pavement = _generator.Street != null
                ? _generator.Street.PavementCentre
                : _generator.DoorPosition + Vector3.forward * 3f;

            _spawner.SpawnPoint    = MakePoint(points.transform, "SpawnPoint",    pavement);
            _spawner.ExitPoint     = MakePoint(points.transform, "ExitPoint",     pavement);
            _spawner.EntryPoint    = MakePoint(points.transform, "EntryPoint",    _generator.ForecourtPosition);
            Vector3 till = _generator.ShopCentre + new Vector3(0f, 0f, -RoomDepth * 0.5f + 2.6f);
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
            if (_generator.Street != null) _spawner.SpawnSpreadX = _generator.Street.PavementSpread;
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

        private void WireEverything()
        {
            _build.OnFurnitureSpawned.AddListener(_game.RegisterFurniture);
            _build.OnFurnitureDespawning.AddListener(_game.UnregisterFurniture);

            // Walls block movement, so the NavMesh has to follow them immediately — waiting
            // until build mode exits lets customers walk through a wall you just built.
            _build.OnObjectPlacedVisually.AddListener((_, def) =>
            {
                if (BuildCatalog.IsBuildingPiece(def.Id)) _generator.BakeNavMesh();
            });
            _build.OnObjectRemovedVisually.AddListener(_ => _generator.BakeNavMesh());
            _build.OnBuildModeEntered.AddListener(_ => _audio.PlaySfx("click"));
            _build.OnBuildModeExited.AddListener(() =>
            {
                _audio.PlaySfx("build");
                _generator.BakeNavMesh();
            });

            _game.OnDayEnded.AddListener(_ui.Results.Show);
            _game.OnInfoPanel.AddListener(_ui.Info.Show);
            _game.OnGameOver.AddListener(_ui.GameOver.Show);

            var player = _generator.Player;
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
