using UnityEngine;
using PetShop.Core;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>
    /// Spawns the shopkeeper and the first-person camera that follows them.
    /// </summary>
    internal class PlayerRigSpawner
    {
        private readonly ShopBuildContext _ctx;

        public PlayerRigSpawner(ShopBuildContext ctx)
        {
            _ctx = ctx;
        }

        // ── Player ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the player object and returns its transform. The caller assigns
        /// <see cref="ShopGenerator.Player"/> from it and then calls <see cref="SpawnCamera"/>,
        /// the same order as before the split.
        /// </summary>
        public Transform SpawnPlayer()
        {
            var playerGo = new GameObject("Player") { layer = GameLayers.Character };
            playerGo.transform.position = _ctx.ForecourtPosition + new Vector3(2f, 0f, 2f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height     = 1.8f;
            cc.radius     = 0.28f;
            cc.center     = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth  = 0.04f;

            // The shopkeeper is always the same face; customers are randomised.
            CharacterFactory.Attach(playerGo, () => cc.velocity, variant: 1);

            playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<InteractionSystem>();

            return playerGo.transform;
        }

        public void SpawnCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.nearClipPlane   = 0.1f;
            // The city blocks run past z = 300. A 200 m far plane sliced them off in a hard
            // straight line across the skyline; fog now takes over well before this.
            cam.farClipPlane    = 900f;
            cam.fieldOfView     = 68f;   // a touch wide, which suits first person indoors

            // First person is the default view. The orbit camera is left in the project and
            // can be swapped back in, but nothing creates it.
            var orbit = cam.GetComponent<ThirdPersonCamera>();
            if (orbit != null) Object.Destroy(orbit);

            var fpc = cam.GetComponent<FirstPersonCamera>() ?? cam.gameObject.AddComponent<FirstPersonCamera>();
            fpc.Body = target;
        }
    }
}
