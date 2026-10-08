using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Localization;

namespace PetShop.UI
{
    /// <summary>Small helpers for assembling the runtime canvas without prefabs.</summary>
    public static class UIFactory
    {
        // ── Design tokens ───────────────────────────────────────────────────────
        // Every UI colour lives here. Accent is reserved for selection, the primary action
        // and keyboard focus; other panels must use these roles rather than literals.

        /// <summary>Panel surface (#14303A).</summary>
        public static readonly Color Surface     = Hex(0x14, 0x30, 0x3A);
        /// <summary>Raised surface for controls and table rows (#1E4250).</summary>
        public static readonly Color Raised      = Hex(0x1E, 0x42, 0x50);
        /// <summary>Primary text (#F2F6F5).</summary>
        public static readonly Color Ink         = Hex(0xF2, 0xF6, 0xF5);
        /// <summary>Secondary text (#A9C2C4).</summary>
        public static readonly Color InkMuted    = Hex(0xA9, 0xC2, 0xC4);
        /// <summary>Selection, primary action and keyboard focus (#F5B935).</summary>
        public static readonly Color Accent      = Hex(0xF5, 0xB9, 0x35);
        /// <summary>Text drawn on an accent fill: the panel colour, for contrast.</summary>
        public static readonly Color OnAccent    = Hex(0x14, 0x30, 0x3A);
        /// <summary>Good news (#5CCB8A).</summary>
        public static readonly Color Positive    = Hex(0x5C, 0xCB, 0x8A);
        /// <summary>Needs attention soon (#F08A4B).</summary>
        public static readonly Color Warning     = Hex(0xF0, 0x8A, 0x4B);
        /// <summary>Bad news or irreversible actions (#E5555A).</summary>
        public static readonly Color Destructive = Hex(0xE5, 0x55, 0x5A);
        /// <summary>
        /// Fill of destructive action buttons (#B83A3F): a deep red that keeps ink text at ≥ 4.5:1,
        /// where <see cref="Destructive"/> (#E5555A) is only ≈ 3.3:1.
        /// </summary>
        public static readonly Color DestructiveFill = Hex(0xB8, 0x3A, 0x3F);
        /// <summary>Hairlines and dividers (#2F5A68).</summary>
        public static readonly Color Border      = Hex(0x2F, 0x5A, 0x68);
        /// <summary>Full-screen dimmer behind modal panels.</summary>
        public static readonly Color Dim         = new(0f, 0f, 0f, DimAlpha);
        /// <summary>Outline colour around the keyboard-selected control.</summary>
        public static readonly Color FocusRing   = Accent;
        /// <summary>Heavier dimmer for full-stop screens (game over, settings over the title).</summary>
        public static readonly Color DimStrong   = new(0f, 0f, 0f, DimStrongAlpha);
        /// <summary>Faint track behind charts and proportion bars.</summary>
        public static readonly Color TrackFaint  = WithAlpha(Ink, TrackFaintAlpha);
        /// <summary>The HUD crosshair dot.</summary>
        public static readonly Color Crosshair   = WithAlpha(Ink, CrosshairAlpha);
        /// <summary>Fill of a disabled action button: visible on the panel, clearly inert.</summary>
        public static readonly Color DisabledFill = Border;
        /// <summary>Highlighted node (e.g. the focused pet in the family tree).</summary>
        public static readonly Color FocusFill   = Border;
        /// <summary>Toggle-on fill: a deep positive green that keeps ink text at ≥ 4.5:1.</summary>
        public static readonly Color PositiveFill = Hex(0x1A, 0x61, 0x38);
        /// <summary>Recessed well inside a panel (scroll panes, reading areas): darker than the surface.</summary>
        public static readonly Color Recessed    = Hex(0x0E, 0x24, 0x2C);
        /// <summary>White multiplier: leaves an image's sprite/tint untouched.</summary>
        public static readonly Color NoTint      = Color.white;
        /// <summary>Dark text for light swatches (contrast picking).</summary>
        public static readonly Color SwatchInkDark  = Color.black;
        /// <summary>Light text for dark swatches (contrast picking).</summary>
        public static readonly Color SwatchInkLight = Color.white;

