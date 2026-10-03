using System.Collections.Generic;
using PetShop.Core;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// Trigger volume over a crosswalk. Tracks Character-layer occupants (player, customers) so cars can stop.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CrosswalkZone : MonoBehaviour
    {
        private static readonly List<CrosswalkZone> AllZones = new List<CrosswalkZone>();

        private readonly HashSet<Collider> _occupants = new HashSet<Collider>();
        private BoxCollider _box;

        /// <summary>Every enabled crosswalk in the scene.</summary>
        public static IReadOnlyList<CrosswalkZone> All => AllZones;

        /// <summary>True while at least one live Character-layer collider is inside.</summary>
        public bool IsOccupied
        {
            get
            {
                _occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                return _occupants.Count > 0;
            }
        }

        /// <summary>The zone's world-space bounds.</summary>
        public Bounds WorldBounds => Box.bounds;

        private BoxCollider Box => _box != null ? _box : (_box = GetComponent<BoxCollider>());

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnEnable() { Box.isTrigger = true; AllZones.Add(this); }

        private void OnDisable() { AllZones.Remove(this); _occupants.Clear(); }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.gameObject.layer == GameLayers.Character) _occupants.Add(other);
        }

        private void OnTriggerExit(Collider other) => _occupants.Remove(other);

        /// <summary>
        /// World position of the stop line for traffic travelling along <paramref name="travelDir"/>:
        /// the zone centre pulled back to its entry edge.
        /// </summary>
        public Vector3 StopLinePosition(Vector3 travelDir)
        {
            Bounds b = Box.bounds;
            Vector3 dir = new Vector3(travelDir.x, 0f, travelDir.z).normalized;
            float reach = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            return b.center - dir * reach;
        }
    }
}
