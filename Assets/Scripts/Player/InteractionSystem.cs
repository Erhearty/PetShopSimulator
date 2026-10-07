using UnityEngine;
using TMPro;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Localization;

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

        /// <summary>Most overlapping interactables the fallback probe considers.</summary>
        private const int MaxOverlaps = 8;

        private GameManager _game;
        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];

        /// <summary>The interact key's label, as shown in prompts ("E" by default).</summary>
        private static string Key => InputBindings.Label(GameAction.Interact);

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

            if (_game != null && (_game.IsBuildModeActive || _game.IsModalOpen ||
                                  _game.IsBuildViewActive || _game.IsGameOver))
            {
                PromptText.enabled = false;
                return;
            }

            string label = FindTarget(out var target) ? PromptFor(target) : null;
            PromptText.text    = label ?? string.Empty;
            PromptText.enabled = !string.IsNullOrEmpty(label);
        }

        /// <summary>Called by PlayerController when the interact key goes down.</summary>
        public void TryInteract()
        {
            if (_game == null) _game = GameManager.Instance;
            if (_game != null && _game.IsBuildModeActive) return;
            if (!FindTarget(out var target)) return;

            // Deliveries first: a pallet parked by a shelf should hand over its stock,
            // not silently restock the shelf behind it.
            var crate = target.GetComponentInParent<DeliveryCrate>();
            if (crate != null) { _game?.CollectDelivery(crate); return; }

            var shelf = target.GetComponentInParent<ShelfUnit>();
            if (shelf != null) { _game?.RestockShelf(shelf); return; }

            var pen = target.GetComponentInParent<PetPen>();
            if (pen != null) { _game?.InspectPen(pen); return; }

            var counter = target.GetComponentInParent<CounterInteractable>();
            if (counter != null) { _game?.UseCounter(); }
        }

        private string PromptFor(Collider col)
        {
            var crate = col.GetComponentInParent<DeliveryCrate>();
            if (crate != null) return crate.Prompt;

            var shelf = col.GetComponentInParent<ShelfUnit>();
            if (shelf != null)
            {
                var shop  = _game != null ? _game.Shop : null;
                int ready = shop != null ? shop.Warehouse(shelf.Category) : 0;
                string source = ready > 0 ? Loc.F("prompt.source.stockroom", ready) : Loc.T("prompt.source.cash_and_carry");

                return shelf.IsEmpty
                    ? Loc.F("prompt.restock_shelf", Key, source)
                    : Loc.F("prompt.restock_category", Key, CategoryName(shelf.Category), shelf.TotalUnits, source);
            }

            var pen = col.GetComponentInParent<PetPen>();
            if (pen != null)
            {
                if (pen.NeedsService)
                    return Loc.F("prompt.pen_service", Key, SpeciesName(pen.PenSpecies), pen.ServiceCost);
                return pen.HasSpace
                    ? Loc.F("prompt.pen_buy", Key, SpeciesName(pen.PenSpecies), Pet.WholesalePrice(pen.PenSpecies), pen.Count, pen.Capacity)
                    : Loc.F("prompt.pen_full", Key, SpeciesName(pen.PenSpecies), pen.Count, pen.Capacity);
            }

            if (col.GetComponentInParent<CounterInteractable>() != null)
            {
                var queue = _game != null ? _game.Queue : null;
                if (queue != null && queue.AnyWaiting)
                    return Loc.F("prompt.serve", Key, queue.Front.ShopperName, queue.Front.BasketCount,
                                 queue.Front.BasketValue, queue.Length);
                return EmptyCounterPrompt;
            }

            return null;
        }

        /// <summary>The counter's prompt while nobody is queueing, so the player knows E works there.</summary>
        internal static string EmptyCounterPrompt => Loc.F("prompt.counter_empty", Key);

        /// <summary>A species' name in the current language.</summary>
        internal static string SpeciesName(Pet.Species species) =>
            Loc.T("species." + species.ToString().ToLowerInvariant());

        /// <summary>A product category's name in the current language, falling back to the enum name.</summary>
        internal static string CategoryName(ProductCategory category)
        {
            string key = "category." + category.ToString().ToLowerInvariant();
            return Loc.Has(key) ? Loc.T(key) : category.ToString();
        }

        /// <summary>
        /// The interactable the player is facing: the first one a sphere cast from the eyes hits,
        /// else the nearest one overlapping a probe just ahead (see <see cref="NearestOverlap"/>).
        /// </summary>
        private bool FindTarget(out Collider target)
        {
            Vector3 origin = transform.position + Vector3.up * EyeHeight;
            if (Physics.SphereCast(origin, CastRadius, transform.forward, out var hit,
                                   Range, GameLayers.InteractMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider;
                return true;
            }
            target = NearestOverlap(origin);
            return target != null;
        }

        /// <summary>
        /// A sphere cast ignores colliders it starts inside, so standing pressed against the
        /// counter found nothing. This catches an interactable overlapping a sphere just ahead of
        /// the eyes, nearest first.
        /// </summary>
        private Collider NearestOverlap(Vector3 origin)
        {
            Vector3 probe = origin + transform.forward * CastRadius;
            int count = Physics.OverlapSphereNonAlloc(probe, CastRadius, _overlaps,
                                                      GameLayers.InteractMask, QueryTriggerInteraction.Ignore);
            Collider best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float distance = (_overlaps[i].bounds.ClosestPoint(probe) - probe).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = _overlaps[i]; }
            }
            return best;
        }
    }

    /// <summary>Marker for the shop counter — interacting serves the next waiting customer.</summary>
    public class CounterInteractable : MonoBehaviour { }
}
