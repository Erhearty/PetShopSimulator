using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PetShop.UI
{
    /// <summary>Small helpers for assembling the runtime canvas without prefabs.</summary>
    public static class UIFactory
    {
        public static readonly Color Ink        = new(0.90f, 0.94f, 0.98f);
        public static readonly Color InkMuted   = new(0.62f, 0.68f, 0.76f);
        public static readonly Color Accent     = new(0.45f, 0.78f, 0.98f);
        public static readonly Color Good       = new(0.45f, 0.86f, 0.58f);
        public static readonly Color Bad        = new(0.95f, 0.45f, 0.42f);
        public static readonly Color PanelBg    = new(0.07f, 0.09f, 0.13f, 0.94f);
        public static readonly Color BarBg      = new(0.07f, 0.09f, 0.13f, 0.86f);
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
