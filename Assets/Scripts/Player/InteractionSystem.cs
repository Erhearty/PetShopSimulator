using UnityEngine;
using TMPro;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;

namespace PetShop.Player
{
    /// <summary>
    /// Looks for furniture in front of the player, shows a context prompt, and runs the
    /// interaction when E is pressed.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text PromptText;

        [Header("Tuning")]
        public float Range       = 2.6f;
        public float CastRadius  = 0.5f;
        public float EyeHeight   = 1.1f;

        private GameManager _game;

        private void Start()
        {
            _game = GameManager.Instance;
            if (PromptText == null)
            {
                var go = GameObject.Find("InteractPrompt");
                if (go != null) PromptText = go.GetComponent<TMP_Text>();
            }
        }

        private void Update()
        {
            if (PromptText == null) return;

            if (_game != null && (_game.IsBuildModeActive || _game.IsModalOpen || _game.IsGameOver))
            {
                PromptText.enabled = false;
                return;
            }

            string label = FindTarget(out var hit) ? PromptFor(hit.collider) : null;
            PromptText.text    = label ?? string.Empty;
            PromptText.enabled = !string.IsNullOrEmpty(label);
        }

        /// <summary>Called by PlayerController when the interact key goes down.</summary>
        public void TryInteract()
        {
            if (_game == null) _game = GameManager.Instance;
            if (_game != null && _game.IsBuildModeActive) return;
            if (!FindTarget(out var hit)) return;

            var shelf = hit.collider.GetComponentInParent<ShelfUnit>();
            if (shelf != null) { _game?.RestockShelf(shelf); return; }

            var pen = hit.collider.GetComponentInParent<PetPen>();
            if (pen != null) { _game?.InspectPen(pen); return; }

            var counter = hit.collider.GetComponentInParent<CounterInteractable>();
            if (counter != null) { _game?.UseCounter(); }
        }

        private string PromptFor(Collider col)
        {
            var shelf = col.GetComponentInParent<ShelfUnit>();
            if (shelf != null)
                return shelf.IsEmpty
                    ? "[E]  Restock shelf"
                    : $"[E]  Restock {shelf.Category} shelf  ({shelf.TotalUnits} units left)";

            var pen = col.GetComponentInParent<PetPen>();
            if (pen != null)
            {
                if (pen.NeedsService)
                    return $"[E]  Feed & clean the {pen.PenSpecies} pen  (€{pen.ServiceCost:N0})";
                return pen.HasSpace
                    ? $"[E]  Buy a {pen.PenSpecies}  (€{Pet.WholesalePrice(pen.PenSpecies):N0})   ·  {pen.Count}/{pen.Capacity} in the pen"
                    : $"[E]  {pen.PenSpecies} pen — full ({pen.Count}/{pen.Capacity})";
            }

            if (col.GetComponentInParent<CounterInteractable>() != null)
            {
                var queue = _game != null ? _game.Queue : null;
                if (queue != null && queue.AnyWaiting)
                    return $"[E]  Serve {queue.Front.ShopperName}  —  {queue.Front.BasketCount} item(s), " +
                           $"€{queue.Front.BasketValue:N2}   ({queue.Length} waiting)";
                return "[E]  Check the books";
            }

            return null;
        }

        private bool FindTarget(out RaycastHit hit)
        {
            Vector3 origin = transform.position + Vector3.up * EyeHeight;
            return Physics.SphereCast(origin, CastRadius, transform.forward, out hit,
                                      Range, GameLayers.InteractMask, QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>Marker for the shop counter — interacting opens the day's books.</summary>
    public class CounterInteractable : MonoBehaviour { }
}
