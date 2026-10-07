using System.Text;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Shop;

namespace PetShop.Dev
{
    /// <summary>
    /// Checks that the places customers must walk between are actually joined on the NavMesh.
    ///
    /// A break here used to be invisible in normal testing: a shopper stranded on the way was
    /// still rung up. The queue now only serves someone standing at the till, so a break shows
    /// as customers giving up in the queue instead.
    /// </summary>
    public static class NavProbe
    {
        public static string Run()
        {
            var generator = Object.FindAnyObjectByType<ShopLayout>();
            if (generator == null) return "[Nav] no ShopLayout";

            Vector3 pavement  = generator.PavementCentre;
            Vector3 forecourt = generator.ForecourtPosition;
            Vector3 inside    = generator.DoorPosition;
            Vector3 till      = generator.TillPosition;

            var report = new StringBuilder("[Nav] connectivity\n");
            bool allGood = true;

            allGood &= Check(report, "pavement › forecourt", pavement,  forecourt);
            allGood &= Check(report, "forecourt › doorway",  forecourt, inside);
            allGood &= Check(report, "doorway › till",       inside,    till);
            allGood &= Check(report, "pavement › till",      pavement,  till);

            report.Append(allGood ? "[Nav] OK — the shop is reachable from the street."
                                  : "[Nav] BROKEN — customers cannot reach the till on foot.");
            return report.ToString();
        }

        private static bool Check(StringBuilder report, string label, Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var a, 6f, NavMesh.AllAreas))
            { report.AppendLine($"  {label,-24} no NavMesh near the start"); return false; }

            if (!NavMesh.SamplePosition(to, out var b, 6f, NavMesh.AllAreas))
            { report.AppendLine($"  {label,-24} no NavMesh near the end"); return false; }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

            bool ok = path.status == NavMeshPathStatus.PathComplete;
            report.AppendLine($"  {label,-24} {path.status,-18} {length,6:F1} m" + (ok ? "" : "   <<<"));
            return ok;
        }
    }
}
