using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PetShop.UI
{
    /// <summary>Small helpers for assembling the runtime canvas without prefabs.</summary>
    public static class UIFactory
    {
        public static readonly Color Ink        = new(0.93f, 0.95f, 0.98f);
        public static readonly Color InkMuted   = new(0.64f, 0.70f, 0.78f);
        public static readonly Color Accent     = new(0.47f, 0.80f, 0.99f);
        public static readonly Color Good       = new(0.46f, 0.88f, 0.62f);
        public static readonly Color Bad        = new(0.97f, 0.47f, 0.44f);
        public static readonly Color Warn       = new(0.98f, 0.80f, 0.40f);
        public static readonly Color PanelBg    = new(0.07f, 0.09f, 0.13f, 0.94f);
        public static readonly Color BarBg      = new(0.07f, 0.09f, 0.13f, 0.86f);
        /// <summary>Floating HUD card fill: dark, slightly translucent.</summary>
        public static readonly Color CardBg     = new(0.08f, 0.10f, 0.15f, 0.82f);
        /// <summary>Recessed track behind meters and progress bars.</summary>
        public static readonly Color TrackBg    = new(1f, 1f, 1f, 0.12f);

        /// <summary>HUD spacing unit (reference pixels): margins and gaps are multiples of this.</summary>
        public const float Gap = 8f;

        private static Sprite _rounded;

        /// <summary>A white 9-sliced rounded-rectangle sprite, generated once and tinted by Image.color.</summary>
        public static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;

            const int size = 48, radius = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();

            _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _rounded.hideFlags = HideFlags.HideAndDontSave;
            return _rounded;
        }

        /// <summary>A rounded, tinted panel — the building block of the floating HUD cards and chips.</summary>
        public static GameObject Card(string name, Transform parent,
                                      Vector2 anchorMin, Vector2 anchorMax, Color color,
                                      Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go  = Panel(name, parent, anchorMin, anchorMax, color, offsetMin, offsetMax);
            var img = go.GetComponent<Image>();
            img.sprite        = RoundedSprite();
            img.type          = Image.Type.Sliced;
            img.raycastTarget = false;
            return go;
        }
        public static readonly Color ButtonBg   = new(0.16f, 0.21f, 0.29f, 0.96f);
        public static readonly Color ButtonOn   = new(0.22f, 0.50f, 0.80f, 0.98f);

        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) return rt;

            // A GameObject created bare already has a Transform, and swapping that for a
            // RectTransform after the fact is unreliable — build UI objects with one.
            Debug.LogWarning($"[UI] '{go.name}' was created without a RectTransform.");
            return go.AddComponent<RectTransform>();
        }

        public static GameObject Node(string name, Transform parent,
                                      Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        public static GameObject Panel(string name, Transform parent,
                                       Vector2 anchorMin, Vector2 anchorMax, Color color,
                                       Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = Node(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            go.AddComponent<Image>().color = color;
            return go;
        }

        public static TMP_Text Label(string name, Transform parent, string text,
                                     Vector2 anchorMin, Vector2 anchorMax,
                                     float fontSize = 18f, Color? color = null,
                                     TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go  = Node(name, parent, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text                 = text;
            tmp.fontSize             = fontSize;
            tmp.color                = color ?? Ink;
            tmp.alignment            = align;
            tmp.textWrappingMode     = TextWrappingModes.Normal;
            tmp.raycastTarget        = false;
            tmp.overflowMode         = TextOverflowModes.Truncate;
            return tmp;
        }

        public static Button Button(string name, Transform parent, string text,
                                    Vector2 anchorMin, Vector2 anchorMax,
                                    float fontSize = 17f, Color? bg = null)
        {
            var go    = Panel(name, parent, anchorMin, anchorMax, bg ?? ButtonBg);
            var image = go.GetComponent<Image>();
            var btn   = go.AddComponent<Button>();
            btn.targetGraphic = image;

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            Label($"{name}_Label", go.transform, text, Vector2.zero, Vector2.one,
                  fontSize, Ink, TextAlignmentOptions.Center);
            return btn;
        }
    }
}
