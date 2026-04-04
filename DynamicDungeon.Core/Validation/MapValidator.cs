using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;

namespace DynamicDungeon.Core.Validation
{
    // Validates that the Exit is reachable from Spawn using flood fill.
    public static class MapValidator
    {
        public static bool IsValid(TileMap map)
        {
            var (sx, sy) = map.SpawnPoint;
            var (ex, ey) = map.ExitPoint;

            // Spawn and Exit must not overlap (degenerate map).
            if (sx == ex && sy == ey) return false;

            var reachable = FloodFill.GetReachable(map, sx, sy);
            return reachable.Contains((ex, ey));
        }
    }
}