        // ── Chart roles (sales categories) ──

        /// <summary>Chart colour: animal sales.</summary>
        public static readonly Color ChartAnimals   = Warning;
        /// <summary>Chart colour: food sales.</summary>
        public static readonly Color ChartFood      = Accent;
        /// <summary>Chart colour: toy sales.</summary>
        public static readonly Color ChartToys      = Hex(0xE0, 0x6B, 0x73);
        /// <summary>Chart colour: accessory sales.</summary>
        public static readonly Color ChartAccessory = Hex(0x70, 0xA8, 0xEB);
        /// <summary>Chart colour: medicine sales.</summary>
        public static readonly Color ChartMedicine  = Positive;

        // ── Legacy names (aliases of the token roles, so older panels keep compiling) ──

        /// <summary>Alias of <see cref="Positive"/>.</summary>
        public static readonly Color Good       = Positive;
        /// <summary>Alias of <see cref="Destructive"/>.</summary>
        public static readonly Color Bad        = Destructive;
        /// <summary>Alias of <see cref="Warning"/>.</summary>
        public static readonly Color Warn       = Warning;
        /// <summary>Alias of <see cref="Surface"/>.</summary>
        public static readonly Color PanelBg    = Surface;
        /// <summary>Alias of <see cref="Surface"/>, slightly translucent for bars over the world.</summary>
        public static readonly Color BarBg      = WithAlpha(Surface, HudAlpha);
        /// <summary>Floating HUD card fill: the panel surface, slightly translucent.</summary>
        public static readonly Color CardBg     = WithAlpha(Surface, HudAlpha);
        /// <summary>Recessed track behind meters and progress bars.</summary>
        public static readonly Color TrackBg    = Border;
        /// <summary>Alias of <see cref="Raised"/>: the default button fill.</summary>
        public static readonly Color ButtonBg   = Raised;
        /// <summary>Alias of <see cref="Accent"/>: selected tab / primary button fill.</summary>
        public static readonly Color ButtonOn   = Accent;

        private const float DimAlpha = 0.62f;
        private const float HudAlpha = 0.9f;
        private const float DimStrongAlpha  = 0.8f;
        private const float TrackFaintAlpha = 0.10f;
        private const float CrosshairAlpha  = 0.55f;
        /// <summary>Height of the accent strip along a modal's top edge (reference pixels).</summary>
        public const float AccentStripHeight = 4f;

        // ── Type, spacing, radius ───────────────────────────────────────────────

        /// <summary>Type scale: small print, captions and table cells.</summary>
        public const float TextSmall   = 14f;
        /// <summary>Type scale: body text and buttons.</summary>
        public const float TextBody    = 18f;
        /// <summary>Type scale: page headings.</summary>
        public const float TextHeading = 24f;
        /// <summary>Type scale: panel titles.</summary>
        public const float TextTitle   = 32f;

        /// <summary>HUD spacing unit (reference pixels): margins and gaps are multiples of this.</summary>
        public const float Gap = 8f;

        /// <summary>Corner radius of panels (reference pixels).</summary>
        public const int PanelRadius   = 14;
        /// <summary>Corner radius of controls (reference pixels).</summary>
        public const int ControlRadius = 8;
        /// <summary>Width of the keyboard focus outline (reference pixels).</summary>
        public const float FocusRingWidth = 2f;

        // Button state tints: multipliers on the button's own fill, so one fill serves every state.
        private static readonly Color HoverTint    = new(1.15f, 1.15f, 1.15f, 1f);
        private static readonly Color PressedTint  = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color DisabledTint = new(0.6f, 0.6f, 0.6f, 0.5f);
        private const float ButtonFadeSeconds = 0.08f;

        /// <summary>Smallest auto-size, as a fraction of the design size, for single-line text and buttons.</summary>
        public const float AutoSizeMinFraction = 0.8f;

        private const int SpriteSize = 48;
        private const float SpritePixelsPerUnit = 100f;

