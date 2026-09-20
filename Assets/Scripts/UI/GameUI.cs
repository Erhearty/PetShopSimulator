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
        public TitleScreen     Title     { get; private set; }

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
            Pause    = canvasGO.AddComponent<PauseMenu>();
            Title    = canvasGO.AddComponent<TitleScreen>();

            HUD.Build(canvasGO.transform, shop, game, build);
            Info.Build(canvasGO.transform);
            Results.Build(canvasGO.transform, game);
            GameOver.Build(canvasGO.transform, game);
            Stats.Build(canvasGO.transform, game);
            Pause.Build(canvasGO.transform, game, audio);

            return canvas;
        }

        private void Update()
        {
            if (_game == null || Title == null || Title.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();

            if (Input.GetKeyDown(KeyCode.Tab) && !_game.IsGameOver && !Pause.IsOpen && !Results.IsOpen)
                Stats.Toggle();
        }

        /// <summary>
        /// Escape, most-transient first: cancel a placement, close a popup, close the ledger,
        /// otherwise open or close the pause menu.
        /// </summary>
        private void HandleEscape()
        {
            if (_build != null && _build.IsActive) { _build.ExitBuildMode(); return; }
            if (Info  != null && Info.IsOpen)      { Info.Hide();            return; }
            if (Stats != null && Stats.IsOpen)     { Stats.Hide();           return; }
            if (Results != null && Results.IsOpen) return;   // must be dismissed with the button
            if (_game.IsGameOver) return;

            Pause.Toggle();
        }
    }
}
