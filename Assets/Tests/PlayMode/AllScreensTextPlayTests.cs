using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;
using PetShop.UI;
using Object = UnityEngine.Object;

namespace PetShop.Tests
{
    /// <summary>
    /// Boots the real game through <see cref="PlaytestHarness"/>, opens every UI screen in turn and
    /// checks every active <see cref="TMP_Text"/> in the scene (canvas and world labels alike): it is
    /// set in the Nunito asset, every character it shows is in that asset, and it does not overflow
    /// unless it clips on purpose. Also checks no legacy <see cref="UnityEngine.UI.Text"/> exists.
    /// </summary>
    public class AllScreensTextPlayTests
    {
        private const int   Seed      = 2718;
        private const float DayLength = 600f;
        /// <summary>0 keeps toasts, floats and the clock where they are while each screen is checked.</summary>
        private const float TimeScale = 0f;
        private const string LogTag = "[AllScreensText]";
        /// <summary>Slack, in pixels, a label's drawn width may exceed its rect width by (rounding).</summary>
        private const float WidthTolerance = 1f;

        /// <summary>
        /// The unicode values in the Nunito asset's character table as loaded, before the boot lays
        /// anything out. The asset is Dynamic, so layout adds any glyph the TTF has to the live table;
        /// only this snapshot shows which characters the prebuilt asset really covers.
        /// </summary>
        private static readonly HashSet<uint> Prebuilt = new();

        /// <summary>A TMP rich-text tag such as &lt;b&gt;, &lt;color=#FFF&gt; or &lt;/color&gt;.</summary>
        private static readonly Regex RichTextTag = new(@"<[^<>]*>");

        /// <summary>
        /// Overflow modes that clip or page on purpose, so isTextOverflowing is expected there.
        /// Page is the guide body, which is split into pages by design.
        /// </summary>
        private static readonly HashSet<TextOverflowModes> DeliberateClipping = new()
        {
            TextOverflowModes.Ellipsis, TextOverflowModes.Truncate, TextOverflowModes.Masking,
            TextOverflowModes.Page,
        };

        /// <summary>
        /// Screens the harness cannot open, each with the reason. Kept in the log next to the list
        /// of screens that were opened.
        /// </summary>
        private static readonly string[] Skipped =
        {
            // The interact prompt is driven by Player.InteractionSystem from what the player aims
            // at; the harness has no aim. Its label is built with the UI font (ShopHUD.BuildPrompt).
            "Interact prompt (needs the player aiming at an interactable)",
        };

        private readonly List<string> _opened   = new();
        private readonly List<string> _failures = new();
        private readonly HashSet<string> _reported = new();
        private TMP_FontAsset _nunito;

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        [UnityTest]
        public IEnumerator EveryScreen_TextIsNunito_HasAllGlyphs_AndDoesNotOverflow()
        {
            _nunito = Resources.Load<TMP_FontAsset>(UIFactory.FontResourcePath);
            Assert.IsNotNull(_nunito, $"No TMP font asset at Resources/{UIFactory.FontResourcePath}.");
            if (Prebuilt.Count == 0)
                foreach (var character in _nunito.characterTable)
                    Prebuilt.Add(character.unicode);
            Assert.IsNotEmpty(Prebuilt, $"{_nunito.name} has an empty character table.");

            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength);
            var game = PlaytestHarness.Game;
            var ui   = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui, "The boot built no GameUI.");
            Time.timeScale = TimeScale;

            // A new game without "skip tutorial" shows the tutorial step in the HUD quest tracker.
            yield return Visit("HUD, inventory bar and tutorial tracker", null, null);

            yield return Visit("Notification", () => ui.HUD.ShowNotification("Delivery arrived — 18 × food"), null);

            yield return Visit("Placement hint (holding a pet pen)",
                () =>
                {
                    game.Furniture.AddOwned(BuildCatalog.PetPen);
                    Assert.IsTrue(game.Build.EnterPlacement(BuildCatalog.PetPen), "could not hold a pet pen");
                },
                () =>
                {
                    game.Build.ExitBuildMode();
                    game.Furniture.TakeOwned(BuildCatalog.PetPen);
                });

