using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Pets;

namespace PetShop.Commerce
{
    /// <summary>
    /// The walking jobs: restocking shelves from the stockroom and servicing pens. Shelves and
    /// pens are found in the scene each time, so the assistant needs no registry.
    /// </summary>
    public partial class Assistant
    {
        [Header("Tuning")]
        [Tooltip("Seconds between restock trips at skill 1.")]
        public float RestockIntervalBase = 14f;
        [Tooltip("Seconds shaved off the restock interval per skill level above 1.")]
        public float RestockIntervalPerSkill = 2.5f;
        [Tooltip("Shortest possible gap between restock trips.")]
        public float RestockIntervalMin = 3f;
        [Tooltip("Units carried per trip at skill 1.")]
        public int   UnitsPerTripBase = 2;
        [Tooltip("Extra units carried per trip per skill level above 1.")]
        public int   UnitsPerTripPerSkill = 2;
        [Tooltip("Seconds between checks on the pens.")]
        public float FeedCheckInterval = 6f;
        [Tooltip("Give up on a walk after this many seconds.")]
        public float WalkTimeout = 20f;
        [Tooltip("How far to search for the NavMesh around the assistant.")]
        public float NavMeshSearchRadius = 4f;

        /// <summary>Seconds between restock trips at the current skill.</summary>
        public float RestockInterval =>
            Mathf.Max(RestockIntervalMin, RestockIntervalBase - RestockIntervalPerSkill * (Skill - StaffCandidate.MinSkill));

        /// <summary>Most units moved from stockroom to shelf in one trip at the current skill.</summary>
        public int UnitsPerTrip =>
            Mathf.Max(1, UnitsPerTripBase + UnitsPerTripPerSkill * (Skill - StaffCandidate.MinSkill));

        // ── Restocker ───────────────────────────────────────────────────────────────────────

        /// <summary>Every few seconds: nearest shelf with room and stock waiting, fill it, come back.</summary>
        private IEnumerator RestockLoop()
        {
            if (_away) yield return ReturnToTill();
            while (true)
            {
                yield return new WaitForSeconds(RestockInterval);
                var shelf = FindRestockTarget();
                if (shelf == null) continue;

                yield return WalkTo(shelf.transform.position);
                if (shelf != null) ReportRestock(shelf, MoveFromWarehouse(shelf, UnitsPerTrip));
                yield return ReturnToTill();
            }
        }

