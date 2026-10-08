using UnityEngine;
using UnityEngine.UI;

namespace PetShop.UI
{
    /// <summary>Shared builders for the book pages that stack dynamic rows in a scrolling column.</summary>
    public partial class StatsPanel
    {
        private const float ColumnSpacing = 8f, WheelSpeed = 30f;

        /// <summary>A masked scroll area over the page body; returns the vertical content column.</summary>
        private static Transform BuildScrollContent(Transform page)
        {
            var viewport = UIFactory.Panel("Scroll", page, new Vector2(0f, BodyBottom), new Vector2(1f, BodyTop), Color.clear);
            viewport.AddComponent<RectMask2D>();

            var content = AddColumn(viewport.transform, "Content");
            var rect = (RectTransform)content;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport          = (RectTransform)viewport.transform;
            scroll.content           = rect;
            scroll.horizontal        = false;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = WheelSpeed;
            return content;
        }

        /// <summary>A child that stacks its own children top to bottom and grows to fit them.</summary>
        private static Transform AddColumn(Transform parent, string name)
        {
            var go = UIFactory.Node(name, parent, Vector2.zero, Vector2.one);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing               = ColumnSpacing;
            layout.childControlWidth     = true;
            layout.childControlHeight    = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        /// <summary>Removes every child immediately from layout and (at end of frame) from the scene.</summary>
        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        /// <summary>A fixed-height row slot inside a layout column.</summary>
        private static GameObject AddRow(Transform parent, string name, float height)
        {
            var row = UIFactory.Node(name, parent, Vector2.zero, Vector2.one);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }
    }
}
