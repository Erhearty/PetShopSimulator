using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// Game-feel feedback: a coin burst at the till on every sale, a dust
    /// puff and scale-pop when the player places furniture, and a dust puff and shrink when it
    /// is removed. Furniture pops and shrinks run on visual-only clones (FeedbackFX.Furniture.cs),
    /// so colliders, NavMesh bakes and the grid always see the real object at its real size.
    /// Load, room seeding and dev furnishing place through <see cref="BuildMode.Place"/> without
    /// raising <see cref="BuildMode.OnFurniturePlacedByPlayer"/>, so they get no effects.
    ///
    /// Everything is pooled — a fixed array of furniture tweens and one
    /// particle system per effect fed through <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> —
    /// so steady frames allocate nothing. No world-space text is drawn here. <see cref="GameSettings.ReduceMotion"/> turns the pops off.
    /// </summary>
    public partial class FeedbackFX : MonoBehaviour
    {
        /// <summary>Simultaneous furniture tweens; the oldest is finished early when all are busy.</summary>
        public const int PopPoolSize = 16;
        /// <summary>Seconds a placement pop takes (0.9 → 1.05 → 1).</summary>
        public const float PopDuration = 0.25f;
        /// <summary>Seconds a coin lives.</summary>
        public const float CoinLifetime = 0.9f;
        /// <summary>Seconds a dust mote lives.</summary>
        public const float DustLifetime = 0.6f;
        /// <summary>Coins thrown per sale.</summary>
        public const int CoinBurst = 14;
        /// <summary>Dust motes per placement or removal.</summary>
        public const int DustBurst = 10;

        private const float PopStart        = 0.9f;
        private const float PopOvershoot    = 1.05f;
        private const float PopPeakFraction = 0.6f;
        private const float CoinHeight      = 1.1f;
        private const float CoinSpeed       = 2.2f;
        private const float CoinSize        = 0.07f;
        private const float CoinGravity     = 1.4f;
        private const float DustSpeed       = 0.8f;
        private const float DustSize        = 0.25f;
        private const float DustGravity     = -0.05f;
        private const float BurstRadius     = 0.15f;
        private const int   MaxParticles    = 256;
        private const float CoinMetallic    = 0.5f;
        private const float CoinSmoothness  = 0.7f;

        private ParticleSystem _coins, _dust;
        private ParticleSystem.EmitParams _emit;
        private CheckoutQueue _queue;

        /// <summary>The pooled coin particle system (for tests and tuning).</summary>
        public ParticleSystem Coins { get { EnsureBuilt(); return _coins; } }
        /// <summary>The pooled dust particle system (for tests and tuning).</summary>
        public ParticleSystem Dust  { get { EnsureBuilt(); return _dust; } }

        private void Awake() => EnsureBuilt();

        private void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// Subscribes to the sale and player build events. Either argument may be null. A customer
        /// who gives up already shows "gave up!" and a reputation float (CustomerAI), so no bubble here.
        /// </summary>
        public void Attach(CheckoutQueue queue, BuildMode build)
        {
            EnsureBuilt();
            _queue = queue;
            if (queue != null) queue.OnShopperServed.AddListener(OnServed);
            if (build == null) return;
            build.OnFurniturePlacedByPlayer.AddListener(PlayPlaced);
            build.OnFurnitureDespawning.AddListener(PlayRemoved);
        }

        private void OnServed(CheckoutQueue.IShopper shopper, float value)
        {
            // The served customer already says "thanks!" through its own speech bubble
            // (CustomerAI.SetBubble), so only the till gets feedback here.
            PlaySale(TillPosition(), value);
        }

        // ── Effects ─────────────────────────────────────────────────────────────

        /// <summary>A coin burst at <paramref name="till"/>.</summary>
        public void PlaySale(Vector3 till, float value)
        {
            EnsureBuilt();
            Emit(_coins, till + Vector3.up * CoinHeight, CoinBurst);
        }

        /// <summary>Advances furniture tweens by <paramref name="dt"/> seconds (tests step this directly).</summary>
        public void Tick(float dt)
        {
            if (_coins == null) return;
            TickFurniture(dt);
        }

        // ── Scale curve ─────────────────────────────────────────────────────────

        /// <summary>0.9 → 1.05 (ease-out) over the first 60 %, then 1.05 → 1 (ease-out).</summary>
        public static float PopScale(float t)
        {
            if (t <= 0f) return PopStart;
            if (t >= 1f) return 1f;
            if (t < PopPeakFraction) return Mathf.Lerp(PopStart, PopOvershoot, EaseOut(t / PopPeakFraction));
            return Mathf.Lerp(PopOvershoot, 1f, EaseOut((t - PopPeakFraction) / (1f - PopPeakFraction)));
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        // ── Particles ───────────────────────────────────────────────────────────

        private void Emit(ParticleSystem system, Vector3 position, int count)
        {
            _emit.position = position;
            _emit.applyShapeToPosition = true;
            system.Emit(_emit, count);
        }

        private void EnsureBuilt()
        {
            if (_coins != null) return;
            _furniture = new FurnitureTween[PopPoolSize];
            _coins = CreateBurst("CoinBurst", MaterialFactory.Get("fx_coin", MaterialFactory.Palette.Sunflower,
                                 CoinMetallic, CoinSmoothness), CoinLifetime, CoinSpeed, CoinSize, CoinGravity);
            _dust  = CreateBurst("DustPuff", MaterialFactory.Get("fx_dust", MaterialFactory.Palette.CreamWall),
                                 DustLifetime, DustSpeed, DustSize, DustGravity);
        }

        private ParticleSystem CreateBurst(string name, Material material, float lifetime, float speed,
                                           float size, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;   // stays alive with emission off, so Emit()ted particles simulate
            main.playOnAwake = false;
            main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = size;
            main.gravityModifier = gravity; main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = BurstRadius;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            system.Play();
            return system;
        }

        // ── Positions ───────────────────────────────────────────────────────────

        private Vector3 TillPosition()
        {
            if (_queue == null) return transform.position;
            return _queue.TillPoint != null ? _queue.TillPoint.position : _queue.StandingPosition(0);
        }
    }
}