        /// <summary>Resources path of the Nunito TMP SDF font asset (Assets/Resources/Fonts/Nunito SDF).</summary>
        public const string FontResourcePath = "Fonts/Nunito SDF";

        private static Sprite _rounded, _control;
        private static TMP_FontAsset _font;
        private static bool _fontLoaded;

        private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        /// <summary><paramref name="c"/> with its alpha replaced.</summary>
        public static Color WithAlpha(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

        /// <summary>
        /// The UI font: Nunito when its SDF asset is in Resources, otherwise null so TMP uses
        /// its default font asset.
        /// </summary>
        public static TMP_FontAsset Font()
        {
            if (_fontLoaded) return _font;
            _fontLoaded = true;
            _font = Resources.Load<TMP_FontAsset>(FontResourcePath);
            return _font;
        }

        /// <summary>A white 9-sliced rounded-rectangle sprite (panel radius), generated once and tinted by Image.color.</summary>
        public static Sprite RoundedSprite() => _rounded != null ? _rounded : _rounded = MakeRounded(PanelRadius);

        /// <summary>A white 9-sliced rounded-rectangle sprite with the smaller control radius.</summary>
        public static Sprite ControlSprite() => _control != null ? _control : _control = MakeRounded(ControlRadius);

        private static Sprite MakeRounded(int radius)
        {
            const int size = SpriteSize;
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

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit, 0,
                                       SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
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

        /// <summary>
        /// A modal panel: an anchored panel with the rounded panel sprite, so every dialog shares
        /// the same corner radius. Same parameters as <see cref="Panel"/>; unlike <see cref="Card"/>
        /// it keeps raycasts so clicks on the panel do not fall through.
        /// </summary>
        public static GameObject ModalPanel(string name, Transform parent,
                                            Vector2 anchorMin, Vector2 anchorMax, Color color,
                                            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go  = Panel(name, parent, anchorMin, anchorMax, color, offsetMin, offsetMax);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSprite();
            img.type   = Image.Type.Sliced;
            return go;
        }

        /// <summary>
        /// The shared modal header: an accent strip inset inside the rounded corners and a title
        /// in the title size. Returns the title label.
        /// </summary>
        public static TMP_Text Header(Transform panel, string title, Vector2 titleMin, Vector2 titleMax)
        {
            Panel("Accent", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), Accent,
                  new Vector2(PanelRadius, -AccentStripHeight), new Vector2(-PanelRadius, 0f));
            return Label("Heading", panel, title, titleMin, titleMax, TextHeading, Ink);
        }

        /// <summary>The RectTransform of a UI object (adds one, with a warning, if it was built bare).</summary>
        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) return rt;

            // A GameObject created bare already has a Transform, and swapping that for a
            // RectTransform after the fact is unreliable — build UI objects with one.
            Debug.LogWarning($"[UI] '{go.name}' was created without a RectTransform.");
            return go.AddComponent<RectTransform>();
        }

        /// <summary>An empty anchored UI object.</summary>
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

