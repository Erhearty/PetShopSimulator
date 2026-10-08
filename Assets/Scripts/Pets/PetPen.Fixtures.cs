using UnityEngine;
using PetShop.Core;
using PetShop.Localization;
using PetShop.UI;

namespace PetShop.Pets
{
    /// <summary>
    /// The two physical things the player clicks on a pen: the feed pad inside it (feeds its pets) and the
    /// plaque by the gate (opens the pen's info panel). Both carry a collider on the pen's layer and a marker
    /// component that <c>InteractionSystem</c> looks for. The colliders are tall so the interaction cast, which
    /// runs at eye height, reaches them although the pad lies on the floor.
    /// </summary>
    public partial class PetPen
    {
        /// <summary>Name of the feed pad's root.</summary>
        public const string FeedSpotName = "PenFeedSpot";
        /// <summary>Name of the info plaque's root.</summary>
        public const string PlaqueName = "PenPlaque";

        /// <summary>Feed pad centre, as shares of the pen from its centre (front-left stall).</summary>
        private const float FeedSpotXShare = -0.25f, FeedSpotZShare = 0.22f;
        /// <summary>Plaque centre sideways, as a share of the pen (beside the gate, outside the front fence).</summary>
        private const float PlaqueXShare = -0.4f;
        private const float PlaqueOutside = 0.12f;
        private const float PlaqueY = 1.0f;

        private const float PadRadius = 0.28f, PadHeight = 0.04f, PadBowlRadius = 0.16f, PadBowlHeight = 0.07f;
        private const float PadPostRadius = 0.03f, PadPostHeight = 0.8f, PadPostBack = 0.22f;
        private const float PadColliderHeight = 1.2f;
        private const float PlaqueWidth = 0.5f, PlaqueBoardHeight = 0.3f, PlaqueThickness = 0.06f;

        /// <summary>The feed pad's root, or null before the pen has started.</summary>
        public Transform FeedSpot { get; private set; }

        /// <summary>The info plaque's root, or null before the pen has started.</summary>
        public Transform Plaque { get; private set; }

        private void BuildFixtures()
        {
            if (FeedSpot != null) return;
            BuildFeedSpot();
            BuildPlaque();
        }

        private void BuildFeedSpot()
        {
            var root = new GameObject(FeedSpotName).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(PenSize * FeedSpotXShare, 0f, PenSize * FeedSpotZShare);
            FeedSpot = root;

            var pad = FixturePiece(MeshBuilder.CreateCylinder(PadRadius, PadHeight,
                          DecorMaterial(MaterialFactory.Palette.Sunflower), "FeedPad"), root);
            pad.localPosition = new Vector3(0f, FloorTop + PadHeight * 0.5f, 0f);

            var bowl = FixturePiece(MeshBuilder.CreateCylinder(PadBowlRadius, PadBowlHeight,
                           DecorMaterial(MaterialFactory.Palette.Feed), "FeedPadBowl"), root);
            bowl.localPosition = new Vector3(0f, FloorTop + PadHeight + PadBowlHeight * 0.5f, 0f);

            var post = FixturePiece(MeshBuilder.CreateCylinder(PadPostRadius, PadPostHeight,
                           DecorMaterial(MaterialFactory.Palette.OakDark), "FeedPadPost"), root);
            post.localPosition = new Vector3(0f, FloorTop + PadPostHeight * 0.5f, -PadPostBack);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, PadColliderHeight * 0.5f, -PadPostBack * 0.25f);
            box.size   = new Vector3(PadRadius * 2.5f, PadColliderHeight, PadRadius * 2.5f + PadPostBack * 0.5f);
            root.gameObject.AddComponent<PenFeedSpot>();
            MeshBuilder.SetLayerRecursive(root.gameObject, gameObject.layer);
        }

        private void BuildPlaque()
        {
            var root = new GameObject(PlaqueName).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(PenSize * PlaqueXShare, 0f, PenSize * 0.5f + PlaqueOutside);
            Plaque = root;

            var post = FixturePiece(MeshBuilder.CreateCylinder(PadPostRadius, PlaqueY,
                           DecorMaterial(MaterialFactory.Palette.OakDark), "PlaquePost"), root);
            post.localPosition = new Vector3(0f, PlaqueY * 0.5f, 0f);

            var board = FixturePiece(MeshBuilder.CreateBox(PlaqueWidth, PlaqueBoardHeight, PlaqueThickness,
                            DecorMaterial(MaterialFactory.Palette.CreamWall), "PlaqueBoard"), root);
            board.localPosition = new Vector3(0f, PlaqueY, 0f);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, PlaqueY * 0.5f + PlaqueBoardHeight * 0.25f, 0f);
            box.size   = new Vector3(PlaqueWidth, PlaqueY + PlaqueBoardHeight * 0.5f, PlaqueThickness * 2f);
            root.gameObject.AddComponent<PenPlaque>();
            MeshBuilder.SetLayerRecursive(root.gameObject, gameObject.layer);
        }

        /// <summary>Parents a fixture primitive and drops its own collider (the fixture root carries the one collider).</summary>
        private static Transform FixturePiece(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            return go.transform;
        }
    }

    /// <summary>Marker on a pen's feed pad: interacting with it feeds the pen's pets.</summary>
    public class PenFeedSpot : MonoBehaviour
    {
        /// <summary>The pen this pad belongs to.</summary>
        public PetPen Pen => GetComponentInParent<PetPen>();
    }

    /// <summary>Marker on a pen's plaque: interacting with it opens the pen's info panel.</summary>
    public class PenPlaque : MonoBehaviour
    {
        /// <summary>The pen this plaque belongs to.</summary>
        public PetPen Pen => GetComponentInParent<PetPen>();
    }
}
