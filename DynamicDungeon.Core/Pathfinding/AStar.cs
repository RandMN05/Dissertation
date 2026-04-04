using System;
using System.Collections.Generic;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Pathfinding
{
    // A* pathfinding with Manhattan distance heuristic.
    // Returns the shortest walkable path from start to end, or an empty list if unreachable.
    public static class AStar
    {
        private static readonly (int dx, int dy)[] CardinalDirections =
        {
            (0, 1), (0, -1), (1, 0), (-1, 0)
        };

        public static List<(int x, int y)> FindPath(TileMap map, (int x, int y) start, (int x, int y) end)
        {
            var openSet   = new SortedSet<Node>(NodeComparer.Instance);
            var gScore    = new Dictionary<(int, int), int>();
            var cameFrom  = new Dictionary<(int, int), (int, int)>();

            gScore[start] = 0;
            openSet.Add(new Node(start, 0, Heuristic(start, end)));

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);

                if (current.Position == end)
                    return ReconstructPath(cameFrom, end);

                foreach (var (dx, dy) in CardinalDirections)
                {
                    var neighbour = (current.Position.x + dx, current.Position.y + dy);
                    if (!map.InBounds(neighbour.Item1, neighbour.Item2)) continue;
                    if (map.Get(neighbour.Item1, neighbour.Item2) == Tile.Wall) continue;

                    int tentativeG = gScore[current.Position] + 1;
                    if (gScore.TryGetValue(neighbour, out int existingG) && tentativeG >= existingG)
                        continue;

                    gScore[neighbour] = tentativeG;
                    cameFrom[neighbour] = current.Position;

                    int f = tentativeG + Heuristic(neighbour, end);
                    openSet.Add(new Node(neighbour, tentativeG, f));
                }
            }

            return new List<(int x, int y)>(); // No path found.
        }

        private static int Heuristic((int x, int y) a, (int x, int y) b)
            => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

        private static List<(int x, int y)> ReconstructPath(
            Dictionary<(int, int), (int, int)> cameFrom, (int x, int y) current)
        {
            var path = new List<(int x, int y)>();
            while (cameFrom.ContainsKey(current))
            {
                path.Add(current);
                current = cameFrom[current];
            }
            path.Add(current);
            path.Reverse();
            return path;
        }

        private class Node
        {
            public (int x, int y) Position { get; }
            public int G { get; }
            public int F { get; }
            // Tie-break id ensures SortedSet treats distinct nodes with equal F as different.
            private static int _idCounter;
            private readonly int _id;

            public Node((int x, int y) position, int g, int f)
            {
                Position = position;
                G = g;
                F = f;
                _id = System.Threading.Interlocked.Increment(ref _idCounter);
            }
        }

        private class NodeComparer : IComparer<Node>
        {
            public static readonly NodeComparer Instance = new NodeComparer();
            public int Compare(Node a, Node b)
            {
                int cmp = a.F.CompareTo(b.F);
                if (cmp != 0) return cmp;
                cmp = a.G.CompareTo(b.G);
                if (cmp != 0) return cmp;
                return a.Position.CompareTo(b.Position);
            }
        }
    }
}
