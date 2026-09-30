using System.Globalization;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Command-line switches for automated playtests and soak runs:
    /// <c>-seed N</c>, <c>-nopacks</c>, <c>-savepath P</c>, <c>-telemetry P</c> and
    /// <c>-quitafterdays N</c>. <c>-daylength</c> is deliberately not handled here —
    /// <see cref="GameManager"/> owns it.
    ///
    /// With none of these flags given nothing changes: no seed is forced, the Asset Store
    /// packs load as usual and saves go to the normal slot.
    /// </summary>
    public static class PlaytestOptions
    {
        /// <summary>Seeds UnityEngine.Random so a run is repeatable.</summary>
        public const string SeedFlag = "-seed";

        /// <summary>Forces the procedural / CC0 fallbacks by hiding the Asset Store packs.</summary>
        public const string NoPacksFlag = "-nopacks";

        /// <summary>Redirects the save slot to an isolated file.</summary>
        public const string SavePathFlag = "-savepath";

        /// <summary>Appends one JSON line per day to this file.</summary>
        public const string TelemetryFlag = "-telemetry";

        /// <summary>Quits the player after this many days have ended.</summary>
        public const string QuitAfterDaysFlag = "-quitafterdays";

        /// <summary>The parsed values of the playtest switches. Default means "no flags given".</summary>
        public struct Settings
        {
            /// <summary>Gameplay seed, or null to leave UnityEngine.Random unseeded.</summary>
            public int? Seed;

            /// <summary>True to hide every model under Resources/Packs/.</summary>
            public bool NoPacks;

            /// <summary>Save file to use instead of the default slot, or null.</summary>
            public string SavePath;

            /// <summary>JSON-lines telemetry file, or null for no telemetry file.</summary>
            public string TelemetryPath;

            /// <summary>Days to play before quitting, or null to run indefinitely.</summary>
            public int? QuitAfterDays;
        }

        /// <summary>The settings most recently passed to <see cref="Apply"/>.</summary>
        public static Settings Current { get; private set; }

        /// <summary>The applied gameplay seed, or null when none was given.</summary>
        public static int? Seed => Current.Seed;

        /// <summary>The applied telemetry file path, or null.</summary>
        public static string TelemetryPath => Current.TelemetryPath;

        /// <summary>The applied day limit, or null.</summary>
        public static int? QuitAfterDays => Current.QuitAfterDays;

        /// <summary>
        /// Pure parse of <paramref name="args"/> (typically Environment.GetCommandLineArgs()).
        /// Unknown arguments are skipped; malformed values and value flags with no value are
        /// ignored with a warning.
        /// </summary>
        public static Settings Parse(string[] args)
        {
            var settings = new Settings();
            if (args == null) return settings;

            for (int i = 0; i < args.Length; i++)
            {
                string flag = args[i]?.ToLowerInvariant();
                if (flag == NoPacksFlag) { settings.NoPacks = true; continue; }
                if (!IsValueFlag(flag)) continue;
                if (!TryTakeValue(args, i, flag, out string value)) continue;

                i++;
                ApplyValue(ref settings, flag, value);
            }
            return settings;
        }

        /// <summary>
        /// Makes <paramref name="settings"/> live: seeds UnityEngine.Random, toggles
        /// <see cref="ModelLibrary.ForceProcedural"/> and sets <see cref="SaveSystem.PathOverride"/>.
        /// </summary>
        public static void Apply(Settings settings)
        {
            Current = settings;
            if (settings.Seed.HasValue)
            {
                Random.InitState(settings.Seed.Value);
                Debug.Log($"[Game] Gameplay seed set to {settings.Seed.Value} from the command line.");
            }

            // Only switch things ON: an absent flag must not undo a value a PlayMode test harness
            // set before adding GameBootstrapper (whose Awake calls this).
            if (settings.NoPacks)
            {
                ModelLibrary.ForceProcedural = true;
                Debug.Log("[Game] Asset Store packs disabled from the command line.");
            }

            if (!string.IsNullOrEmpty(settings.SavePath))
            {
                SaveSystem.PathOverride = settings.SavePath;
                Debug.Log($"[Game] Save path set to {settings.SavePath} from the command line.");
            }
        }

        private static bool IsValueFlag(string flag) =>
            flag == SeedFlag || flag == SavePathFlag || flag == TelemetryFlag || flag == QuitAfterDaysFlag;

        private static bool IsKnownFlag(string arg)
        {
            string flag = arg?.ToLowerInvariant();
            return flag == NoPacksFlag || IsValueFlag(flag);
        }

        /// <summary>The argument after <paramref name="index"/>, unless it is missing or another flag.</summary>
        /// <summary>True for any other command-line switch (e.g. -daylength, -batchmode); a negative number is a value.</summary>
        private static bool LooksLikeFlag(string arg) =>
            arg.StartsWith("-", System.StringComparison.Ordinal)
            && !int.TryParse(arg, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out _);

        private static bool TryTakeValue(string[] args, int index, string flag, out string value)
        {
            value = index + 1 < args.Length ? args[index + 1] : null;
            if (!string.IsNullOrEmpty(value) && !IsKnownFlag(value) && !LooksLikeFlag(value)) return true;

            Debug.LogWarning($"[Game] Ignoring {flag}: it needs a value.");
            value = null;
            return false;
        }

        private static void ApplyValue(ref Settings settings, string flag, string value)
        {
            switch (flag)
            {
                case SeedFlag:          settings.Seed          = ParseInt(flag, value, requirePositive: false); break;
                case QuitAfterDaysFlag: settings.QuitAfterDays = ParseInt(flag, value, requirePositive: true);  break;
                case SavePathFlag:      settings.SavePath      = value; break;
                case TelemetryFlag:     settings.TelemetryPath = value; break;
            }
        }

        /// <summary>Invariant-culture integer, or null (with a warning) when malformed or out of range.</summary>
        private static int? ParseInt(string flag, string value, bool requirePositive)
        {
            bool ok = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
            if (ok && (!requirePositive || n > 0)) return n;

            string kind = requirePositive ? "positive whole number" : "whole number";
            Debug.LogWarning($"[Game] Ignoring {flag}: '{value}' is not a {kind}.");
            return null;
        }
    }
}
