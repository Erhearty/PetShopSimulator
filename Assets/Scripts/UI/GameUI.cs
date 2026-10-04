using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// Owns every panel and is the single place keyboard input is routed to the UI.
    /// Escape means different things depending on what is open, so it is resolved here in
    /// one ordered list rather than being contested by four components.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public ShopHUD         HUD       { get; private set; }
        public InfoPanel       Info      { get; private set; }
        public DayResultsPanel Results   { get; private set; }
        public GameOverPanel   GameOver  { get; private set; }
        public PauseMenu       Pause     { get; private set; }
        public StatsPanel      Stats     { get; private set; }
        public BreedingPanel   Breeding  { get; private set; }
        public StaffPanel      StaffBoard{ get; private set; }
        public FamilyTreePanel FamilyTree{ get; private set; }
        public TitleScreen     Title     { get; private set; }
        public ShowcasePanel   Showcase  { get; private set; }
        public ReorderPanel    Reorder   { get; private set; }

        /// <summary>The bottom inventory bar: owned furniture slots and stockroom counts.</summary>
        public InventoryBar    Inventory { get; private set; }

        /// <summary>The quest journal, opened with <see cref="QuestJournalPanel.OpenKey"/>.</summary>
        public QuestJournalPanel Journal { get; private set; }

        /// <summary>The furniture catalogue on the Shop book's Build page: order furniture and pick owned pieces up to place.</summary>
        public FurnitureCatalogPanel Catalogue { get; private set; }

        /// <summary>The build view's Wall / Window wall / Doorway / Fence / Remove tool strip.</summary>
        public BuildToolbar Toolbar { get; private set; }

        /// <summary>The top-down build view; Esc leaves it once nothing more transient is open.</summary>
        public BuildCamera BuildView { get; set; }

        /// <summary>The in-game guide: opened from the Shop book, pause menu, title screen or <see cref="GameAction.Guide"/>.</summary>
        public GuidePanel      Guide     { get; private set; }

        /// <summary>Controls rebinding and general options; opened from the pause menu or title screen.</summary>
        public SettingsPanel   Settings  { get; private set; }

        /// <summary>The canvas every panel lives under. Panels must parent here, not to GameUI:
        /// a plain Transform in the middle of a UI hierarchy breaks RectTransform anchoring.</summary>
        public Transform CanvasRoot { get; private set; }

        public Canvas Canvas { get; private set; }

        private GameManager _game;
        private BuildMode   _build;

        public bool AnyModalOpen =>
            (Results  != null && Results.IsOpen)  ||
            (Pause    != null && Pause.IsOpen)    ||
            (Stats    != null && Stats.IsOpen)    ||
            (FamilyTree != null && FamilyTree.IsOpen) ||
            (Showcase != null && Showcase.IsOpen) ||
            (Breeding != null && Breeding.IsOpen) ||
            (StaffBoard != null && StaffBoard.IsOpen) ||
            (Reorder != null && Reorder.IsOpen) ||
            (Settings != null && Settings.IsOpen) ||
            (Journal  != null && Journal.IsOpen)  ||
            (Guide    != null && Guide.IsOpen)    ||
            (Title    != null && Title.IsOpen);

        public Canvas Build(GameManager game, ShopManager shop, BuildMode build, AudioManager audio)
        {
            _game  = game;
            _build = build;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Explicit ordering: the HUD must sit above anything else that ever gets drawn,
            // and overlay canvases are sorted by this rather than by hierarchy depth.
            canvas.sortingOrder      = 100;
            canvas.overrideSorting   = false;
            canvas.pixelPerfect      = false;
            Canvas = canvas;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            CanvasRoot = canvasGO.transform;

            HUD      = canvasGO.AddComponent<ShopHUD>();
            Info     = canvasGO.AddComponent<InfoPanel>();
            Results  = canvasGO.AddComponent<DayResultsPanel>();
            GameOver = canvasGO.AddComponent<GameOverPanel>();
            Stats    = canvasGO.AddComponent<StatsPanel>();
            Breeding   = canvasGO.AddComponent<BreedingPanel>();
            StaffBoard = canvasGO.AddComponent<StaffPanel>();
            FamilyTree = canvasGO.AddComponent<FamilyTreePanel>();
            Showcase   = canvasGO.AddComponent<ShowcasePanel>();
            Reorder    = canvasGO.AddComponent<ReorderPanel>();
            Inventory  = canvasGO.AddComponent<InventoryBar>();
            Toolbar    = canvasGO.AddComponent<BuildToolbar>();
            Pause    = canvasGO.AddComponent<PauseMenu>();
            Title    = canvasGO.AddComponent<TitleScreen>();
            Settings = canvasGO.AddComponent<SettingsPanel>();
            Journal  = canvasGO.AddComponent<QuestJournalPanel>();
            Guide    = canvasGO.AddComponent<GuidePanel>();

            HUD.Build(canvasGO.transform, shop, game, build);
            Info.Build(canvasGO.transform);
            Results.Build(canvasGO.transform, game);
            GameOver.Build(canvasGO.transform, game);
            Stats.Build(canvasGO.transform, game, Reorder, build);
            Catalogue = Stats.Catalogue;
            Reorder.Build(canvasGO.transform, game);
            HUD.Catalogue = Catalogue;
            Inventory.Build(canvasGO.transform, game.Furniture, shop, build, () => AnyModalOpen || game.IsGameOver);
            Toolbar.Build(canvasGO.transform, game, shop, build, () => BuildView, () => AnyModalOpen || game.IsGameOver);
            Stats.BuildView = () => BuildView;
            Stats.Toolbar   = Toolbar;
            Breeding.Build(canvasGO.transform, game, FamilyTree, Showcase);
            FamilyTree.Build(canvasGO.transform, game);   // after Breeding so it draws on top
            Showcase.Build(canvasGO.transform, game);
            StaffBoard.Build(canvasGO.transform, game);
            Settings.Build(canvasGO.transform, game);
            Journal.Build(canvasGO.transform, game);
            Guide.Build(canvasGO.transform, game);
            Stats.OpenGuide = Guide.Show;
            Stats.GuideOpen = () => Guide.IsOpen;
            Journal.InputBlocked = () => Guide.IsOpen;
            Pause.Build(canvasGO.transform, game, audio, Settings, Guide);

            return canvas;
        }

        /// <summary>Wires just the pieces the Escape routing needs, without building the panels. Test seam.</summary>
        internal void WireForTests(GameManager game, BuildMode build, FurnitureCatalogPanel catalogue,
                                   StatsPanel stats = null, GuidePanel guide = null)
        {
            _game     = game;
            _build    = build;
            Catalogue = catalogue;
            Stats     = stats;
            Guide     = guide;
        }

        private void Update()
        {
            if (_game == null || Title == null) return;
            if (Title.IsOpen)
            {
                // Over the title screen only the guide (opened from it) answers Esc.
                if (Guide != null && Guide.IsOpen && Input.GetKeyDown(KeyCode.Escape)) Guide.Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();

            if (InputBindings.GetKeyDown(GameAction.Guide)) HandleGuideKey();
            if (InputBindings.GetKeyDown(GameAction.Ledger)) HandleLedgerKey();
            if (Input.GetKeyDown(QuestJournalPanel.OpenKey)) ToggleJournal();
        }

        /// <summary>The guide key: toggles the guide (over the book too) unless the day is over or a rebind is listening.</summary>
        internal void HandleGuideKey()
        {
            if (Guide == null || _game.IsGameOver) return;
            if ((Results != null && Results.IsOpen) || (Settings != null && Settings.IsCapturing)) return;
            Guide.Toggle();
        }

        /// <summary>The ledger key: opens or closes the Shop book unless another panel holds the keyboard.</summary>
        internal void HandleLedgerKey()
        {
            // While the book is open a Tab binding turns its pages instead; Esc closes it.
            if (Stats == null || Stats.CyclesWithLedgerKey || _game.IsGameOver || LedgerBlocked()) return;
            Stats.Toggle();
        }

        /// <summary>True while a panel the book must not open over (or close under) is showing.</summary>
        private bool LedgerBlocked() =>
            (Guide   != null && Guide.IsOpen)   ||
            (Pause   != null && Pause.IsOpen)   ||
            (Results != null && Results.IsOpen) ||
            (Reorder != null && Reorder.IsOpen) ||
            (Journal != null && Journal.IsOpen);

        /// <summary>Closes the journal when open; opens it only when nothing else holds the keyboard.</summary>
        private void ToggleJournal()
        {
            if (Journal == null || (Guide != null && Guide.IsOpen)) return;
            if (Journal.IsOpen) { Journal.Hide(); return; }
            if (!_game.IsGameOver && !_game.IsModalOpen) Journal.Show();
        }

        /// <summary>
        /// Escape, most-transient first: cancel a placement or build-view tool (Remove included), close a popup, close the ledger,
        /// leave the build view once no panel is open, otherwise open or close the pause menu.
        /// </summary>
        internal void HandleEscape()
        {
            if (Settings != null && Settings.IsCapturing) return;   // Esc cancels the rebind only
            if (Settings != null && Settings.IsOpen) { Settings.Hide(); return; }
            if (Guide != null && Guide.IsOpen) { Guide.Hide(); return; }
            if (Journal != null && Journal.IsOpen) { Journal.Hide(); return; }
            if (Reorder != null && Reorder.IsOpen) { Reorder.Hide(); return; }
            if (Catalogue != null && Catalogue.IsOpen) { Catalogue.Hide(); return; }
            if (_build != null && _build.IsActive) { _build.ExitBuildMode(); return; }
            if (Info  != null && Info.IsOpen)      { Info.Hide();            return; }
            if (Showcase != null && Showcase.IsOpen) { Showcase.Hide();        return; }
            if (FamilyTree != null && FamilyTree.IsOpen) { FamilyTree.Hide();  return; }
            if (Breeding != null && Breeding.IsOpen) { Breeding.Hide();        return; }
            if (StaffBoard != null && StaffBoard.IsOpen) { StaffBoard.Hide();   return; }
            if (Stats != null && Stats.IsOpen)     { Stats.Hide();           return; }
            if (Results != null && Results.IsOpen) return;   // must be dismissed with the button
            // Only once no panel is open, so Esc never leaves the view with a panel still showing.
            if (BuildView != null && BuildView.IsActive) { BuildView.Exit(); return; }
            if (_game.IsGameOver) return;

            Pause.Toggle();
        }
    }
}