        /// <summary>Nearest shelf that has space and whose category has units in the stockroom.</summary>
        private ShelfUnit FindRestockTarget()
        {
            if (Shop == null) return null;
            ShelfUnit best = null;
            float bestDistance = float.MaxValue;
            foreach (var shelf in FindObjectsByType<ShelfUnit>(FindObjectsSortMode.None))
            {
                if (shelf == null || !shelf.HasSpace || Shop.Warehouse(shelf.Category) <= 0) continue;
                if (ProductsFor(shelf).Count == 0) continue;
                float distance = (shelf.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = shelf;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Products this shelf can take: its current lines, else the catalogue's for its category.</summary>
        private List<ProductItem> ProductsFor(ShelfUnit shelf)
        {
            var products = new List<ProductItem>();
            foreach (var line in shelf.Lines)
                if (line.Product != null && line.Units < shelf.MaxPerLine) products.Add(line.Product);
            if (shelf.Lines.Count >= shelf.MaxLines || Catalog == null) return products;

            foreach (var product in Catalog.GetByCategory(shelf.Category))
                if (product != null && !products.Contains(product)) products.Add(product);
            return products;
        }

        /// <summary>
        /// Moves up to <paramref name="maxUnits"/> from the stockroom onto <paramref name="shelf"/>,
        /// one at a time. Never spends cash; a unit the shelf refuses goes back to the stockroom.
        /// </summary>
        private int MoveFromWarehouse(ShelfUnit shelf, int maxUnits)
        {
            int moved = 0;
            foreach (var product in ProductsFor(shelf))
            {
                while (moved < maxUnits)
                {
                    if (Shop.TakeFromWarehouse(shelf.Category, 1) != 1) return moved;
                    if (shelf.AddStock(product, 1) == 1) { moved++; continue; }
                    Shop.AddToWarehouse(shelf.Category, 1);
                    break;
                }
            }
            return moved;
        }

        private void ReportRestock(ShelfUnit shelf, int units)
        {
            if (units > 0)
                Notify($"{DisplayName} put {units} {shelf.Category} on the shelf from the stockroom.");
        }

        // ── Feeder ───────────────────────────────────────────────────────────────────────────

        /// <summary>Every few seconds: find a pen that needs service, walk over, pay and service it.</summary>
        private IEnumerator FeedLoop()
        {
            if (_away) yield return ReturnToTill();
            while (true)
            {
                yield return new WaitForSeconds(FeedCheckInterval);
                var pen = FindPenNeedingService();
                if (pen == null) continue;

                yield return WalkTo(pen.transform.position);
                if (pen != null && pen.NeedsService) ServicePen(pen);
                yield return ReturnToTill();
            }
        }

        /// <summary>Nearest pen that wants feeding or mucking out.</summary>
        private PetPen FindPenNeedingService()
        {
            PetPen best = null;
            float bestDistance = float.MaxValue;
            foreach (var pen in FindObjectsByType<PetPen>(FindObjectsSortMode.None))
            {
                if (pen == null || !pen.NeedsService) continue;
                float distance = (pen.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = pen;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Pays the pen's upkeep and services it; says so when the till cannot cover it.</summary>
        private void ServicePen(PetPen pen)
        {
            if (Shop == null) return;
            float cost = pen.ServiceCost;
            if (!Shop.ChangeBalance(-cost, "Pen upkeep"))
            {
                Notify($"{DisplayName} could not afford €{cost:N0} to look after the {pen.PenSpecies} pen.");
                return;
            }
            pen.Service();
            Notify($"{DisplayName} fed and cleaned the {pen.PenSpecies} pen — €{cost:N0}.");
        }

        // ── Walking ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Walks towards <paramref name="target"/>, giving up after <see cref="WalkTimeout"/>.
        /// With no reachable NavMesh the job still happens, just without the walk.
        /// </summary>
        private IEnumerator WalkTo(Vector3 target)
        {
            _away = true;
            if (!EnsureOnNavMesh()) yield break;
            if (NavMesh.SamplePosition(target, out var hit, NavMeshSearchRadius, NavMesh.AllAreas))
                target = hit.position;
            if (!_agent.SetDestination(target)) yield break;

            float timeout = WalkTimeout;
            while (timeout > 0f && _agent != null && _agent.enabled)
            {
                timeout -= Time.deltaTime;
                if (!_agent.pathPending && ArrivedOrStuck()) yield break;
                yield return null;
            }
        }

        private bool ArrivedOrStuck() =>
            _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
            _agent.remainingDistance <= _agent.stoppingDistance;

        /// <summary>Walks back to the till, then stands exactly at the station facing the queue.</summary>
        private IEnumerator ReturnToTill()
        {
            yield return WalkTo(_homePosition);
            if (_agent != null) _agent.enabled = false;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            _away = false;
        }

        /// <summary>Enables the agent on the nearest NavMesh point; false when there is none nearby.</summary>
        private bool EnsureOnNavMesh()
        {
            if (_agent == null) return false;
            if (_agent.enabled && _agent.isOnNavMesh) return true;
            if (!NavMesh.SamplePosition(transform.position, out var hit, NavMeshSearchRadius, NavMesh.AllAreas))
                return false;

            transform.position = hit.position;
            _agent.enabled = true;
            return _agent.isOnNavMesh || _agent.Warp(hit.position);
        }
    }
}
