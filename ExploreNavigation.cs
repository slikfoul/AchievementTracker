using System;
using UnityEngine;

namespace AchievementTracker
{
    internal static class ExploreNavigation
    {
        // Keep the game's stat identifiers: positive X is the right/east edge on the map,
        // but Player.UpdateStats records it as ExploreWest (including its NoMap variant).
        public static float SignedDistance(string stat, Vector3 position)
        {
            switch (stat)
            {
                case "ExploreNorth":
                case "ExploreNorthNoMap": return position.z;
                case "ExploreSouth":
                case "ExploreSouthNoMap": return -position.z;
                case "ExploreWest":
                case "ExploreWestNoMap": return position.x;
                case "ExploreEast":
                case "ExploreEastNoMap": return -position.x;
                default: throw new ArgumentException("Unknown exploration stat", nameof(stat));
            }
        }
    }
}
