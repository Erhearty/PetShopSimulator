using System.Collections;
using System.IO;
using UnityEngine;

namespace PetShop.Dev
{
    /// <summary>
    /// Development aid: saves PNGs of the running game so a build can be eyeballed
    /// without sitting at the machine.
    ///
    ///   PetShopSimulator.x86_64 -screenshot /tmp/shot.png -screenshotdelay 8 -screenshotcount 3
    ///
    /// Attached by GameBootstrapper only when -screenshot is on the command line.
    /// </summary>
    public class ScreenshotCapture : MonoBehaviour
    {
        public string Path      = "screenshot.png";
        public float  Delay     = 6f;
        public float  Interval  = 3f;
        public int    Count     = 1;
        public bool   QuitAfter = true;

        /// <summary>Reads the -screenshot arguments; returns false when none were given.</summary>
        public static bool TryCreate(out ScreenshotCapture capture)
        {
            capture = null;
            var args = System.Environment.GetCommandLineArgs();

            string path = null;
            float delay = 6f, interval = 3f;
            int   count = 1;

            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-screenshot":      path = args[i + 1]; break;
                    case "-screenshotdelay": float.TryParse(args[i + 1], out delay); break;
                    case "-screenshotevery": float.TryParse(args[i + 1], out interval); break;
                    case "-screenshotcount": int.TryParse(args[i + 1], out count); break;
                }
            }
            if (string.IsNullOrEmpty(path)) return false;

            var go = new GameObject("ScreenshotCapture");
            capture = go.AddComponent<ScreenshotCapture>();
            capture.Path     = path;
            capture.Delay    = delay;
            capture.Interval = interval;
            capture.Count    = Mathf.Max(1, count);
            return true;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(Delay);

            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string stem = System.IO.Path.Combine(
                dir ?? ".", System.IO.Path.GetFileNameWithoutExtension(Path));

            for (int i = 0; i < Count; i++)
            {
                string file = Count == 1 ? $"{stem}.png" : $"{stem}_{i + 1}.png";
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log($"[Screenshot] Captured {file}");
                if (i < Count - 1) yield return new WaitForSeconds(Interval);
            }

            yield return new WaitForSeconds(1.5f);
            if (QuitAfter)
            {
                Debug.Log("[Screenshot] Done — quitting.");
                Application.Quit();
            }
        }
    }
}
