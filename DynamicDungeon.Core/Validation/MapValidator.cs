using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;

namespace DynamicDungeon.Core.Validation
{
    // Validates that the Exit is reachable from Spawn using flood fill.
    public static class MapValidator
    {
        public static bool IsValid(TileMap map) => Validate(map, out _);

        /// <summary>
        /// Returns true if the map is valid. When false, <paramref name="failureReason"/>
        /// describes why (e.g. "Exit unreachable from Spawn", "Spawn equals Exit").
        /// </summary>
        public static bool Validate(TileMap map, out string failureReason)
        {
            var (sx, sy) = map.SpawnPoint;
            var (ex, ey) = map.ExitPoint;

            if (sx == ex && sy == ey)
            {
                failureReason = "Spawn and Exit occupy the same tile (degenerate map)";
                return false;
            }

            var reachable = FloodFill.GetReachable(map, sx, sy);
            if (!reachable.Contains((ex, ey)))
            {
                failureReason = $"Exit ({ex},{ey}) is not reachable from Spawn ({sx},{sy}) — {reachable.Count} floor tiles reachable";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
