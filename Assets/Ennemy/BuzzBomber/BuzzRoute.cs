using UnityEngine;

namespace SonicFX.Buzz
{
    public enum BuzzRouteMode { AllerRetour, Boucle }

    [DisallowMultipleComponent]
    public class BuzzRoute : MonoBehaviour
    {
        public BuzzRouteMode mode = BuzzRouteMode.AllerRetour;
        [Tooltip("Ordre du parcours. Les points restent fixes pendant que l'ennemi vole.")]
        public BuzzRoutePoint[] points = new BuzzRoutePoint[0];

        public int FirstValid()
        {
            if (points == null) return -1;
            for (int i = 0; i < points.Length; i++) if (points[i] != null) return i;
            return -1;
        }

        public int NextValid(int current, ref int direction)
        {
            return BuzzRouteTraversal.Next(points == null ? 0 : points.Length, current, ref direction,
                mode == BuzzRouteMode.Boucle, i => points[i] != null);
        }

        private void OnDrawGizmos()
        {
            if (points == null) return;
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            Transform first = null, previous = null;
            foreach (var point in points)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.transform.position, 0.25f);
                if (previous != null) Gizmos.DrawLine(previous.position, point.transform.position);
                if (first == null) first = point.transform;
                previous = point.transform;
            }
            if (mode == BuzzRouteMode.Boucle && first != null && previous != first)
                Gizmos.DrawLine(previous.position, first.position);
        }
    }

    // Independent of scene objects, so empty and partially deleted routes can be verified directly.
    public static class BuzzRouteTraversal
    {
        public static int Next(int count, int current, ref int direction, bool loop, System.Func<int, bool> valid)
        {
            if (count <= 0) return -1;
            direction = direction < 0 ? -1 : 1;
            current = System.Math.Max(0, System.Math.Min(current, count - 1));
            for (int attempt = 0; attempt < count * 2; attempt++)
            {
                int next = current + direction;
                if (loop) next = (next + count) % count;
                else if (next < 0 || next >= count)
                {
                    direction = -direction;
                    next = current + direction;
                    if (next < 0 || next >= count) next = current;
                }
                current = next;
                if (valid(current)) return current;
            }
            return -1;
        }

    }
}
