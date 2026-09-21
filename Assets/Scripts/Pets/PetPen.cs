using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Pets
{
    /// <summary>
    /// An in-store enclosure holding live pets. Each resident gets a procedural 3D body
    /// that wanders gently inside the fence.
    /// </summary>
    public class PetPen : MonoBehaviour
    {
        [Header("Config")]
        public int         Capacity   = 4;
        public Pet.Species PenSpecies = Pet.Species.Rabbit;
        public float       PenSize    = 2.5f;

        [Header("Upkeep")]
        [Tooltip("Feed and bedding drain over a trading day; both are restored by servicing the pen.")]
        [Range(0f, 1f)] public float FoodLevel   = 1f;
        [Range(0f, 1f)] public float Cleanliness = 1f;

        /// <summary>What it costs to feed and muck out this pen, scaled by how many live here.</summary>
        public float ServiceCost => 4f + Count * 3.5f;

        public bool NeedsFeeding => FoodLevel   < 0.45f;
        public bool NeedsCleaning => Cleanliness < 0.45f;
        public bool NeedsService => Count > 0 && (NeedsFeeding || NeedsCleaning);

        public UnityEvent<PetPen, Pet> OnPetAdded   = new();
        public UnityEvent<PetPen, Pet> OnPetRemoved = new();
        public UnityEvent<PetPen>      OnDayPassed  = new();
        public UnityEvent<PetPen>      OnServiced   = new();

        private readonly List<Pet>        _residents = new();
        private readonly List<PetVisual>  _visuals   = new();
        private Transform  _visualRoot;
        private Transform  _upkeepRoot;
        private WorldLabel _signLabel;
        private Transform  _foodBar;
        private Transform  _cleanBar;
        private Renderer   _foodBarRenderer;
        private Renderer   _cleanBarRenderer;

        // ── Queries ─────────────────────────────────────────────────────────────

        public IReadOnlyList<Pet> Residents => _residents;
        public int  Count      => _residents.Count;
        public bool HasSpace   => _residents.Count < Capacity;
        public bool HasAdults  => _residents.Exists(p => p.IsAdult);
        public int  AdultCount => _residents.FindAll(p => p.IsAdult).Count;

        // ── Population ──────────────────────────────────────────────────────────

        public bool AddPet(Pet pet)
        {
            if (pet == null || !HasSpace) return false;
            if (pet.species != PenSpecies)
            {
                Debug.LogWarning($"[PetPen] Wrong species: pen is {PenSpecies}, got {pet.species}");
                return false;
            }
            _residents.Add(pet);
            RefreshVisuals();
            OnPetAdded.Invoke(this, pet);
            return true;
        }

        /// <summary>Remove and return an adult for sale. Null when there is none.</summary>
        public Pet TakeAdult()
        {
            var adult = _residents.Find(p => p.IsAdult);
            if (adult == null) return null;
            _residents.Remove(adult);
            RefreshVisuals();
            OnPetRemoved.Invoke(this, adult);
            return adult;
        }

        public bool RemovePet(Pet pet)
        {
            if (!_residents.Remove(pet)) return false;
            RefreshVisuals();
            OnPetRemoved.Invoke(this, pet);
            return true;
        }

        /// <summary>
        /// Ages every resident by one day, using however much feed and bedding was left.
        /// Called by BreedingSystem at day end.
        /// </summary>
        public void AdvanceDay()
        {
            bool fed   = FoodLevel   > 0.2f;
            bool clean = Cleanliness > 0.2f;

            foreach (var pet in _residents) pet.AdvanceDay(fed, clean);

            // More animals eat more and make more mess.
            float load = 0.18f + Count * 0.12f;
            FoodLevel   = Mathf.Clamp01(FoodLevel   - load);
            Cleanliness = Mathf.Clamp01(Cleanliness - load * 0.85f);

            RefreshVisuals();
            OnDayPassed.Invoke(this);
        }

        /// <summary>Fill the feeder and muck out. Returns false if nothing needed doing.</summary>
        public bool Service()
        {
            if (FoodLevel > 0.95f && Cleanliness > 0.95f) return false;

            FoodLevel   = 1f;
            Cleanliness = 1f;
            foreach (var pet in _residents)
            {
                pet.hunger    = Mathf.Max(0f, pet.hunger - 0.5f);
                pet.happiness = Mathf.Clamp01(pet.happiness + 0.12f);
            }
            RefreshVisuals();
            OnServiced.Invoke(this);
            return true;
        }

        /// <summary>Average health of the residents, 1 when empty.</summary>
        public float AverageHealth
        {
            get
            {
                if (_residents.Count == 0) return 1f;
                float total = 0f;
                foreach (var pet in _residents) total += pet.health;
                return total / _residents.Count;
            }
        }

        public string Describe()
        {
            if (_residents.Count == 0) return $"{PenSpecies} pen — empty";

            var sb = new System.Text.StringBuilder($"{PenSpecies} pen  ({_residents.Count}/{Capacity})\n");
            sb.AppendLine($"  Feed {FoodLevel * 100f:0}%   ·   Bedding {Cleanliness * 100f:0}%" +
                          (NeedsService ? $"   ·   servicing costs €{ServiceCost:N2}" : ""));
            sb.AppendLine();
            foreach (var p in _residents)
                sb.AppendLine($"  {p.DisplayName(),-34}{p.Condition,-11}€{p.SellPrice():0.00}");
            if (AdultCount >= 2 && HasSpace)      sb.AppendLine("  Two adults — they may breed tonight.");
            else if (AdultCount < 2 && HasSpace)   sb.AppendLine("  Needs two adults to breed.");
            if (!HasSpace)                         sb.AppendLine("  Pen is full.");
            return sb.ToString().TrimEnd();
        }

        // ── Gate sign ───────────────────────────────────────────────────────────

        private void Start()
        {
            BuildPenSign();
            RefreshLabel();
        }

        /// <summary>
        /// A sign on the gate plus two upkeep bars. The whole yard has to be readable from
        /// the path without walking up to every pen and pressing E.
        /// </summary>
        private void BuildPenSign()
        {
            // Eye level, not scaled off the pen: any higher and the sign leaves the frame
            // when you stand next to the gate (and hides above the pergola beams).
            const float y = 1.32f;
            _signLabel = WorldLabel.Create(transform, new Vector3(0f, y, PenSize * 0.5f),
                                           "", 0.09f, UIFactory.Ink, width: 1.6f);

            var barRoot = new GameObject("UpkeepBars").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, y - 0.26f, PenSize * 0.5f - 0.005f);

            _foodBar  = MakeBar(barRoot, -0.07f, out _foodBarRenderer);
            _cleanBar = MakeBar(barRoot, -0.16f, out _cleanBarRenderer);
        }

        /// <summary>One track plus a fill that scales from its left edge.</summary>
        private Transform MakeBar(Transform parent, float localY, out Renderer fillRenderer)
        {
            const float width = 0.62f, height = 0.055f;

            var track = MeshBuilder.CreateBox(width, height, 0.015f,
                MaterialFactory.Get("penBar_track", new Color(0.1f, 0.11f, 0.14f, 0.9f)), "BarTrack");
            track.transform.SetParent(parent, false);
            track.transform.localPosition = new Vector3(0f, localY, 0f);
            Destroy(track.GetComponent<Collider>());

            var fill = MeshBuilder.CreateBox(width, height * 0.72f, 0.015f,
                MaterialFactory.Get("penBar_fill", Color.white), "BarFill");
            fill.transform.SetParent(parent, false);
            // Pivot on the left edge so scaling drains the bar rightwards.
            fill.transform.localPosition = new Vector3(-width * 0.5f, localY, -0.008f);
            Destroy(fill.GetComponent<Collider>());

            fillRenderer = fill.GetComponent<Renderer>();
            return fill.transform;
        }

        /// <summary>Scales and tints one bar without duplicating its material.</summary>
        private void SetBar(Transform bar, Renderer renderer, float value01)
        {
            if (bar == null || renderer == null) return;

            const float width = 0.62f;
            float v = Mathf.Clamp01(value01);

            Vector3 scale = bar.localScale;
            bar.localScale    = new Vector3(width * v, scale.y, scale.z);
            bar.localPosition = new Vector3(-width * 0.5f + width * v * 0.5f,
                                            bar.localPosition.y, bar.localPosition.z);

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", Color.Lerp(UIFactory.Bad, UIFactory.Good, v));
            renderer.SetPropertyBlock(block);
        }

        private static Color RarityColour(Pet.Rarity rarity) => rarity switch
        {
            Pet.Rarity.Uncommon  => new Color(0.55f, 0.85f, 0.55f),
            Pet.Rarity.Rare      => new Color(0.55f, 0.74f, 0.98f),
            Pet.Rarity.Legendary => new Color(0.98f, 0.82f, 0.38f),
            _                    => UIFactory.Ink
        };

        /// <summary>Species, asking price and condition — refreshed whenever the pen changes.</summary>
        public void RefreshLabel()
        {
            if (_signLabel == null) return;

            SetBar(_foodBar,  _foodBarRenderer,  FoodLevel);
            SetBar(_cleanBar, _cleanBarRenderer, Cleanliness);

            if (_residents.Count == 0)
            {
                _signLabel.SetText($"{PenSpecies} pen\n<size=75%>empty</size>");
                _signLabel.SetColour(UIFactory.InkMuted);
                return;
            }

            Pet best = _residents[0];
            foreach (var p in _residents)
                if (p.SellPrice() > best.SellPrice()) best = p;

            var shop  = GameManager.Instance != null ? GameManager.Instance.Shop : null;
            float ask = shop != null ? shop.PriceOf(best.SellPrice()) : best.SellPrice();

            _signLabel.SetText($"{PenSpecies}   € {ask:0.00}\n" +
                               $"<size=75%>{_residents.Count} / {Capacity}  ·  {best.rarity}</size>");
            _signLabel.SetColour(RarityColour(best.rarity));
        }

        // ── Visuals ─────────────────────────────────────────────────────────────

        private void RefreshVisuals()
        {
            if (_visualRoot == null)
            {
                _visualRoot = new GameObject("Pets").transform;
                _visualRoot.SetParent(transform, false);
            }

            foreach (var v in _visuals)
                if (v != null) Destroy(v.gameObject);
            _visuals.Clear();

            RefreshUpkeepVisuals();
            RefreshLabel();

            float inner = PenSize * 0.5f - 0.35f;
            for (int i = 0; i < _residents.Count; i++)
            {
                Pet pet    = _residents[i];
                // Scaled against the pen so animals read at a glance rather than looking lost.
                float maturity = pet.IsAdult ? 1f : pet.growthStage == Pet.GrowthStage.Juvenile ? 0.78f : 0.6f;
                float size     = maturity * Mathf.Clamp(PenSize / 3.4f, 0.8f, 1.6f);

                GameObject go = SpawnBody(pet, size, out Animator animator);
                MeshBuilder.StripColliders(go);
                MeshBuilder.SetLayerRecursive(go, gameObject.layer);

                var wander = go.AddComponent<PetVisual>();
                wander.Init(SlotPos(i, inner), inner, 0.25f + pet.energyLevel * 0.6f);
                _visuals.Add(wander);

                // Animated species get their walk cycle driven from the wander speed.
                if (animator != null)
                {
                    var visual = go.AddComponent<CharacterVisual>();
                    visual.WalkReferenceSpeed = 0.6f;
                    visual.RunThreshold       = 1.6f;
                    visual.IdleThreshold      = 0.05f;
                    visual.Init(animator, () => wander.Velocity);
                }
            }
        }

        /// <summary>
        /// A modelled animal if the pack has one for this species, otherwise the procedural
        /// stand-in. Returns the animator when the model brought one.
        /// </summary>
        private GameObject SpawnBody(Pet pet, float size, out Animator animator)
        {
            animator = null;

            // Animal packs name their prefabs inconsistently (Fox, Kitty, dog_01 ...), so try
            // the species name and a few known aliases before falling back to the blocky
            // stand-in. Missing packs must never break a pen.
            string model = ModelLibrary.FirstAvailable(AnimalModelPaths(pet.species));
            if (model != null)
            {
                float height = Mathf.Clamp(0.85f * size, 0.45f, 1.5f);
                var modelled = ModelLibrary.Spawn(model, _visualRoot, _visualRoot.position,
                                                  0f, ModelLibrary.Fit.Height, height);
                if (modelled != null)
                {
                    animator = modelled.GetComponentInChildren<Animator>();
                    if (animator != null) animator.applyRootMotion = false;
                    return modelled;
                }
            }

            var go = MeshBuilder.CreatePet(pet.species, pet.coat, size);
            go.transform.SetParent(_visualRoot, false);
            return go;
        }

        /// <summary>
        /// Straw when the pen is clean, scattered muck when it is not — the state has to be
        /// readable from across the yard, not only in the info panel.
        /// </summary>
        private void RefreshUpkeepVisuals()
        {
            if (_upkeepRoot == null)
            {
                _upkeepRoot = new GameObject("Upkeep").transform;
                _upkeepRoot.SetParent(transform, false);
            }
            for (int i = _upkeepRoot.childCount - 1; i >= 0; i--)
                Destroy(_upkeepRoot.GetChild(i).gameObject);

            if (Cleanliness < 0.5f)
            {
                var muck = MaterialFactory.Get("pen_muck", new Color(0.36f, 0.29f, 0.18f), 0f, 0.1f);
                int blobs = Mathf.RoundToInt(Mathf.Lerp(6f, 1f, Cleanliness / 0.5f));
                for (int i = 0; i < blobs; i++)
                {
                    var blob = MeshBuilder.CreateBox(Random.Range(0.16f, 0.3f), 0.05f, Random.Range(0.16f, 0.3f),
                                                     muck, "Muck");
                    blob.transform.SetParent(_upkeepRoot, false);
                    blob.transform.localPosition = new Vector3(
                        Random.Range(-PenSize * 0.35f, PenSize * 0.35f), 0.03f,
                        Random.Range(-PenSize * 0.35f, PenSize * 0.35f));
                    blob.transform.localEulerAngles = new Vector3(0f, Random.Range(0f, 360f), 0f);
                    Destroy(blob.GetComponent<Collider>());
                }
            }

            // Feed heap in the corner, shrinking as it is eaten.
            if (FoodLevel > 0.05f)
            {
                float size = Mathf.Lerp(0.08f, 0.3f, FoodLevel);
                var feed = MeshBuilder.CreateBox(size, size * 0.45f, size,
                    MaterialFactory.Get("pen_feed", new Color(0.82f, 0.68f, 0.34f), 0f, 0.15f), "Feed");
                feed.transform.SetParent(_upkeepRoot, false);
                feed.transform.localPosition = new Vector3(PenSize * 0.32f, 0.04f, -PenSize * 0.20f);
                Destroy(feed.GetComponent<Collider>());
            }
        }

        /// <summary>Candidate Resources paths for a species' model, best first.</summary>
        private static string[] AnimalModelPaths(Pet.Species species)
        {
            string[] aliases = species switch
            {
                Pet.Species.Cat     => new[] { "Cat", "Kitty" },
                Pet.Species.Dog     => new[] { "Dog" },
                Pet.Species.Fox     => new[] { "Fox" },
                Pet.Species.Chicken => new[] { "Chicken", "Hen" },
                // "Pinguin" is the ithappy pack's spelling, not a typo here.
                Pet.Species.Penguin => new[] { "Penguin", "Pinguin" },
                Pet.Species.Deer    => new[] { "Deer" },
                Pet.Species.Horse   => new[] { "Horse" },
                Pet.Species.Tiger   => new[] { "Tiger" },
                _                   => new[] { species.ToString() },
            };

            // Packs commonly suffix a variant number, so try the bare name and _001/_01 too.
            var paths = new List<string>();
            foreach (string alias in aliases)
            foreach (string suffix in new[] { "", "_001", "_01", "_1" })
            {
                paths.Add(ModelLibrary.Animals + alias + suffix);
                paths.Add(ModelLibrary.Animals + alias.ToLowerInvariant() + suffix);
            }
            return paths.ToArray();
        }

        private static Vector3 SlotPos(int i, float inner)
        {
            const int cols = 2;
            float step = inner;
            return new Vector3(((i % cols) - 0.5f) * step, 0.03f, ((i / cols) - 0.5f) * step);
        }
    }

    /// <summary>Gentle idle wander + hop, so pens do not look like static props.</summary>
    public class PetVisual : MonoBehaviour
    {
        private Vector3 _home;
        private Vector3 _target;
        private float   _radius;
        private float   _speed;
        private float   _hopPhase;
        private float   _restTimer;
        private Vector3 _lastPosition;

        /// <summary>Local-space velocity, so an animator can be driven from the wander.</summary>
        public Vector3 Velocity { get; private set; }

        public void Init(Vector3 home, float radius, float speed)
        {
            _home   = home;
            _radius = Mathf.Max(0.15f, radius * 0.5f);
            _speed  = speed;
            transform.localPosition = home;
            _lastPosition = home;
            _target   = home;
            _hopPhase = Random.value * 10f;
        }

        private void Update()
        {
            _hopPhase += Time.deltaTime * (2f + _speed);

            if (_restTimer > 0f)
            {
                _restTimer -= Time.deltaTime;
            }
            else
            {
                Vector3 flat = transform.localPosition; flat.y = 0f;
                Vector3 goal = _target;                 goal.y = 0f;

                if (Vector3.Distance(flat, goal) < 0.05f)
                {
                    var offset = Random.insideUnitCircle * _radius;
                    _target    = _home + new Vector3(offset.x, 0f, offset.y);
                    _restTimer = Random.Range(0.6f, 2.5f);
                }
                else
                {
                    Vector3 dir = (goal - flat).normalized;
                    transform.localPosition += dir * (_speed * Time.deltaTime);
                    transform.localRotation  = Quaternion.Slerp(
                        transform.localRotation, Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
                }
            }

            var p = transform.localPosition;
            // Modelled animals walk; the blocky stand-ins hop, which reads better for them.
            p.y = _home.y + (GetComponent<PetShop.Core.CharacterVisual>() != null
                ? 0f
                : Mathf.Abs(Mathf.Sin(_hopPhase)) * 0.05f);
            transform.localPosition = p;

            Velocity = Time.deltaTime > 0f ? (p - _lastPosition) / Time.deltaTime : Vector3.zero;
            _lastPosition = p;
        }
    }
}
