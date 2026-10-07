using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetShop.UI
{
    /// <summary>
    /// Shows an accent outline around a control while it holds keyboard selection, so the
    /// focused button is obvious when navigating with the keyboard. Added by <see cref="UIFactory.Button"/>.
    /// </summary>
    public class FocusOutline : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Outline _outline;

        /// <summary>Whether the outline is currently showing.</summary>
        public bool IsShowing => _outline != null && _outline.enabled;

        private void Awake() => Ensure();

        private void Ensure()
        {
            if (_outline != null) return;
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor    = UIFactory.FocusRing;
            _outline.effectDistance = new Vector2(UIFactory.FocusRingWidth, -UIFactory.FocusRingWidth);
            _outline.enabled        = false;
        }

        /// <inheritdoc/>
        public void OnSelect(BaseEventData eventData) { Ensure(); _outline.enabled = true; }

        /// <inheritdoc/>
        public void OnDeselect(BaseEventData eventData) { Ensure(); _outline.enabled = false; }
    }
}
