using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// Game-feel feedback: a floating "+€X" and a coin burst at the till on every sale, a dust
    /// puff and scale-pop when the player places furniture, and a dust puff and shrink when it
    /// is removed. Furniture pops and shrinks run on visual-only clones (FeedbackFX.Furniture.cs),
    /// so colliders, NavMesh bakes and the grid always see the real object at its real size.
    /// Load, room seeding and dev furnishing place through <see cref="BuildMode.Place"/> without
    /// raising <see cref="BuildMode.OnFurniturePlacedByPlayer"/>, so they get no effects.
    ///
    /// Everything is pooled — a fixed ring of world labels, a fixed array of pops and one
    /// particle system per effect fed through <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> —
    /// so steady frames allocate nothing. <see cref="GameSettings.ReduceMotion"/> turns the pops off.
    /// </summary>
    public partial class FeedbackFX : MonoBehaviour
    {
        /// <summary>Floating labels kept in the pool; the oldest is recycled when all are busy.</summary>
        public const int TextPoolSize = 8;
        /// <summary>Simultaneous scale pops; the oldest is finished early when all are busy.</summary>
        public const int PopPoolSize = 16;
        /// <summary>Seconds a floating label lives.</summary>
        public const float TextLifetime = 1.4f;
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

        /// <summary>Bubble shown over a served customer.</summary>
        public static string HappyText => Loc.T("hud.thanks");

        private const float PopStart        = 0.9f;
        private const float PopOvershoot    = 1.05f;
        private const float PopPeakFraction = 0.6f;
        private const float TextRiseSpeed   = 0.8f;
        private const float TextHoldFraction = 0.33f;
        private const float SaleTextSize    = 0.28f;
        private const float BubbleTextSize  = 0.2f;
        private const float TextWidth       = 3.2f;
        private const float SaleTextHeight  = 1.6f;
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

        private struct TextSlot { public WorldLabel Label; public float Age; public Color Colour; public bool Active; }
        private struct Pop { public Transform Target; public Vector3 BaseScale; public float Age; }

        private TextSlot[] _texts;
        private Pop[]      _pops;
        private int        _popCount;
        private ParticleSystem _coins, _dust;
        private ParticleSystem.EmitParams _emit;
        private CheckoutQueue _queue;

        /// <summary>Floating labels currently showing.</summary>
        public int ActiveTextCount { get; private set; }
        /// <summary>Label scale pops currently running (furniture uses <see cref="ActiveFurnitureTweenCount"/>).</summary>
        public int ActivePopCount => _popCount;
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

        /// <summary>A floating "+€X" and a coin burst at <paramref name="till"/>.</summary>
        public void PlaySale(Vector3 till, float value)
        {
            EnsureBuilt();
            ShowText(till + Vector3.up * SaleTextHeight, $"+€{value:N2}", UIFactory.Accent, SaleTextSize, false);
            Emit(_coins, till + Vector3.up * CoinHeight, CoinBurst);
        }

        /// <summary>A happy bubble that pops in over a customer's head.</summary>
        public void PlayCustomerMood(Vector3 head)
        {
            EnsureBuilt();
            ShowText(head, HappyText, UIFactory.Positive, BubbleTextSize, true);
        }

        /// <summary>Advances labels, pops and furniture tweens by <paramref name="dt"/> seconds (tests step this directly).</summary>
        public void Tick(float dt)
        {
            if (_texts == null) return;
            TickTexts(dt);
            TickPops(dt);
            TickFurniture(dt);
        }

        // ── Floating text ───────────────────────────────────────────────────────

        private void ShowText(Vector3 position, string text, Color colour, float size, bool pop)
        {
            int i = FreeTextSlot();
            ref var slot = ref _texts[i];
            if (!slot.Active) ActiveTextCount++;
            slot.Active = true;
            slot.Age    = 0f;
            slot.Colour = colour;
            slot.Label.transform.position   = position;
            slot.Label.transform.localScale = Vector3.one * (size / SaleTextSize);
            slot.Label.gameObject.SetActive(true);
            slot.Label.SetText(text);
            slot.Label.SetColour(colour);
            if (pop && !GameSettings.ReduceMotion) StartPop(slot.Label.transform);
        }

        private int FreeTextSlot()
        {
            int oldest = 0;
            for (int i = 0; i < _texts.Length; i++)
            {
                if (!_texts[i].Active) return i;
                if (_texts[i].Age > _texts[oldest].Age) oldest = i;
            }
            return oldest;
        }

        private void TickTexts(float dt)
        {
            for (int i = 0; i < _texts.Length; i++)
            {
                ref var slot = ref _texts[i];
                if (!slot.Active) continue;
                slot.Age += dt;
                if (slot.Age >= TextLifetime) { ReleaseText(ref slot); continue; }
                slot.Label.transform.position += Vector3.up * (TextRiseSpeed * dt);
                float t = slot.Age / TextLifetime;
                float alpha = t < TextHoldFraction ? 1f : 1f - (t - TextHoldFraction) / (1f - TextHoldFraction);
                slot.Label.SetColour(UIFactory.WithAlpha(slot.Colour, alpha));
            }
        }

        private void ReleaseText(ref TextSlot slot)
        {
            slot.Active = false;
            ForgetPop(slot.Label.transform);
            slot.Label.gameObject.SetActive(false);
            ActiveTextCount--;
        }

        // ── Scale pops ──────────────────────────────────────────────────────────

        private void StartPop(Transform target)
        {
            for (int i = 0; i < _popCount; i++)
                if (_pops[i].Target == target) { _pops[i].Age = 0f; return; }
            if (_popCount == _pops.Length) FinishPop(0);
            _pops[_popCount++] = new Pop { Target = target, BaseScale = target.localScale, Age = 0f };
            target.localScale = _pops[_popCount - 1].BaseScale * PopStart;
        }

        private void TickPops(float dt)
        {
            for (int i = _popCount - 1; i >= 0; i--)
            {
                ref var pop = ref _pops[i];
                if (pop.Target == null) { RemovePop(i); continue; }
                pop.Age += dt;
                float t = pop.Age / PopDuration;
                if (t >= 1f) { FinishPop(i); continue; }
                pop.Target.localScale = pop.BaseScale * PopScale(t);
            }
        }

        /// <summary>0.9 → 1.05 (ease-out) over the first 60 %, then 1.05 → 1 (ease-out).</summary>
        public static float PopScale(float t)
        {
            if (t <= 0f) return PopStart;
            if (t >= 1f) return 1f;
            if (t < PopPeakFraction) return Mathf.Lerp(PopStart, PopOvershoot, EaseOut(t / PopPeakFraction));
            return Mathf.Lerp(PopOvershoot, 1f, EaseOut((t - PopPeakFraction) / (1f - PopPeakFraction)));
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        private void FinishPop(int i)
        {
            if (_pops[i].Target != null) _pops[i].Target.localScale = _pops[i].BaseScale;
            RemovePop(i);
        }

        private void ForgetPop(Transform target)
        {
            for (int i = _popCount - 1; i >= 0; i--)
                if (_pops[i].Target == target) FinishPop(i);
        }

        private void RemovePop(int i)
        {
            _pops[i] = _pops[--_popCount];
            _pops[_popCount] = default;
        }

        // ── Particles ───────────────────────────────────────────────────────────

        private void Emit(ParticleSystem system, Vector3 position, int count)
        {
            _emit.position = position;
            _emit.applyShapeToPosition = true;
            system.Emit(_emit, count);
        }

        private void EnsureBuilt()
        {
            if (_texts != null) return;
            _texts = new TextSlot[TextPoolSize];
            for (int i = 0; i < _texts.Length; i++)
            {
                var label = WorldLabel.Create(transform, Vector3.zero, string.Empty, SaleTextSize, UIFactory.Accent,
                                              width: TextWidth, plate: false);
                label.MaxVisibleDistance = 0f;
                label.gameObject.SetActive(false);
                _texts[i].Label = label;
            }
            _pops      = new Pop[PopPoolSize];
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
