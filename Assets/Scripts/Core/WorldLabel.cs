using UnityEngine;
using TMPro;

namespace PetShop.Core
{
    /// <summary>
    /// A small billboarded text label drawn in the world — price tags, stock counts, the
    /// "+€12.40" that pops when a sale lands.
    ///
    /// Deliberately TextMeshPro in world space rather than a screen-space canvas per object:
    /// these have to sit on shelves and over animals at the right size and occlude correctly
    /// behind walls, which a screen-space overlay cannot do.
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        [Tooltip("Hide the label past this distance; 0 keeps it always visible.")]
        public float MaxVisibleDistance = 26f;

        [Tooltip("Hide the label when the viewer is practically inside it.")]
        public float MinVisibleDistance = 0.8f;

        private TextMeshPro _text;
        private Transform   _plate;
        private Renderer    _plateRenderer;
        private float       _size = 0.1f;
        private float       _maxWidth = 2.4f;

        public TextMeshPro Text => _text;

        public static WorldLabel Create(Transform parent, Vector3 localPosition, string text,
                                        float size = 0.16f, Color? colour = null,
                                        float width = 2.4f, bool plate = true)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var label = go.AddComponent<WorldLabel>();
            label._size     = size;
            label._maxWidth = width;

            if (plate)
            {
                // A unit cube; AutoFit scales it to whatever the text actually measures.
                var back = MeshBuilder.CreateBox(1f, 1f, 0.02f,
                    MaterialFactory.Get("label_plate", new Color(0.07f, 0.09f, 0.13f, 0.82f)), "Plate");
                back.transform.SetParent(go.transform, false);
                back.transform.localPosition = new Vector3(0f, 0f, 0.012f);
                Object.Destroy(back.GetComponent<Collider>());
                label._plate         = back.transform;
                label._plateRenderer = back.GetComponent<Renderer>();
            }

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = Vector3.zero;

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text             = text;
            // TextMeshPro world units run at roughly fontSize/10 metres per line, so `size`
            // is the cap height in metres — the number a caller can actually reason about.
            tmp.fontSize         = size * 10f;
            tmp.color            = colour ?? Color.white;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(width, size * 6f);

            label._text = tmp;
            label.AutoFit();
            return label;
        }

        public void SetText(string value)
        {
            if (_text == null) return;
            _text.text = value;
            AutoFit();
        }

        /// <summary>
        /// Shrinks the plate onto the text. Without this every tag is the same slab width
        /// regardless of content, and short labels float on an oversized black bar.
        /// </summary>
        private void AutoFit()
        {
            if (_text == null) return;

            Vector2 preferred = _text.GetPreferredValues(_text.text, _maxWidth, 0f);
            float w = Mathf.Min(preferred.x, _maxWidth) + _size * 1.1f;
            float h = Mathf.Max(preferred.y, _size) + _size * 0.7f;

            _text.rectTransform.sizeDelta = new Vector2(Mathf.Min(preferred.x + 0.02f, _maxWidth), h);
            if (_plate != null) _plate.localScale = new Vector3(w, h, 0.02f);
        }

        public void SetColour(Color colour)
        {
            if (_text != null) _text.color = colour;
        }

        /// <summary>Caps how wide the label may grow before the text is clipped.</summary>
        public void SetPlateWidth(float width)
        {
            _maxWidth = width;
            AutoFit();
        }

        // Billboarding runs per rendering camera rather than once per frame against
        // Camera.main: the screenshot tour renders through its own camera, and a label
        // turned to face the player is edge-on — invisible — to any other viewpoint.
        private void OnEnable()  => Camera.onPreCull += FaceCamera;
        private void OnDisable() => Camera.onPreCull -= FaceCamera;

        private void FaceCamera(Camera cam)
        {
            if (cam == null || _text == null) return;

            // TextMeshPro reads from its −Z side, so forward points away from the viewer.
            Vector3 away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(away, Vector3.up);

            float distanceSq = away.sqrMagnitude;
            bool visible = (MaxVisibleDistance <= 0f || distanceSq < MaxVisibleDistance * MaxVisibleDistance)
                        && distanceSq > MinVisibleDistance * MinVisibleDistance;

            // Toggling the renderers (not the GameObjects) keeps this per-camera: onPreCull
            // runs before culling, so each camera sees its own answer.
            var textRenderer = _text.renderer;
            if (textRenderer != null) textRenderer.enabled = visible;
            if (_plateRenderer != null) _plateRenderer.enabled = visible;
        }
    }

    /// <summary>
    /// A label that drifts upward and fades out, then destroys itself — the "+€12.40" that
    /// confirms a sale without needing the player to be watching the balance.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public float Lifetime = 1.8f;
        public float RiseSpeed = 0.9f;

        private WorldLabel _label;
        private float      _age;
        private Color      _colour;

        public static void Spawn(Vector3 position, string text, Color colour, float size = 0.3f)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;

            var floating = go.AddComponent<FloatingText>();
            floating._label  = WorldLabel.Create(go.transform, Vector3.zero, text, size, colour,
                                                 width: 3.2f, plate: false);
            floating._label.MaxVisibleDistance = 0f;
            floating._colour = colour;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float t = _age / Lifetime;
            if (t >= 1f) { Destroy(gameObject); return; }

            // Hold full opacity for the first third, then fade.
            float alpha = t < 0.33f ? 1f : 1f - (t - 0.33f) / 0.67f;
            _label?.SetColour(new Color(_colour.r, _colour.g, _colour.b, alpha));
        }
    }
}
