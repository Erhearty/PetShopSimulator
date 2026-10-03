using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Layer lookups, resolved once by name so the project keeps working even if the
    /// layer indices in TagManager.asset ever move.
    /// </summary>
    public static class GameLayers
    {
        private static int _character = -1;
        private static int _furniture = -1;
        private static int _ghost     = -1;
        private static int _scenery   = -1;
        private static int _heldItem  = -1;

        /// <summary>Name of the layer the first-person held-item viewmodel renders on.</summary>
        public const string HeldItemLayerName = "HeldItem";

        /// <summary>
        /// The held-item viewmodel, drawn only by its overlay camera. Unlike the other lookups
        /// this is -1 (not Default) when the layer is missing, so callers never hide Default.
        /// </summary>
        public static int HeldItem =>
            _heldItem >= 0 ? _heldItem : (_heldItem = LayerMask.NameToLayer(HeldItemLayerName));

        private static int HeldItemBit => HeldItem >= 0 ? 1 << HeldItem : 0;

        /// <summary>Player and customers — excluded from camera collision and build raycasts.</summary>
        public static int Character => Resolve(ref _character, "Character");

        /// <summary>Shelves, pens, the counter — what the interaction system looks for.</summary>
        public static int Furniture => Resolve(ref _furniture, "Furniture");

        /// <summary>
        /// The street outside — walls, road, parked cars. Never interactable. Also carries the road
        /// surface that traffic cars raycast against to snap to the ground, and the traffic cars themselves.
        /// </summary>
        public static int Scenery => Resolve(ref _scenery, "Scenery");

        /// <summary>
        /// The build-mode preview and the invisible barriers that fence the pavement in.
        /// Still collides and still bakes into the NavMesh — it is only excluded from
        /// raycasts, so the camera does not shove itself against an invisible wall.
        /// </summary>
        public static int Ghost => Resolve(ref _ghost, "Ignore Raycast");

        public static LayerMask InteractMask => 1 << Furniture;

        /// <summary>
        /// What the third-person camera is allowed to collide with: real building geometry
        /// only. Scenery (street props, trees, parked cars) is excluded deliberately — a
        /// camera that ducks every time the player walks past a lamp post reads as stutter.
        /// </summary>
        public static LayerMask CameraBlocker =>
            ~((1 << Character) | (1 << Ghost) | (1 << Scenery) | HeldItemBit);

        private static int Resolve(ref int cache, string name)
        {
            if (cache >= 0) return cache;
            int layer = LayerMask.NameToLayer(name);
            cache = layer >= 0 ? layer : 0;
            return cache;
        }
    }
}
