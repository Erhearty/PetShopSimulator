using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Core;

namespace PetShop.Commerce
{
    /// <summary>
    /// A hired shop assistant, working one of three jobs for a daily wage:
    /// a <see cref="StaffRole.Cashier"/> stands behind the till and serves the queue, more
    /// slowly than the player would; a <see cref="StaffRole.Restocker"/> carries delivered stock
    /// from the stockroom onto the shelves; a <see cref="StaffRole.Feeder"/> keeps the pens fed
    /// and clean, paying their upkeep from the till.
    ///
    /// This is the counterweight to running the shop alone: hiring trades margin for freedom.
    /// Floor-work behaviour (restocking, feeding, walking) lives in Assistant.Floorwork.cs.
    /// </summary>
    public partial class Assistant : MonoBehaviour
    {
        [Header("Work rate")]
        [Tooltip("Seconds to ring up one customer. The player is instant.")]
        public float ServiceSeconds = 4.5f;

        [Header("Economics")]
        public float DailyWage = 55f;

        /// <summary>Who they are, for notifications and the staff board.</summary>
        public string StaffName = "Assistant";

        /// <summary>The queue a cashier serves.</summary>
        public CheckoutQueue Queue;
        /// <summary>The shop whose stockroom and balance this assistant works with.</summary>
        public ShopManager   Shop;
        /// <summary>Products to put on a shelf that has nothing on it yet; optional.</summary>
        public ItemDatabase  Catalog;

        /// <summary>The job they are doing now. Change it with <see cref="SetRole"/>.</summary>
        public StaffRole Role { get; private set; } = StaffRole.Cashier;

        /// <summary>How good they are, <see cref="StaffCandidate.MinSkill"/>..<see cref="StaffCandidate.MaxSkill"/>.</summary>
        public int Skill { get; private set; } = StaffCandidate.MinSkill;

        private float           _timer;
        private CharacterVisual _visual;
        private NavMeshAgent    _agent;
        private Coroutine       _routine;
        private Vector3         _homePosition;
        private Quaternion      _homeRotation;

        // Agent tuning, matched to shoppers so staff and customers move alike.
        private const float AgentSpeed            = 2.2f;
        private const float AgentAngularSpeed     = 300f;
        private const float AgentAcceleration     = 6f;
        private const float AgentRadius           = 0.32f;
        private const float AgentHeight           = 1.85f;
        private const float AgentStoppingDistance = 0.6f;

        /// <summary>True while walking to or working at a shelf or pen, away from the till.</summary>
        private bool _away;

        /// <summary>The notice shown, once for the whole shop, when the last counter goes and cashiers stop serving.</summary>
        public const string NoCounterNotice =
            "Your cashiers have no counter to serve from — place a counter to open the till again.";

        /// <summary>Builds an assistant standing at the till, facing the queue.</summary>
        public static Assistant Create(Transform parent, Vector3 position, Vector3 facing,
                                       CheckoutQueue queue, ShopManager shop, int index)
        {
            var go = new GameObject($"Assistant_{index}") { layer = GameLayers.Character };
            // Built inactive so the agent does not try to bind before the NavMesh is baked.
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (facing.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            var assistant = go.AddComponent<Assistant>();
            assistant.Queue = queue;
            assistant.Shop  = shop;
            assistant._homePosition = go.transform.position;
            assistant._homeRotation = go.transform.rotation;
            assistant._agent = CreateAgent(go);
            assistant._visual = CharacterFactory.Attach(go, assistant.CurrentVelocity, variant: index + 3);

            go.SetActive(true);
            return assistant;
        }

        /// <summary>
        /// Moves this assistant's place behind the till (the counter was placed or moved). An
        /// assistant standing at the till steps straight there; one out on the floor returns to it.
        /// </summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            _homePosition = position;
            _homeRotation = rotation;
            if (!_away) transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Adds a disabled NavMeshAgent, tuned like a shopper; enabled when a trip starts.</summary>
        private static NavMeshAgent CreateAgent(GameObject go)
        {
            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed            = AgentSpeed;
            agent.angularSpeed     = AgentAngularSpeed;
            agent.acceleration     = AgentAcceleration;
            agent.radius           = AgentRadius;
            agent.height           = AgentHeight;
            agent.stoppingDistance = AgentStoppingDistance;
            agent.autoBraking      = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled          = false;
            return agent;
        }

        /// <summary>Velocity for the animator: the agent's while walking, zero while standing.</summary>
        private Vector3 CurrentVelocity() =>
            _agent != null && _agent.enabled ? _agent.velocity : Vector3.zero;

        /// <summary>
        /// Sets the skill level, clamped to the valid range. Service speed is kept separately in
        /// <see cref="ServiceSeconds"/> so a saved assistant comes back exactly as they were.
        /// </summary>
        public void SetSkill(int skill)
        {
            Skill = Mathf.Clamp(skill, StaffCandidate.MinSkill, StaffCandidate.MaxSkill);
        }

        /// <summary>
        /// Puts the assistant on a different job. Anything they were carrying out is dropped
        /// cleanly (no stock or cash is in flight between steps) and they start the new job.
        /// </summary>
        public void SetRole(StaffRole role)
        {
            if (role == Role && _routine != null) return;
            Role = role;
            RestartRoutine();
        }

        private void OnEnable() => RestartRoutine();

        private void OnDisable()
        {
            _routine = null;
        }

        /// <summary>Stops the current job loop and starts the one for <see cref="Role"/>.</summary>
        private void RestartRoutine()
        {
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _timer   = 0f;
            _routine = StartCoroutine(RoleLoop());
        }

        /// <summary>The behaviour loop for the current role.</summary>
        private IEnumerator RoleLoop() => Role switch
        {
            StaffRole.Restocker => RestockLoop(),
            StaffRole.Feeder    => FeedLoop(),
            _                   => CashierLoop(),
        };

        /// <summary>A cashier just needs to be back at the till; Update does the serving.</summary>
        private IEnumerator CashierLoop()
        {
            if (_away) yield return ReturnToTill();
        }

        private void Update()
        {
            if (Role != StaffRole.Cashier || _away) { _timer = 0f; return; }
            if (!HasCounterToServeFrom()) { _timer = 0f; return; }
            if (Queue == null || !Queue.FrontReady) { _timer = 0f; return; }

            _timer += Time.deltaTime;
            if (_timer < ServiceSeconds) return;
            _timer = 0f;

            var shopper = Queue.NextReady;
            if (shopper == null) return;

            float value = shopper.BasketValue;
            Queue.ServeFront();
            AudioManager.Instance?.PlaySfx("sale", 0.6f);
            GameManager.Instance?.Notify($"{name.Replace('_', ' ')} served {shopper.ShopperName} — €{value:N2}");
        }

        /// <summary>
        /// True when there is a counter to serve from. Without one (the last counter was packed away)
        /// the cashier stays on the payroll but waits. The shop is told once per loss of the last counter, not
        /// once per cashier (<see cref="GameManager.ClaimNoCounterNotice"/>).
        /// </summary>
        private bool HasCounterToServeFrom()
        {
            var game = GameManager.Instance;
            if (game == null || game.StaffStationValid) return true;
            if (game.ClaimNoCounterNotice()) Notify(NoCounterNotice);
            return false;
        }

        /// <summary>Name for notifications: the staff name, or the object name as a fallback.</summary>
        private string DisplayName =>
            string.IsNullOrEmpty(StaffName) ? name.Replace('_', ' ') : StaffName;

        private static void Notify(string message) => GameManager.Instance?.Notify(message);
    }
}
