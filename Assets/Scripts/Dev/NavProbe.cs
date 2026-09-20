using System.Text;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Shop;

namespace PetShop.Dev
{
    /// <summary>
    /// Checks that the places customers must walk between are actually joined on the NavMesh.
    ///
    /// A break here is invisible in normal testing: <c>CustomerAI.NavigateTo</c> gives up
    /// after a timeout but the shopper stays in the checkout queue regardless, so an
    /// assistant still rings up sales for people stranded on the pavement. The logs look
    /// healthy while nobody has entered the building.
    /// </summary>
    public static class NavProbe
    {
        public static string Run()
        {
            var generator = Object.FindAnyObjectByType<ShopGenerator>();
            if (generator == null) return "[Nav] no ShopGenerator";

            Vector3 pavement  = generator.Street != null ? generator.Street.PavementCentre
                                                         : generator.ForecourtPosition + Vector3.forward * 6f;
            Vector3 forecourt = generator.ForecourtPosition;
            Vector3 inside    = generator.DoorPosition;
            Vector3 till      = generator.ShopCentre + new Vector3(0f, 0f, -generator.RoomDepth * 0.5f + 2.6f);

            var report = new StringBuilder("[Nav] connectivity\n");
            bool allGood = true;

            allGood &= Check(report, "pavement → forecourt", pavement,  forecourt);
            allGood &= Check(report, "forecourt → doorway",  forecourt, inside);
            allGood &= Check(report, "doorway → till",       inside,    till);
            allGood &= Check(report, "pavement → till",      pavement,  till);

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
