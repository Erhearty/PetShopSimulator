using UnityEngine;

namespace PetShop.UI
{
    /// <summary>
    /// Reference-counted freeze of <see cref="Time.timeScale"/>. Every panel that stops the clock
    /// (pause menu, Shop book) holds it through <see cref="Acquire"/> / <see cref="Release"/>, so
    /// one closing never un-pauses the game while another is still open, and the running speed
    /// (a -timescale soak run is not 1x) is restored exactly once, when the last hold is released.
    /// </summary>
    public static class GamePause
    {
        private static int   _holds;
        private static float _resumeScale = 1f;

        /// <summary>True while at least one panel holds the pause.</summary>
        public static bool IsPaused => _holds > 0;

        /// <summary>Number of outstanding holds.</summary>
        public static int Holds => _holds;

        /// <summary>Adds a hold; the first one remembers the running speed and freezes time.</summary>
        public static void Acquire()
        {
            if (_holds++ == 0)
            {
                if (Time.timeScale > 0f) _resumeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        /// <summary>Drops a hold; the last one restores the remembered speed. Extra releases are ignored.</summary>
        public static void Release()
        {
            if (_holds == 0) return;
            if (--_holds == 0) Time.timeScale = _resumeScale;
        }

        /// <summary>Drops every hold without touching the time scale. Test seam.</summary>
        internal static void ResetForTests()
        {
            _holds = 0;
            _resumeScale = 1f;
        }
    }
}
