using UnityEngine;
using TMPro;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>
    /// Drives the HUD balance label: the shown figure counts up (or down) towards the real
    /// balance and flashes the accent colour briefly when money comes in. With
    /// <see cref="GameSettings.ReduceMotion"/> on it snaps instead. The text is only rebuilt
    /// when the rounded figure changes, so steady frames allocate nothing.
    /// </summary>
    public class BalanceTicker : MonoBehaviour
    {
        /// <summary>Seconds the accent flash takes to fade back to the base colour.</summary>
        public const float FlashSeconds = 0.45f;
        /// <summary>Fraction of the remaining gap closed per second.</summary>
        private const float CatchUpRate = 8f;
        /// <summary>Minimum euros per second, so the last few euros do not crawl.</summary>
        private const float MinStepPerSecond = 40f;

        private TMP_Text _label;
        private float    _target;
        private float    _shown;
        private float    _flash;
        private long     _lastShownRounded = long.MinValue;
        private Color    _baseColour;
        private bool     _initialised;

        /// <summary>The figure currently displayed (mid-tick it trails the real balance).</summary>
        public float Shown => _shown;

        /// <summary>Sets the balance to tick towards and the label colour once the flash fades.</summary>
        public void SetTarget(float balance, Color baseColour)
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            _baseColour = baseColour;
            if (!_initialised || GameSettings.ReduceMotion) { _shown = balance; _initialised = true; }
            else if (balance > _target) _flash = FlashSeconds;
            _target = balance;
            Render();
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Advances the count-up and flash by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (_label == null) return;
            if (!Mathf.Approximately(_shown, _target))
            {
                float gap  = Mathf.Abs(_target - _shown);
                float step = Mathf.Max(gap * CatchUpRate, MinStepPerSecond) * dt;
                _shown = Mathf.MoveTowards(_shown, _target, step);
            }
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt);
            Render();
        }

        private void Render()
        {
            if (_label == null) return;
            long rounded = (long)Mathf.Round(_shown);
            if (rounded != _lastShownRounded)
            {
                _lastShownRounded = rounded;
                _label.text = $"€ {_shown:N0}";
            }
            _label.color = Color.Lerp(_baseColour, UIFactory.Accent, _flash / FlashSeconds);
        }
    }
}