        /// <summary>An anchored, flat-filled UI object.</summary>
        public static GameObject Panel(string name, Transform parent,
                                       Vector2 anchorMin, Vector2 anchorMax, Color color,
                                       Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = Node(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            go.AddComponent<Image>().color = color;
            return go;
        }

        /// <summary>A TextMeshPro label in the UI font.</summary>
        public static TMP_Text Label(string name, Transform parent, string text,
                                     Vector2 anchorMin, Vector2 anchorMax,
                                     float fontSize = TextBody, Color? color = null,
                                     TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go  = Node(name, parent, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var font = Font();
            if (font != null) tmp.font = font;
            tmp.text                 = text;
            tmp.fontSize             = fontSize;
            tmp.color                = color ?? Ink;
            tmp.alignment            = align;
            tmp.textWrappingMode     = TextWrappingModes.Normal;
            tmp.raycastTarget        = false;
            tmp.overflowMode         = TextOverflowModes.Ellipsis;
            return tmp;
        }

        /// <summary>
        /// A <see cref="Label"/> showing localisation <paramref name="key"/> (formatted with
        /// <paramref name="args"/>), re-read whenever the language changes, and auto-sized down to
        /// <see cref="AutoSizeMinFraction"/> of <paramref name="fontSize"/> so longer translations fit.
        /// </summary>
        public static TMP_Text LabelKey(string name, Transform parent, string key,
                                        Vector2 anchorMin, Vector2 anchorMax,
                                        float fontSize = TextBody, Color? color = null,
                                        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
                                        params object[] args)
        {
            var tmp = Label(name, parent, "", anchorMin, anchorMax, fontSize, color, align);
            AutoFit(tmp);
            LocalizedText.Bind(tmp, key, args);
            return tmp;
        }

        /// <summary>A <see cref="Button"/> whose label shows localisation <paramref name="key"/> and follows language changes.</summary>
        public static Button ButtonKey(string name, Transform parent, string key,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       float fontSize = 17f, Color? bg = null, params object[] args)
        {
            var btn = Button(name, parent, "", anchorMin, anchorMax, fontSize, bg);
            LocalizedText.Bind(btn.GetComponentInChildren<TMP_Text>(), key, args);
            return btn;
        }

        /// <summary>Binds an existing label to <paramref name="key"/> so it follows language changes.</summary>
        public static void Localize(TMP_Text label, string key, params object[] args) =>
            LocalizedText.Bind(label, key, args);

        /// <summary>Lets <paramref name="text"/> shrink to <see cref="AutoSizeMinFraction"/> of its size instead of truncating.</summary>
        public static void AutoFit(TMP_Text text)
        {
            if (text == null) return;
            float size = text.fontSize;
            text.fontSizeMax      = size;
            text.fontSizeMin      = size * AutoSizeMinFraction;
            text.enableAutoSizing = true;
        }

        /// <summary>
        /// A rounded button with hover/pressed/selected/disabled states and an accent outline while
        /// keyboard-selected. An accent fill gets panel-coloured text for contrast. The label
        /// auto-sizes down to <see cref="AutoSizeMinFraction"/> of <paramref name="fontSize"/>.
        /// </summary>
        public static Button Button(string name, Transform parent, string text,
                                    Vector2 anchorMin, Vector2 anchorMax,
                                    float fontSize = 17f, Color? bg = null)
        {
            Color fill = bg ?? ButtonBg;
            var go    = Panel(name, parent, anchorMin, anchorMax, fill);
            var image = go.GetComponent<Image>();
            image.sprite = ControlSprite();
            image.type   = Image.Type.Sliced;
            var btn   = go.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.colors = ButtonStates();
            go.AddComponent<FocusOutline>();

            var label = Label($"{name}_Label", go.transform, text, Vector2.zero, Vector2.one,
                              fontSize, LabelColourOn(fill), TextAlignmentOptions.Center);
            AutoFit(label);
            return btn;
        }

        /// <summary>The shared button state block (tints applied over the fill).</summary>
        public static ColorBlock ButtonStates()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor      = Color.white;
            colors.highlightedColor = HoverTint;
            colors.pressedColor     = PressedTint;
            colors.selectedColor    = Color.white;
            colors.disabledColor    = DisabledTint;
            colors.colorMultiplier  = 1f;
            colors.fadeDuration     = ButtonFadeSeconds;
            return colors;
        }

        /// <summary>Marks a button selected (accent fill, panel text) or not (raised fill, ink text).</summary>
        public static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            Color fill = selected ? Accent : Raised;
            if (image != null) image.color = fill;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = LabelColourOn(fill);
        }

        /// <summary>Wraps <paramref name="text"/> in a TMP rich-text colour tag for <paramref name="colour"/>.</summary>
        public static string Tint(string text, Color colour) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color>";

        /// <summary>Text colour readable on <paramref name="fill"/>.</summary>
        public static Color LabelColourOn(Color fill)
        {
            if (fill == Accent) return OnAccent;
            // The deep fills (DestructiveFill, PositiveFill) were chosen to carry ink text at ≥ 4.5:1.
            if (fill == DestructiveFill || fill == PositiveFill) return Ink;
            return Ink;
        }
    }
}
