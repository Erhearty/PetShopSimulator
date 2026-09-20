using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Dev
{
    /// <summary>
    /// Flies a camera through a set of framed viewpoints and writes a PNG of each, so the
    /// game can be looked at without anyone sitting in front of it.
    ///
    /// Renders through an off-screen RenderTexture rather than ScreenCapture, because that
    /// works in batch mode where there is no game view — which is how the editor-side
    /// <c>SceneShot</c> drives it. The same component serves a normal player build.
    ///
    ///   -tour &lt;dir&gt;   capture into that directory, then quit
    /// </summary>
    public class CameraTour : MonoBehaviour
    {
        public string OutputDir = "Screenshots";
        public int    Width     = 1280;
        public int    Height    = 720;
        public float  WarmupSeconds = 2.5f;

        public struct Shot
        {
            public string  Name;
            public Vector3 Position;
            public Vector3 LookAt;
            public float   Fov;
            public float   OrthoSize;   // > 0 renders orthographic, for plan views

            public Shot(string name, Vector3 position, Vector3 lookAt, float fov = 60f, float orthoSize = 0f)
            { Name = name; Position = position; LookAt = lookAt; Fov = fov; OrthoSize = orthoSize; }
        }

        /// <summary>Reads -tour from the command line; returns false when it is absent.</summary>
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
            // Let the shop generate, the NavMesh bake and a few customers appear.
            // WaitForSeconds is fine here (batch mode still advances time), but guard against
            // a world that never finished building.
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
            cam.farClipPlane    = 600f;

            var rt  = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            int index = 0;
            foreach (var shot in shots)
            {
                cam.transform.position = shot.Position;
                cam.transform.LookAt(shot.LookAt, Vector3.up);
                cam.orthographic = shot.OrthoSize > 0f;
                if (cam.orthographic) cam.orthographicSize = shot.OrthoSize;
                else                  cam.fieldOfView      = shot.Fov;

                // Two plain frames to let animation and transforms settle. Deliberately not
                // WaitForEndOfFrame: that never fires in batch mode, where there is no screen
                // to end a frame on, and the coroutine would hang forever.
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
                index++;
            }

            Destroy(cam.gameObject);
            rt.Release();

            // Marker the editor-side driver polls for, so it knows when to exit.
            File.WriteAllText(Path.Combine(OutputDir, "_done.txt"), $"{index} shots\n");
            Debug.Log($"[Tour] done — {index} shots in {OutputDir}");

            yield return new WaitForSeconds(0.3f);

            // In a player build, quit. In the editor, SceneShot sees the _done marker and
            // exits the editor itself — calling Application.Quit from editor play mode tears
            // down the windowing system underneath Unity and crashes it on the way out.
            if (!Application.isEditor) Application.Quit();
        }

        /// <summary>
        /// Viewpoints derived from the live scene rather than hard-coded, so they stay framed
        /// when the shop or the street is resized.
        /// </summary>
        private List<Shot> BuildShots()
        {
            var gen = FindAnyObjectByType<ShopGenerator>();
            float w  = gen != null ? gen.RoomWidth  : 20f;
            float d  = gen != null ? gen.RoomDepth  : 16f;
            float h  = gen != null ? gen.WallHeight : 4f;
            Vector3 shop = gen != null ? gen.ShopCentre : Vector3.zero;

            float front = shop.z + d * 0.5f;
            var shots = new List<Shot>
            {
                // Kept inside the yard: viewpoints out on the pavement end up with a street
                // tree or a lamp post filling the frame.
                new("yard_from_gate",     new Vector3(shop.x + 14f, 7f, front + 5f),
                                          new Vector3(shop.x + 6f, 1.5f, shop.z - 4f), 60f),
                new("shopfront",          new Vector3(shop.x + 3.5f, 2.4f, front + 6f),
                                          new Vector3(shop.x, 2.4f, front), 55f),
                new("yard_wide",          new Vector3(shop.x + 30f, 9f, shop.z - 14f),
                                          new Vector3(shop.x + 4f, 1f, shop.z - 2f), 62f),
                new("interior_wide",      new Vector3(shop.x, h - 0.9f, shop.z + d * 0.5f - 1.5f),
                                          new Vector3(shop.x, 0.8f, shop.z - d * 0.3f), 68f),
                new("interior_counter",   new Vector3(shop.x + w * 0.22f, 1.9f, shop.z + 1.5f),
                                          new Vector3(shop.x, 1.1f, shop.z - d * 0.45f), 55f),
                new("interior_shelves",   new Vector3(shop.x + w * 0.30f, 1.65f, shop.z + d * 0.22f),
                                          new Vector3(shop.x - w * 0.28f, 1.1f, shop.z - d * 0.32f), 62f),
                new("interior_till",      new Vector3(shop.x + 2.2f, 1.7f, shop.z - d * 0.12f),
                                          new Vector3(shop.x - 0.5f, 1.2f, shop.z - d * 0.42f), 50f),
            };

            // A look at the pens, wherever they ended up
            var pens = FindObjectsByType<PetShop.Pets.PetPen>(FindObjectsSortMode.None);
            if (pens.Length > 0)
            {
                Vector3 centre = Vector3.zero;
                foreach (var pen in pens) centre += pen.transform.position;
                centre /= pens.Length;
                shots.Add(new Shot("pens", centre + new Vector3(0f, 5.5f, -9f), centre, 58f));
                shots.Add(new Shot("pen_close", pens[0].transform.position + new Vector3(0f, 1.6f, -3.2f),
                                   pens[0].transform.position + Vector3.up * 0.4f, 45f));
            }

            // Whatever the player is looking at
            var player = GameObject.Find("Player");
            if (player != null)
                shots.Add(new Shot("player", player.transform.position + new Vector3(2.2f, 1.8f, 2.6f),
                                   player.transform.position + Vector3.up * 1.1f, 45f));

            // A customer, if any are in
            var customer = FindAnyObjectByType<PetShop.Customer.CustomerAI>();
            if (customer != null)
                shots.Add(new Shot("customer", customer.transform.position + new Vector3(1.6f, 1.7f, 2.2f),
                                   customer.transform.position + Vector3.up * 1.0f, 42f));

            shots.Add(new Shot("aerial", shop + new Vector3(0f, 40f, -18f), shop + Vector3.forward * 6f, 60f));

            // Plan views for design review. Orthographic so distances read true — a
            // perspective "top view" makes the far side of the block look smaller than it is.
            Vector3 plot = new(0f, 0f, 0f);
            shots.Add(new Shot("plan_yard", plot + Vector3.up * 90f, plot, 60f, orthoSize: 22f));
            shots.Add(new Shot("plan_block", plot + new Vector3(0f, 140f, 18f),
                               plot + new Vector3(0f, 0f, 18f), 60f, orthoSize: 46f));
            shots.Add(new Shot("plan_city", plot + new Vector3(0f, 240f, 60f),
                               plot + new Vector3(0f, 0f, 60f), 60f, orthoSize: 110f));

            // A raking bird's-eye, which reads better than straight down for composition.
            shots.Add(new Shot("city_oblique", plot + new Vector3(-70f, 80f, -60f),
                               plot + new Vector3(10f, 0f, 30f), 55f));
            return shots;
        }
    }
}
