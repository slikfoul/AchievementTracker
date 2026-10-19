using UnityEngine;

namespace AchievementTracker
{
    /// <summary>Only screen layout changes may invalidate a sailing placement; camera/heading are excluded.</summary>
    internal struct SailingLayoutKey
    {
        public int HudId, ShipId;
        public Rect Desired, Viewport, MiniMap;
        public Vector3 Scale;

        public bool Matches(SailingLayoutKey other) => HudId == other.HudId && ShipId == other.ShipId &&
            SameRect(Desired, other.Desired) && SameRect(Viewport, other.Viewport) &&
            SameRect(MiniMap, other.MiniMap) && (Scale - other.Scale).sqrMagnitude < 0.000001f;

        private static bool SameRect(Rect a, Rect b) =>
            (a.position - b.position).sqrMagnitude < 0.01f && (a.size - b.size).sqrMagnitude < 0.01f;
    }

    /// <summary>Find a clear position in HUD coordinates without changing the saved position.</summary>
    internal static class HudPlacement
    {
        public static bool TryAvoid(Rect desired, Rect obstacle, Rect viewport, float gap, out Rect placed,
            bool preferBelow = false)
        {
            placed = desired;
            Rect blocked = Rect.MinMaxRect(obstacle.xMin - gap, obstacle.yMin - gap,
                obstacle.xMax + gap, obstacle.yMax + gap);
            if (!desired.Overlaps(blocked)) return true;
            if (viewport.width <= 0f || viewport.height <= 0f || desired.width > viewport.width) return false;

            // Keep the tracker underneath the sailing UI when that space fits the complete panel.
            if (preferBelow && blocked.yMin - desired.height >= viewport.yMin)
            {
                Rect below = new Rect(Mathf.Clamp(desired.x, viewport.xMin, viewport.xMax - desired.width),
                    blocked.yMin - desired.height, desired.width, desired.height);
                if (below.yMax <= viewport.yMax)
                {
                    placed = below;
                    return true;
                }
            }

            bool found = false;
            float bestDistance = float.PositiveInfinity;
            Consider(new Rect(blocked.xMin - desired.width, desired.y, desired.width, desired.height),
                desired, blocked, viewport, ref found, ref bestDistance, ref placed);
            Consider(new Rect(desired.x, blocked.yMin - desired.height, desired.width, desired.height),
                desired, blocked, viewport, ref found, ref bestDistance, ref placed);
            Consider(new Rect(desired.x, blocked.yMax, desired.width, desired.height),
                desired, blocked, viewport, ref found, ref bestDistance, ref placed);
            Consider(new Rect(blocked.xMax, desired.y, desired.width, desired.height),
                desired, blocked, viewport, ref found, ref bestDistance, ref placed);
            return found;
        }

        private static void Consider(Rect candidate, Rect desired, Rect blocked, Rect viewport,
            ref bool found, ref float bestDistance, ref Rect placed)
        {
            candidate.x = Mathf.Clamp(candidate.x, viewport.xMin, viewport.xMax - candidate.width);
            // A long tracked list may already extend below the screen; keep its heading visible.
            candidate.y = candidate.height > viewport.height ? viewport.yMax - candidate.height
                : Mathf.Clamp(candidate.y, viewport.yMin, viewport.yMax - candidate.height);
            if (candidate.Overlaps(blocked)) return;
            float distance = (candidate.position - desired.position).sqrMagnitude;
            if (distance >= bestDistance) return;
            found = true;
            bestDistance = distance;
            placed = candidate;
        }
    }
}
