using System;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Builds the shopkeeper and the customers from the animated character pack, falling
    /// back to the procedural humanoid when the pack is absent.
    /// </summary>
    public static class CharacterFactory
    {
        public const int PackSize = 100;   // body_root_1 .. body_root_100 in the fallback pack

        /// <summary>
        /// The City People cast, preferred over the generic bodies because the models are
        /// better and more varied. Paths are relative to Resources.
        /// </summary>
        /// <summary>
        /// The City People cast, preferred over the generic bodies because the models are
        /// better and more varied.
        ///
        /// "city/casual_Female_G" is deliberately absent: that variant is in swimwear, which
        /// looks like a bug when it walks into a pet shop in the middle of a city street. The
        /// downtown set is the everyday-clothes equivalent.
        /// </summary>
        public static readonly string[] CityPeople =
        {
            "Packs/CityPeople/downtown/casual_Female_K",
            "Packs/CityPeople/downtown/casual_Male_K",
            "Packs/CityPeople/city/casual_Male_G",
            "Packs/CityPeople/elder/elder_Female_A",
            "Packs/CityPeople/little_kids/little_boy_B",
            "Packs/CityPeople/professions/Doctor_Male_B",
            "Packs/CityPeople/professions/police_Female_A",
            "Packs/CityPeople/disabilities/prostheticLeg_girl",
            "Packs/CityPeople/worker_Male_constructor_B",
        };

        private static bool CityPeopleInstalled => ModelLibrary.Has(CityPeople[0]);

        /// <summary>Roughly how tall a person stands, in metres.</summary>
        public const float AdultHeight = 1.78f;

        /// <summary>
        /// Attaches a character body under <paramref name="root"/> and returns the component
        /// driving its animator, or null if the procedural fallback was used.
        /// </summary>
        public static CharacterVisual Attach(GameObject root, Func<Vector3> velocitySource,
                                             int variant = -1, float height = AdultHeight)
        {
            string path;
            if (CityPeopleInstalled)
            {
                int index = variant < 0 ? UnityEngine.Random.Range(0, CityPeople.Length)
                                        : Mathf.Abs(variant) % CityPeople.Length;
                path = CityPeople[index];

                // The kid should not be adult height.
                if (path.Contains("little_kids")) height *= 0.72f;
                else if (path.Contains("elder"))  height *= 0.94f;
            }
            else
            {
                if (variant < 0) variant = UnityEngine.Random.Range(1, PackSize + 1);
                path = ModelLibrary.People + "body_root_" + variant;
            }

            var body = ModelLibrary.Spawn(path, root.transform, root.transform.position,
                                          root.transform.eulerAngles.y, ModelLibrary.Fit.Height, height);

            if (body == null)
            {
                BuildProcedural(root, height);
                return null;
            }

            // The holder carries position/yaw; keep it neutral so the character root drives facing.
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.name = "Body";

            MeshBuilder.StripColliders(body);
            MeshBuilder.SetLayerRecursive(body, GameLayers.Character);

            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null) return null;

            animator.applyRootMotion = false;   // movement comes from the agent/controller
            animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;

            var visual = root.AddComponent<CharacterVisual>();
            visual.Init(animator, velocitySource);
            return visual;
        }

        /// <summary>The original blocky stand-in, used when the character pack is not installed.</summary>
        private static void BuildProcedural(GameObject root, float height)
        {
            var body = MeshBuilder.CreateHumanoid(RandomSkin(), RandomShirt(), RandomTrousers(), "Body");
            body.transform.SetParent(root.transform, false);

            // CreateHumanoid is authored around 1.89 m tall.
            float scale = height / 1.89f;
            body.transform.localScale = Vector3.one * scale;

            MeshBuilder.StripColliders(body);
            MeshBuilder.SetLayerRecursive(body, GameLayers.Character);
        }

        public static Color RandomSkin()
        {
            Color[] p = { new(1f, 0.87f, 0.73f), new(0.94f, 0.76f, 0.59f),
                          new(0.80f, 0.60f, 0.40f), new(0.60f, 0.40f, 0.24f), new(0.40f, 0.24f, 0.13f) };
            return p[UnityEngine.Random.Range(0, p.Length)];
        }

        public static Color RandomShirt()
        {
            Color[] p = { new(0.85f, 0.28f, 0.28f), new(0.25f, 0.45f, 0.80f), new(0.30f, 0.65f, 0.38f),
                          new(0.90f, 0.75f, 0.25f), new(0.70f, 0.35f, 0.75f), new(0.25f, 0.70f, 0.72f),
                          new(0.92f, 0.92f, 0.90f), new(0.95f, 0.55f, 0.20f) };
            return p[UnityEngine.Random.Range(0, p.Length)];
        }

        public static Color RandomTrousers()
        {
            Color[] p = { new(0.22f, 0.25f, 0.32f), new(0.35f, 0.32f, 0.28f),
                          new(0.18f, 0.20f, 0.24f), new(0.45f, 0.42f, 0.38f) };
            return p[UnityEngine.Random.Range(0, p.Length)];
        }
    }

    /// <summary>
    /// Drives the character pack's animator from whatever is actually moving the object —
    /// a NavMeshAgent for customers, a CharacterController for the player.
    ///
    /// The pack's controller exposes: move (0 idle / 1 walk / 2 run) and speed (playback
    /// multiplier), so the walk cycle can be matched to real travel speed instead of
    /// sliding.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        private static readonly int MoveId  = Animator.StringToHash("move");
        private static readonly int SpeedId = Animator.StringToHash("speed");

        /// <summary>
        /// State names to fall back on for controllers that expose no parameters. City
        /// People and the animal pack are built that way: their controllers are a bare list
        /// of states, so they have to be driven with CrossFade by name rather than by
        /// setting an int. The first name that the controller actually contains wins.
        /// </summary>
        private static readonly string[] IdleStates =
        {
            "idle_m_2_220f", "idle_f_1_150f", "idle_f_2_190f", "idle_selfcheck_1_300f",
            "root_Idle", "Idle", "idle",
        };
        private static readonly string[] WalkStates =
        {
            "locom_m_basicWalk_30f", "locom_f_basicWalk_30f", "root_Walk", "Walk", "walk",
        };
        private static readonly string[] RunStates =
        {
            "locom_m_jogging_30f", "locom_f_jogging_30f", "Run", "run",
        };

        [Tooltip("Travel speed the walk animation was authored at, in m/s.")]
        public float WalkReferenceSpeed = 1.35f;
        public float IdleThreshold      = 0.12f;
        public float RunThreshold       = 3.2f;

        private Animator      _animator;
        private Func<Vector3> _velocity;
        private int           _lastMove = -1;

        /// <summary>Locomotion band last sent to the animator: 0 idle, 1 walk, 2 run, -1 before the first update.</summary>
        public int CurrentMove => _lastMove;

        private bool _usesParameters;
        private int  _idleHash, _walkHash, _runHash;

        public void Init(Animator animator, Func<Vector3> velocitySource)
        {
            _animator = animator;
            _velocity = velocitySource;
            if (_animator == null) return;

            _usesParameters = HasParameter("move");
            if (_usesParameters) return;

            _idleHash = FirstExistingState(IdleStates);
            _walkHash = FirstExistingState(WalkStates);
            _runHash  = FirstExistingState(RunStates);

            if (_walkHash == 0 && _idleHash == 0)
                Debug.LogWarning($"[CharacterVisual] {name}: controller has neither a 'move' " +
                                 "parameter nor any recognised state — it will stand still.");
        }

        private bool HasParameter(string parameterName)
        {
            foreach (var p in _animator.parameters)
                if (p.name == parameterName) return true;
            return false;
        }

        private int FirstExistingState(string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                int hash = Animator.StringToHash(candidate);
                if (_animator.HasState(0, hash)) return hash;
            }
            return 0;
        }

        /// <summary>Plays a one-shot animation, where the controller supports it.</summary>
        public void PlayTrigger(string trigger)
        {
            if (_animator == null || string.IsNullOrEmpty(trigger)) return;
            if (_usesParameters && HasParameter(trigger)) _animator.SetTrigger(trigger);
        }

        private void Update()
        {
            if (_animator == null || _velocity == null) return;

            Vector3 v = _velocity();
            v.y = 0f;
            float speed = v.magnitude;

            int move = speed < IdleThreshold ? 0 : speed < RunThreshold ? 1 : 2;
            float playback = move == 0 ? 1f : Mathf.Clamp(speed / WalkReferenceSpeed, 0.55f, 1.9f);

            if (_usesParameters)
            {
                if (move != _lastMove) _animator.SetInteger(MoveId, move);
                _animator.SetFloat(SpeedId, playback);
            }
            else if (move != _lastMove)
            {
                int hash = move switch
                {
                    0 => _idleHash != 0 ? _idleHash : _walkHash,
                    2 => _runHash  != 0 ? _runHash  : _walkHash,
                    _ => _walkHash != 0 ? _walkHash : _idleHash,
                };
                if (hash != 0) _animator.CrossFadeInFixedTime(hash, 0.18f, 0);
            }

            if (!_usesParameters) _animator.speed = move == 0 ? 1f : playback;
            _lastMove = move;
        }
    }
}