            string[] pages = { "Overview", "Stock", "Animals", "Staff", "Build", "Guide" };
            int[] tabs = { StatsPanel.OverviewTab, StatsPanel.StockTab, StatsPanel.AnimalsTab,
                           StatsPanel.StaffTab, StatsPanel.BuildTab, StatsPanel.GuideTab };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = tabs[i];
                yield return Visit($"Shop book: {pages[i]}",
                    () => { ui.Stats.Show(); ui.Stats.ShowTab(tab); },
                    () => { CloseTransient(ui); ui.Stats.Hide(); });
            }

            int sections = GuideContent.Sections().Count;
            for (int i = 0; i < sections; i++)
            {
                int section = i;
                yield return Visit($"Guide section {section}",
                    () => { ui.Guide.Show(); ui.Guide.ShowSection(section); }, ui.Guide.Hide);
            }

            yield return Visit("Pause menu", ui.Pause.Open, () => { ui.Pause.Close(); Time.timeScale = TimeScale; });
            yield return Visit("Settings (with the rebind list)", ui.Settings.Show, ui.Settings.Hide);
            yield return Visit("Day results",
                () => ui.Results.Show(game.Shop.GetCurrentDaySummary()), ui.Results.Hide);
            yield return Visit("Quest journal", ui.Journal.Show, ui.Journal.Hide);
            yield return Visit("Staff panel", ui.StaffBoard.Show, ui.StaffBoard.Hide);
            yield return Visit("Breeding", ui.Breeding.Show, ui.Breeding.Hide);

            var pet = ScriptableObject.CreateInstance<Pet>();
            pet.id = "all-screens-text-pet";
            pet.petName = "Biscuit";
            yield return Visit("Family tree", () => ui.FamilyTree.Show(pet), ui.FamilyTree.Hide);
            Object.Destroy(pet);

            yield return Visit("Showcase", ui.Showcase.Show, ui.Showcase.Hide);
            yield return Visit("Reorder", ui.Reorder.Show, ui.Reorder.Hide);
            yield return Visit("Furniture catalogue", ui.Catalogue.Show, ui.Catalogue.Hide);

            Assert.IsNotNull(ui.BuildView, "GameUI has no build view, so the build toolbar cannot be opened.");
            yield return Visit("Build toolbar", ui.BuildView.Enter, () => CloseTransient(ui));

            // In batch mode the bootstrapper hides the title without building it; build it here.
            yield return Visit("Title screen", () => ui.Title.Build(ui.CanvasRoot, (_, _) => { }), ui.Title.Hide);

            yield return Visit("World labels and a FeedbackFX sale burst",
                () =>
                {
                    var fx = Object.FindAnyObjectByType<FeedbackFX>();
                    Assert.IsNotNull(fx, "No FeedbackFX in the world.");
                    fx.PlaySale(Vector3.up, 12.40f);
                }, null);

            // Last: it pauses the game for good.
            yield return Visit("Game over", () => ui.GameOver.Show("The bank called in the loan."), null);

            var legacy = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in legacy)
                _failures.Add($"legacy UnityEngine.UI.Text at {PathOf(text.transform)}");

            Debug.Log($"{LogTag} screens opened ({_opened.Count}): {string.Join(" | ", _opened)}");
            Debug.Log($"{LogTag} screens skipped ({Skipped.Length}): {string.Join(" | ", Skipped)}");

            Assert.IsEmpty(_failures, $"{_failures.Count} text problem(s):\n" + string.Join("\n", _failures));
        }

        /// <summary>Opens a screen, lets it lay out, checks every active text, then closes it.</summary>
        private IEnumerator Visit(string name, Action open, Action close)
        {
            open?.Invoke();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            CheckTexts(name);
            _opened.Add(name);
            close?.Invoke();
            yield return null;
        }

        private void CheckTexts(string screen)
        {
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                if (!text.isActiveAndEnabled) continue;
                text.ForceMeshUpdate();
                string path = PathOf(text.transform);

                if (text.font != _nunito)
                    Report(screen, path, $"font is '{(text.font != null ? text.font.name : "none")}', not {_nunito.name}");

                string shown = Visible(text);
                if (shown.Length > 0 && !_nunito.HasCharacters(shown, out uint[] missing, false, false)
                    && missing != null && missing.Length > 0)
                    Report(screen, path, $"characters not in {_nunito.name}: {Describe(missing)} in \"{text.text}\"");

                uint[] notPrebuilt = NotPrebuilt(shown);
                if (notPrebuilt.Length > 0)
                    Report(screen, path, $"characters not in the prebuilt {_nunito.name} character table: {Describe(notPrebuilt)} in \"{text.text}\"");

                if (text.isTextOverflowing && !DeliberateClipping.Contains(text.overflowMode))
                    Report(screen, path, $"text overflows its rect (overflowMode {text.overflowMode}): \"{text.text}\"");

                if (!DeliberateClipping.Contains(text.overflowMode))
                {
                    bool noWrap = text.textWrappingMode == TextWrappingModes.NoWrap
                               || text.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap;
                    float drawn = noWrap ? text.preferredWidth : text.textBounds.size.x;
                    float width = text.rectTransform.rect.width;
                    if (drawn > width + WidthTolerance)
                        Report(screen, path, $"text is wider than its rect ({drawn:F1} > {width:F1} px, {(noWrap ? "NoWrap" : "wrapping")}, overflowMode {text.overflowMode}): \"{text.text}\"");
                }
            }
        }

        /// <summary>Records one failure; the same object and problem is reported once, on the first screen.</summary>
        private void Report(string screen, string path, string problem)
        {
            if (_reported.Add(path + "|" + problem))
                _failures.Add($"[{screen}] {path}: {problem}");
        }

        /// <summary>The characters <paramref name="text"/> draws: rich-text tags and control characters removed.</summary>
        private static string Visible(TMP_Text text)
        {
            string raw = text.text ?? "";
            if (text.richText) raw = RichTextTag.Replace(raw, "");
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
                if (c >= ' ' && c != '\u007F') sb.Append(c);
            return sb.ToString();
        }

        /// <summary>The distinct characters of <paramref name="shown"/>, whitespace and controls skipped, not in <see cref="Prebuilt"/>.</summary>
        private static uint[] NotPrebuilt(string shown)
        {
            var missing = new List<uint>();
            for (int i = 0; i < shown.Length; i++)
            {
                if (char.IsWhiteSpace(shown, i) || char.IsControl(shown, i)) continue;
                uint code = (uint)char.ConvertToUtf32(shown, i);
                if (char.IsSurrogatePair(shown, i)) i++;
                if (!Prebuilt.Contains(code) && !missing.Contains(code)) missing.Add(code);
            }
            return missing.ToArray();
        }

        private static string Describe(uint[] codes)
        {
            var sb = new StringBuilder();
            foreach (uint c in codes) sb.Append($"'{char.ConvertFromUtf32((int)c)}' (U+{c:X4}) ");
            return sb.ToString().TrimEnd();
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>Closes the catalogue, guide and build view if a book page opened them.</summary>
        private static void CloseTransient(GameUI ui)
        {
            if (ui.Guide != null) ui.Guide.Hide();
            if (ui.Catalogue != null) ui.Catalogue.Hide();
            if (ui.BuildView != null && ui.BuildView.IsActive) ui.BuildView.Exit();
        }
    }
}
