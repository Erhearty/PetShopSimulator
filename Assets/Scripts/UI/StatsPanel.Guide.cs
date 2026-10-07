using System;
using UnityEngine;
using PetShop.Core;
using PetShop.Localization;

namespace PetShop.UI
{
    /// <summary>The Shop book's Guide page: what the guide covers and the way into it.</summary>
    public partial class StatsPanel
    {
        private BookPage _guide;

        /// <summary>Opens the full guide (wired by <see cref="GameUI"/>).</summary>
        public Action OpenGuide { get; set; }

        /// <summary>
        /// Says whether the guide is open over the book (wired by <see cref="GameUI"/>); while it is,
        /// the book and its catalogue ignore the keyboard so only the guide answers.
        /// </summary>
        public Func<bool> GuideOpen { get; set; }

        private bool GuideIsOpen => GuideOpen != null && GuideOpen();

        private void BuildGuidePage()
        {
            _guide = AddPage("Guide", UIFactory.TextBody);
            RefreshGuidePage();
            var open = UIFactory.ButtonKey("OpenGuide", _guide.Root.transform, "guide.book.open",
                new Vector2(0f, LinkRowBottom), new Vector2(LinkWidth, LinkRowTop), UIFactory.TextSmall, UIFactory.Accent);
            open.onClick.AddListener(() => HandOff(OpenGuide));
        }

        /// <summary>Rewrites the page text, so a rebound guide key shows the next time the book opens.</summary>
        private void RefreshGuidePage()
        {
            if (_guide == null) return;
            var topics = new System.Text.StringBuilder();
            topics.AppendLine(Loc.F("guide.book.intro", InputBindings.Label(GameAction.Guide)));
            foreach (var section in GuideContent.Sections()) topics.AppendLine($"•  {section.Title}");
            _guide.Body.text = topics.ToString();
        }
    }
}
