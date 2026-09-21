using UnityEngine;
using PetShop.Core;

namespace PetShop.Commerce
{
    /// <summary>
    /// A hired shop assistant. Stands behind the till and serves the queue on their own,
    /// more slowly than the player would, for a daily wage.
    ///
    /// This is the counterweight to the checkout queue: without staff you must be behind the
    /// counter whenever anyone is ready to pay, which makes the rest of the shop impossible
    /// to run. Hiring trades margin for freedom.
    /// </summary>
    public class Assistant : MonoBehaviour
    {
        [Header("Work rate")]
        [Tooltip("Seconds to ring up one customer. The player is instant.")]
        public float ServiceSeconds = 4.5f;

        [Header("Economics")]
        public float DailyWage = 55f;

        /// <summary>Who they are, for notifications and the staff board.</summary>
        public string StaffName = "Assistant";

        public CheckoutQueue Queue;
        public ShopManager   Shop;

        private float _timer;
        private CharacterVisual _visual;

        /// <summary>Builds an assistant standing at the till, facing the queue.</summary>
        public static Assistant Create(Transform parent, Vector3 position, Vector3 facing,
                                       CheckoutQueue queue, ShopManager shop, int index)
        {
            var go = new GameObject($"Assistant_{index}") { layer = GameLayers.Character };
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (facing.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            var assistant = go.AddComponent<Assistant>();
            assistant.Queue = queue;
            assistant.Shop  = shop;

            // Stationary, so the animator only ever needs the idle state.
            assistant._visual = CharacterFactory.Attach(go, () => Vector3.zero, variant: index + 3);
            return assistant;
        }

        private void Update()
        {
            if (Queue == null || !Queue.AnyWaiting) { _timer = 0f; return; }

            _timer += Time.deltaTime;
            if (_timer < ServiceSeconds) return;
            _timer = 0f;

            var shopper = Queue.Front;
            if (shopper == null) return;

            float value = shopper.BasketValue;
            Queue.ServeFront();
            AudioManager.Instance?.PlaySfx("sale", 0.6f);
            GameManager.Instance?.Notify($"{name.Replace('_', ' ')} served {shopper.ShopperName} — €{value:N2}");
        }
    }
}
