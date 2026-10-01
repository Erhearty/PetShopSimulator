using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// Boots the real game world inside a PlayMode test, the same way SceneBuilder does it
    /// (a new GameObject with <see cref="GameBootstrapper"/>), and tears it down again.
    ///
    /// Statics that <see cref="PlaytestOptions.Apply"/> only ever switches ON
    /// (<see cref="ModelLibrary.ForceProcedural"/>, <see cref="SaveSystem.PathOverride"/>) are
    /// set here before the bootstrapper is added and reset here in <see cref="Teardown"/>.
    /// </summary>
    public static class PlaytestHarness
    {
        /// <summary>A model that exists only when the Asset Store street pack is installed.</summary>
        public const string KnownPackPath = "Packs/Street/Roads/Streets/Road_Streight";

        /// <summary>Frames to let GameBootstrapper.Start run before calling Begin ourselves.</summary>
        private const int FramesBeforeManualBegin = 2;

        /// <summary>Real seconds to wait for the first day to start before failing.</summary>
        private const float BootTimeoutRealSeconds = 30f;

        /// <summary>Temp save file name; lives under Application.temporaryCachePath.</summary>
        private const string SaveFileName = "petshop_playtest_save.json";

        /// <summary>The save file itself plus the backup and scratch files SaveSystem writes beside it.</summary>
        private static readonly string[] SaveFileSuffixes = { "", ".bak", ".tmp" };

        /// <summary>
        /// The only error tolerated while the world builds: editor-only, raised when the runtime
        /// NavMesh bake meets a pack mesh imported without read/write access.
        /// </summary>
        private const string ToleratedBootErrorPattern =
            @"RuntimeNavMeshBuilder: Source mesh .* does not allow read access";

        /// <summary>Most boot errors listed in a failure message.</summary>
        private const int MaxBootErrorsInMessage = 10;

        private static readonly Regex ToleratedBootError = new(ToleratedBootErrorPattern);
        /// <summary>Error / exception / assert logs captured during the last world build.</summary>
        private static readonly List<string> _bootErrors = new();
        private static bool _capturingBootLogs;
        /// <summary>LogAssert.ignoreFailingMessages as it was before the capture started.</summary>
        private static bool _savedIgnoreFailingMessages;

        private static readonly HashSet<GameObject> _preexistingRoots = new();
        private static bool   _booted;
        private static string _savePath;
        /// <summary>Result of the first <see cref="PacksInstalled"/> probe; null until then.</summary>
        private static bool?  _packsInstalled;

        /// <summary>The bootstrapper added by the last <see cref="Boot"/>, or null.</summary>
        public static GameBootstrapper Bootstrapper { get; private set; }

        /// <summary>The temp file saves are redirected to while a test is booted.</summary>
        public static string TempSavePath => Path.Combine(Application.temporaryCachePath, SaveFileName);

        /// <summary>The live game, or null when nothing is booted.</summary>
        public static GameManager Game => GameManager.Instance;

        /// <summary>
        /// Builds the world and waits until the first trading day is running.
        /// </summary>
        /// <param name="packs">False forces the procedural / CC0 fallbacks.</param>
        /// <param name="seed">Seed for UnityEngine.Random, applied before anything spawns.</param>
        /// <param name="timeScale">Time.timeScale once the day has started.</param>
        /// <param name="dayLength">Game seconds per trading day (GameManager.DayLengthSeconds).</param>
        public static IEnumerator Boot(bool packs, int seed, float timeScale, float dayLength)
        {
            StartCapturingBootLogs();
            try
            {
                yield return BuildWorld(packs, seed, dayLength);
            }
            finally
            {
                StopCapturingBootLogs();
            }
            AssertNoBootErrors();
            Time.timeScale = timeScale;
        }

        /// <summary>Adds the bootstrapper and waits until the first trading day is running.</summary>
        private static IEnumerator BuildWorld(bool packs, int seed, float dayLength)
        {
            RememberPreexistingRoots();
            PrepareStatics(packs, seed);

            Bootstrapper = new GameObject("PlaytestBootstrapper").AddComponent<GameBootstrapper>();
            GameManager game = GameManager.Instance;
            Assert.IsNotNull(game, "GameBootstrapper.Awake did not create a GameManager.");
            // Awake has already run (and applied any -daylength), so this override sticks.
            game.DayLengthSeconds = dayLength;

            for (int i = 0; i < FramesBeforeManualBegin; i++) yield return null;
            if (!game.IsDayRunning) BeginManually(game);

            yield return WaitForDayRunning(game);
        }

        /// <summary>
        /// Restores timeScale, destroys every root object the boot created, resets the
        /// playtest statics and deletes the temp save. Safe to call more than once.
        /// </summary>
        public static void Teardown()
        {
            // A boot that failed part-way never reached its finally; stop capturing here too.
            StopCapturingBootLogs();
            Time.timeScale = 1f;
            if (_booted) DestroyBootedRoots();

            _booted      = false;
            Bootstrapper = null;
            _preexistingRoots.Clear();

            ModelLibrary.ForceProcedural = false;
            ModelLibrary.ResetCache();
            SaveSystem.PathOverride = null;
            DeleteSaveFiles();
        }

        /// <summary>
        /// True when the Asset Store packs are on disk. Ignores ForceProcedural for the check.
        /// Probed once and cached: only that first probe (made before any world boots) clears the
        /// model cache, so later calls never wipe ModelLibrary.MissingPaths or ModelLibrary.PackFallbacks between Boot and the
        /// assertions. Boot/Teardown's own ResetCache is what clears the cache between worlds.
        /// </summary>
        public static bool PacksInstalled()
        {
            if (_packsInstalled.HasValue) return _packsInstalled.Value;

            bool forced = ModelLibrary.ForceProcedural;
            ModelLibrary.ForceProcedural = false;
            bool installed = ModelLibrary.Has(KnownPackPath);
            ModelLibrary.ForceProcedural = forced;
            ModelLibrary.ResetCache();
            _packsInstalled = installed;
            return installed;
        }

        /// <summary>Formats up to <paramref name="cap"/> names, noting how many more were left out.</summary>
        public static string ListNames(IReadOnlyList<string> names, int cap)
        {
            int shown = Mathf.Min(cap, names.Count);
            var head = new List<string>(shown);
            for (int i = 0; i < shown; i++) head.Add(names[i]);

            string list = string.Join(", ", head);
            return names.Count > cap ? $"{list} (+{names.Count - cap} more)" : list;
        }

        /// <summary>Stops boot errors failing the test by themselves and records them instead.</summary>
        private static void StartCapturingBootLogs()
        {
            StopCapturingBootLogs();
            _bootErrors.Clear();
            _savedIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnBootLog;
            _capturingBootLogs = true;
        }

        /// <summary>Unsubscribes and restores LogAssert.ignoreFailingMessages. Safe to call more than once.</summary>
        private static void StopCapturingBootLogs()
        {
            if (!_capturingBootLogs) return;
            Application.logMessageReceived -= OnBootLog;
            LogAssert.ignoreFailingMessages = _savedIgnoreFailingMessages;
            _capturingBootLogs = false;
        }

        private static void OnBootLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            if (ToleratedBootError.IsMatch(condition)) return;
            _bootErrors.Add($"{type}: {condition}");
        }

        private static void AssertNoBootErrors()
        {
            if (_bootErrors.Count == 0) return;
            Assert.Fail($"{_bootErrors.Count} error logs while building the world: "
                        + ListNames(_bootErrors, MaxBootErrorsInMessage));
        }

        private static void RememberPreexistingRoots()
        {
            // The test runner keeps its own objects in the active scene; they must survive Teardown.
            _preexistingRoots.Clear();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                _preexistingRoots.Add(root);
            _booted = true;
        }

        private static void PrepareStatics(bool packs, int seed)
        {
            ModelLibrary.ForceProcedural = !packs;
            ModelLibrary.ResetCache();

            _savePath = TempSavePath;
            DeleteSaveFiles();
            SaveSystem.PathOverride = _savePath;

            Random.InitState(seed);
        }

        /// <summary>Outside batch mode Start leaves the title screen up; dismiss it and open the shop.</summary>
        private static void BeginManually(GameManager game)
        {
            var ui = Object.FindAnyObjectByType<GameUI>();
            if (ui != null && ui.Title != null) ui.Title.Hide();
            game.SetModalOpen(false);
            game.Begin();
        }

        private static IEnumerator WaitForDayRunning(GameManager game)
        {
            float deadline = Time.realtimeSinceStartup + BootTimeoutRealSeconds;
            while (game != null && !game.IsDayRunning)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"The first day did not start within {BootTimeoutRealSeconds} s.");
                yield return null;
            }
            Assert.IsNotNull(game, "The GameManager was destroyed while booting.");
        }

        private static void DestroyBootedRoots()
        {
            // DestroyImmediate so GameManager/AudioManager clear their Instance before the next Boot.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_preexistingRoots.Contains(root)) Object.DestroyImmediate(root);
        }

        private static void DeleteSaveFiles()
        {
            if (string.IsNullOrEmpty(_savePath)) return;
            foreach (string suffix in SaveFileSuffixes)
            {
                string file = _savePath + suffix;
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
