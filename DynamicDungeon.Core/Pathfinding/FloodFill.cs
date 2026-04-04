using System.Collections.Generic;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Pathfinding
{
    // BFS flood fill from a starting tile.
    // Used by MapValidator to confirm the Exit is reachable from Spawn.
    public static class FloodFill
    {
        private static readonly (int dx, int dy)[] CardinalDirections =
        {
            (0, 1), (0, -1), (1, 0), (-1, 0)
        };

        // Returns the set of all (x,y) positions reachable from (startX, startY)
        // by walking on non-Wall tiles.
        public static HashSet<(int x, int y)> GetReachable(TileMap map, int startX, int startY)
        {
            var visited = new HashSet<(int, int)>();
            var queue   = new Queue<(int x, int y)>();

            visited.Add((startX, startY));
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                foreach (var (dx, dy) in CardinalDirections)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    if (map.Get(nx, ny) == Tile.Wall) continue;
                    if (visited.Contains((nx, ny))) continue;

                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }

            return visited;
        }
    }
}
