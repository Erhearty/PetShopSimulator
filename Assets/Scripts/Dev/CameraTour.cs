using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Dev
{
    /// <summary>
    /// Photographs the whole game — world, mechanics and every UI panel — so it can be
    /// reviewed without launching it.
    ///
    /// Renders through an off-screen RenderTexture rather than ScreenCapture, because that
    /// works in batch mode where there is no game view. UI shots need a further trick: a
    /// ScreenSpaceOverlay canvas is composited straight to the display and never appears in
    /// a RenderTexture, so for those the canvas is temporarily rebound to the tour camera in
    /// ScreenSpaceCamera mode and put back afterwards.
    ///
    ///   -tour &lt;dir&gt;   capture into that directory, then quit
    /// </summary>
    public class CameraTour : MonoBehaviour
    {
        public string OutputDir = "Screenshots";
        public int    Width     = 1600;
        public int    Height    = 900;
        public float  WarmupSeconds = 3.5f;

        private enum Kind { World, Plan, Ui }

        private struct Shot
        {
            public string  Name;
            public Kind    Kind;
            public Vector3 Position;
            public Vector3 LookAt;
            public float   Fov;
            public float   OrthoSize;
            public Action  Setup;      // opens a panel / sets a state before the frame
            public Action  Teardown;
        }

        public static bool TryCreate(out CameraTour tour)
        {
            tour = null;
            var args = System.Environment.GetCommandLineArgs();
            string dir = null;

            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tour") dir = args[i + 1];

            if (string.IsNullOrEmpty(dir)) return false;

            var go = new GameObject("CameraTour");
            tour = go.AddComponent<CameraTour>();
            tour.OutputDir = dir;
            Debug.Log($"[Tour] enabled, writing to {dir}");
            return true;
        }

        private IEnumerator Start()
        {
            float waited = 0f;
            while (waited < WarmupSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[Tour] warmup done, capturing...");
            Debug.Log(NavProbe.Run());

            Directory.CreateDirectory(OutputDir);

            var shots = BuildShots();
            var cam   = new GameObject("TourCamera").AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.nearClipPlane   = 0.05f;
            cam.farClipPlane    = 900f;

            var rt  = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            for (int index = 0; index < shots.Count; index++)
            {
                var shot = shots[index];

                cam.transform.position = shot.Position;
                cam.transform.LookAt(shot.LookAt, Vector3.up);
                cam.orthographic = shot.Kind == Kind.Plan;
                if (cam.orthographic) cam.orthographicSize = shot.OrthoSize;
                else                  cam.fieldOfView      = shot.Fov;

                bool uiShot = shot.Kind == Kind.Ui;
                Canvas canvas = uiShot ? BindCanvas(cam) : null;

                try { shot.Setup?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Tour] setup for {shot.Name} failed: {e.Message}"); }

                yield return null;
                yield return null;

                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;

                string file = Path.Combine(OutputDir, $"{index:00}_{shot.Name}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Debug.Log($"[Tour] {file}");

                try { shot.Teardown?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Tour] teardown for {shot.Name} failed: {e.Message}"); }

                if (canvas != null) RestoreCanvas(canvas);
            }

            Destroy(cam.gameObject);
            rt.Release();

            // After the camera work: paths that cannot be photographed get checked instead.
            Debug.Log(BreedProbe.Run());

            File.WriteAllText(Path.Combine(OutputDir, "_done.txt"), $"{shots.Count} shots\n");
            Debug.Log($"[Tour] done — {shots.Count} shots in {OutputDir}");

            yield return null;
            if (!Application.isEditor) Application.Quit();
        }

        // ── Canvas rebinding ────────────────────────────────────────────────────

        private RenderMode _savedMode;
        private Camera     _savedCamera;
        private float      _savedPlane;

        private Canvas BindCanvas(Camera cam)
        {
            var ui = FindAnyObjectByType<GameUI>();
            var canvas = ui != null ? ui.Canvas : null;
            if (canvas == null) return null;

            _savedMode   = canvas.renderMode;
            _savedCamera = canvas.worldCamera;
            _savedPlane  = canvas.planeDistance;

            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = cam;
            canvas.planeDistance = 1f;
            return canvas;
        }

        private void RestoreCanvas(Canvas canvas)
        {
            canvas.renderMode    = _savedMode;
            canvas.worldCamera   = _savedCamera;
            canvas.planeDistance = _savedPlane;
        }

        // ── Shot list ───────────────────────────────────────────────────────────

        private List<Shot> BuildShots()
        {
            var gen  = FindAnyObjectByType<ShopGenerator>();
            var ui   = FindAnyObjectByType<GameUI>();
            var game = GameManager.Instance;

            float w = gen != null ? gen.RoomWidth  : 16f;
            float d = gen != null ? gen.RoomDepth  : 12f;
            float h = gen != null ? gen.WallHeight : 4f;
            Vector3 shop  = gen != null ? gen.ShopCentre : Vector3.zero;
            float front   = shop.z + d * 0.5f;
            float yardEnd = gen != null ? gen.YardFrontZ : 17f;

            Vector3 pens = PenCentre(out Vector3 firstPen);

            var shots = new List<Shot>
            {
                // 00-15: the world. Sixteen of them, so the HUD lands on 16 as asked.
                World("street_from_across", new Vector3(shop.x + 6f, 6.5f, yardEnd + 26f),
                                            new Vector3(shop.x + 2f, 3f, front), 55f),
                World("shopfront",          new Vector3(shop.x + 3.5f, 2.4f, front + 6f),
                                            new Vector3(shop.x, 2.4f, front), 55f),
                World("yard_from_gate",     new Vector3(shop.x + 13f, 6.5f, yardEnd - 2f),
                                            new Vector3(shop.x + 6f, 1.5f, shop.z - 6f), 60f),
                World("yard_wide",          new Vector3(shop.x + 32f, 10f, shop.z - 18f),
                                            new Vector3(shop.x + 4f, 1f, shop.z - 2f), 62f),
                World("garden",             new Vector3(shop.x + 12f, 3.2f, shop.z - 16f),
                                            new Vector3(shop.x + 15f, 1.2f, shop.z - 11f), 55f),
                World("paddock",            pens + new Vector3(-13f, 7f, -13f), pens + Vector3.up, 58f),
                // Offset diagonally: straight back from a pen puts the camera inside the
                // sign of the pen behind it.
                World("pen_close",          firstPen + new Vector3(-3.4f, 2.0f, -3.6f),
                                            firstPen + Vector3.up * 0.9f, 50f),
                World("pergola",            pens + new Vector3(-9f, 2.2f, -11f),
                                            pens + new Vector3(2f, 2.4f, 2f), 60f),
                World("interior_wide",      new Vector3(shop.x, h - 1.1f, shop.z + d * 0.5f - 1.4f),
                                            new Vector3(shop.x, 0.9f, shop.z - d * 0.35f), 68f),
                World("interior_counter",   new Vector3(shop.x + 2.2f, 1.7f, shop.z - d * 0.10f),
                                            new Vector3(shop.x - 0.5f, 1.2f, shop.z - d * 0.42f), 50f),
                World("interior_shelves",   new Vector3(shop.x + w * 0.30f, 1.65f, shop.z + d * 0.22f),
                                            new Vector3(shop.x - w * 0.28f, 1.1f, shop.z - d * 0.30f), 62f),
                World("interior_pet_decor", new Vector3(shop.x - 1.5f, 1.6f, shop.z + 1.2f),
                                            new Vector3(shop.x + w * 0.45f, 1.2f, shop.z + 2.4f), 55f),
                World("roof_detail",        new Vector3(shop.x + 13f, 16f, front + 10f),
                                            new Vector3(shop.x + 2f, 11f, shop.z), 45f),
                World("player",             PlayerShot(out Vector3 playerLook), playerLook, 45f),
                World("customer",           CustomerShot(out Vector3 custLook), custLook, 45f),
                World("aerial",             shop + new Vector3(10f, 44f, -22f), shop + Vector3.forward * 4f, 60f),
            };

            // 16 onwards: the interface, over an interior view.
            Vector3 uiFrom = new(shop.x + 1.5f, 1.65f, shop.z + d * 0.5f - 2f);
            Vector3 uiTo   = new(shop.x - 1f, 1.2f, shop.z - d * 0.4f);

            shots.Add(Ui("hud", uiFrom, uiTo, null, null));

            if (ui != null && ui.Stats != null)
            {
                shots.Add(Ui("ledger_shelves",   uiFrom, uiTo, () => { ui.Stats.Show(); ui.Stats.ShowTab(0); }, ui.Stats.Hide));
                shots.Add(Ui("ledger_animals",   uiFrom, uiTo, () => { ui.Stats.Show(); ui.Stats.ShowTab(1); }, ui.Stats.Hide));
                shots.Add(Ui("ledger_catalogue", uiFrom, uiTo, () => { ui.Stats.Show(); ui.Stats.ShowTab(2); }, ui.Stats.Hide));
                shots.Add(Ui("ledger_manage",    uiFrom, uiTo, () => { ui.Stats.Show(); ui.Stats.ShowTab(3); }, ui.Stats.Hide));
            }

            if (ui != null && ui.Info != null)
                shots.Add(Ui("info_panel", uiFrom, uiTo,
                             () => ui.Info.Show(PenDescription()), ui.Info.Hide));

            if (ui != null && ui.Results != null && game != null)
                shots.Add(Ui("day_results", uiFrom, uiTo,
                             () => ui.Results.Show(game.Shop.GetCurrentDaySummary()), ui.Results.Hide));

            if (ui != null && ui.Pause != null)
                shots.Add(Ui("pause_menu", uiFrom, uiTo,
                             () => ui.Pause.Open(),
                             () => { ui.Pause.Close(); Time.timeScale = 1f; }));

            if (game != null && game.Build != null)
                shots.Add(Ui("build_mode",
                             new Vector3(shop.x + 9f, 3.4f, shop.z - 6f),
                             new Vector3(shop.x + 13f, 0f, shop.z - 11f),
                             () => game.Build.EnterBuildMode(BuildCatalog.Get(BuildCatalog.PetPen)),
                             () => game.Build.ExitBuildMode()));

            // Plan views last: orthographic, so distances read true for design review.
            Vector3 plot = Vector3.zero;
            shots.Add(Plan("plan_yard",  plot + Vector3.up * 90f, plot, 22f));
            shots.Add(Plan("plan_block", plot + new Vector3(0f, 140f, 20f), plot + new Vector3(0f, 0f, 20f), 48f));
            shots.Add(Plan("plan_city",  plot + new Vector3(0f, 260f, 90f), plot + new Vector3(0f, 0f, 90f), 130f));
            shots.Add(World("city_oblique", plot + new Vector3(-80f, 90f, -70f),
                            plot + new Vector3(10f, 0f, 40f), 55f));

            // Appended rather than slotted in with the other panels, so the HUD stays on 16.
            if (ui != null && ui.Breeding != null)
                shots.Add(Ui("breeding_panel", uiFrom, uiTo,
                             () => ui.Breeding.Show(), ui.Breeding.Hide));

            if (ui != null && ui.StaffBoard != null)
                shots.Add(Ui("staff_board", uiFrom, uiTo,
                             () => ui.StaffBoard.Show(), ui.StaffBoard.Hide));

            // Deliveries: order a pallet and force it to land so the forecourt loop is on film.
            if (game != null && gen != null)
            {
                Vector3 forecourt = gen.ForecourtPosition;
                shots.Add(new Shot
                {
                    Name     = "delivery",
                    Kind     = Kind.World,
                    Position = forecourt + new Vector3(4.5f, 2.2f, 4.5f),
                    LookAt   = forecourt + Vector3.up * 0.7f,
                    Fov      = 55f,
                    Setup    = () =>
                    {
                        game.OrderStock(PetShop.Commerce.ProductCategory.Food, 18);
                        game.OrderStock(PetShop.Commerce.ProductCategory.Toy, 12);
                        game.Shop?.PollDeliveries(1f);   // pull both vans forward to now
                    },
                });
            }

            return shots;
        }

        // ── Shot helpers ────────────────────────────────────────────────────────

        private static Shot World(string name, Vector3 from, Vector3 to, float fov) =>
            new() { Name = name, Kind = Kind.World, Position = from, LookAt = to, Fov = fov };

        private static Shot Plan(string name, Vector3 from, Vector3 to, float size) =>
            new() { Name = name, Kind = Kind.Plan, Position = from, LookAt = to, OrthoSize = size };

        private static Shot Ui(string name, Vector3 from, Vector3 to, Action setup, Action teardown) =>
            new() { Name = name, Kind = Kind.Ui, Position = from, LookAt = to, Fov = 60f,
                    Setup = setup, Teardown = teardown };

        private Vector3 PenCentre(out Vector3 firstPen)
        {
            var pens = FindObjectsByType<PetShop.Pets.PetPen>(FindObjectsSortMode.None);
            if (pens.Length == 0) { firstPen = Vector3.zero; return Vector3.zero; }

            Vector3 centre = Vector3.zero;
            foreach (var pen in pens) centre += pen.transform.position;
            firstPen = pens[0].transform.position;
            return centre / pens.Length;
        }

        private string PenDescription()
        {
            var pen = FindAnyObjectByType<PetShop.Pets.PetPen>();
            return pen != null ? pen.Describe() : "No pens.";
        }

        private Vector3 PlayerShot(out Vector3 lookAt)
        {
            var player = GameObject.Find("Player");
            Vector3 at = player != null ? player.transform.position : Vector3.zero;
            lookAt = at + Vector3.up * 1.1f;
            return at + new Vector3(2.4f, 1.9f, 2.8f);
        }

        private Vector3 CustomerShot(out Vector3 lookAt)
        {
            var customer = FindAnyObjectByType<PetShop.Customer.CustomerAI>();
            if (customer == null)
            {
                var assistant = FindAnyObjectByType<PetShop.Commerce.Assistant>();
                Vector3 fallback = assistant != null ? assistant.transform.position : Vector3.zero;
                lookAt = fallback + Vector3.up * 1.1f;
                return fallback + new Vector3(1.8f, 1.8f, 2.4f);
            }
            Vector3 at = customer.transform.position;
            // Aimed at the thought bubble over the head, not the chest.
            lookAt = at + Vector3.up * 1.75f;
            return at + new Vector3(1.7f, 2.0f, 2.3f);
        }
    }
}
