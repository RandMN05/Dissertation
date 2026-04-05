using System;
using System.Collections.Generic;
using System.Linq;
using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;

namespace DynamicDungeon.Core.Algorithms
{
    // Wave Function Collapse: each cell starts in superposition over three internal tile types.
    //
    // Tile type hierarchy:
    //   Floor        — open walkable space
    //   Wall         — thin border tile; legal transition between open and solid regions
    //   WallInterior — solid wall mass; can only be adjacent to Wall or WallInterior
    //
    // Directional adjacency rules (symmetric — no contradiction can arise):
    //   Floor[*]        = {Floor, Wall}
    //   Wall[*]         = {Floor, Wall, WallInterior}
    //   WallInterior[*] = {Wall, WallInterior}
    //
    // Constraint propagation (AC-3) cascades these rules across the grid.
    // Collapsing a WallInterior cell immediately forces its neighbours to Wall-or-WallInterior,
    // which propagates outward — creating the solid block structure that distinguishes WFC
    // from the organic noise of cellular automata.
    //
    // Connectivity: the 50-attempt retry loop discards maps where Spawn and Exit are not
    // flood-fill connected. At 45–60% Floor weight, natural connectivity is common enough
    // that the loop reliably finds a valid seed.
    public class WaveFunctionCollapse : IMapAlgorithm
    {
        private static readonly int[] EnemyCountByDifficulty = { 3, 6, 12 };

        // Cardinal direction offsets. Index 0=(0,+1), 1=(0,-1), 2=(+1,0), 3=(-1,0).
        private static readonly (int dx, int dy)[] Directions =
            { (0, 1), (0, -1), (1, 0), (-1, 0) };

        private enum WfcTile { Floor, Wall, WallInterior }

        // Directional adjacency rules. Array index matches Directions[]: 0=+Y, 1=-Y, 2=+X, 3=-X.
        // All rules are mutually symmetric — if A allows B in direction d, B allows A in the
        // reverse direction. Symmetric rules guarantee no WFC contradiction can occur
        // (Wall is always legal in every cell).
        private static Dictionary<WfcTile, HashSet<WfcTile>[]> BuildRules() =>
            new Dictionary<WfcTile, HashSet<WfcTile>[]>
            {
                [WfcTile.Floor] = new[]
                {
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall }, // +Y
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall }, // -Y
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall }, // +X
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall }, // -X
                },
                [WfcTile.Wall] = new[]
                {
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior }, // +Y
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior }, // -Y
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior }, // +X
                    new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior }, // -X
                },
                [WfcTile.WallInterior] = new[]
                {
                    new HashSet<WfcTile> { WfcTile.Wall, WfcTile.WallInterior }, // +Y
                    new HashSet<WfcTile> { WfcTile.Wall, WfcTile.WallInterior }, // -Y
                    new HashSet<WfcTile> { WfcTile.Wall, WfcTile.WallInterior }, // +X
                    new HashSet<WfcTile> { WfcTile.Wall, WfcTile.WallInterior }, // -X
                },
            };

        // Per-biome tile weights (integer percentages; only ratios matter).
        //
        //   Dungeon (45% open): Floor and WallInterior compete at near-equal weight (~0.9:1),
        //     producing large irregular open areas separated by equally large solid wall masses.
        //   Cave    (60% open): Floor-dominant (~2:1 over WallInterior) — spacious open areas
        //     with thinner wall masses.
        //   Ruins   (52% open): balanced — moderate wall masses and open courtyards.
        private static (int floor, int wall, int wallInterior) GetWeights(BiomeType biome) =>
            biome switch
            {
                BiomeType.Cave => (40, 8, 52),
                BiomeType.Ruins => (34, 7, 59),
                _ => (28, 5, 67),
            };

        private static int TileWeight(WfcTile t, (int floor, int wall, int wallInterior) w) =>
            t switch
            {
                WfcTile.Floor => w.floor,
                WfcTile.Wall => w.wall,
                WfcTile.WallInterior => w.wallInterior,
                _ => 0,
            };

        public TileMap Generate(GenerationParameters p)
        {
            var rules = BuildRules();
            var weights = GetWeights(p.Biome);
            var pool = new List<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior };

            for (int attempt = 0; attempt < 50; attempt++)
            {
                var result = TryGenerate(p, rules, pool, weights, p.Seed + attempt);
                if (result != null) return result;
            }

            // Fallback: delegate to Cellular Automata if WFC can't produce a connected map.
            return new CellularAutomata().Generate(p);
        }

        private static TileMap? TryGenerate(
            GenerationParameters p,
            Dictionary<WfcTile, HashSet<WfcTile>[]> rules,
            List<WfcTile> pool,
            (int floor, int wall, int wallInterior) weights,
            int seed)
        {
            var rng = new Random(seed);
            int w = p.Width, h = p.Height;

            // Interior cells start with all three candidates in superposition.
            // Border cells are pre-collapsed to Wall to keep maps enclosed.
            var superposition = new HashSet<WfcTile>[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    superposition[x, y] = (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        ? new HashSet<WfcTile> { WfcTile.Wall }
                        : new HashSet<WfcTile>(pool);
                }

            // Pre-seed the two corner anchor cells to Floor before the main collapse loop.
            // This forces WFC's propagation to naturally bias the map toward connectivity —
            // the two anchors are as far apart as possible, so the algorithm must keep a
            // Floor-reachable path between them rather than letting wall masses bisect the map.
            // PlaceSpawnAndExit's corner-scanning search will find these exact cells first.
            int spawnX = 1, spawnY = 1;
            int exitX = w - 2, exitY = h - 2;
            superposition[spawnX, spawnY] = new HashSet<WfcTile> { WfcTile.Floor };
            superposition[exitX, exitY] = new HashSet<WfcTile> { WfcTile.Floor };
            if (!Propagate(superposition, rules, w, h, spawnX, spawnY)) return null;
            if (!Propagate(superposition, rules, w, h, exitX, exitY)) return null;

            // Observe → Collapse → Propagate loop.
            while (true)
            {
                var cell = PickLowestEntropy(superposition, w, h, weights, rng);
                if (cell == null) break; // All cells collapsed.

                var (cx, cy) = cell.Value;
                var options = superposition[cx, cy].ToList();
                if (options.Count == 0) return null; // Contradiction.

                var chosen = PickWeighted(options, weights, rng);
                superposition[cx, cy] = new HashSet<WfcTile> { chosen };

                if (!Propagate(superposition, rules, w, h, cx, cy))
                    return null; // Propagation reached a contradiction.
            }

            // Map WfcTile → Tile.
            var map = new TileMap(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    map.Set(x, y,
                        superposition[x, y].First() == WfcTile.Floor
                            ? Tile.Floor
                            : Tile.Wall);
                }

            PlaceSpawnAndExit(map);

            // Discard disconnected maps and retry with a different seed.
            var reachable = FloodFill.GetReachable(map, map.SpawnPoint.x, map.SpawnPoint.y);
            if (!reachable.Contains(map.ExitPoint))
                return null;

            PlaceEnemies(map, rng, EnemyCountByDifficulty[(int)p.Difficulty]);

            return map;
        }

        // Selects the uncollapsed cell with the lowest Shannon entropy H = -Σ p_i · ln(p_i).
        // Collapsing lowest-entropy cells first minimises the chance that a forced constraint
        // propagates back to invalidate earlier decisions.
        private static (int x, int y)? PickLowestEntropy(
            HashSet<WfcTile>[,] sp, int w, int h,
            (int floor, int wall, int wallInterior) weights,
            Random rng)
        {
            double minEntropy = double.MaxValue;
            var candidates = new List<(int x, int y)>();

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (sp[x, y].Count <= 1) continue; // Already collapsed.

                    double entropy = ShannonEntropy(sp[x, y], weights);
                    if (entropy < minEntropy - 1e-9)
                    {
                        minEntropy = entropy;
                        candidates.Clear();
                    }
                    if (Math.Abs(entropy - minEntropy) < 1e-9)
                        candidates.Add((x, y));
                }

            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
        }

        // Shannon entropy H = -Σ p_i · ln(p_i), where p_i = weight(t_i) / Σ weights.
        private static double ShannonEntropy(
            HashSet<WfcTile> candidates,
            (int floor, int wall, int wallInterior) weights)
        {
            int total = 0;
            foreach (var t in candidates)
                total += TileWeight(t, weights);
            if (total == 0) return 0.0;

            double h = 0;
            foreach (var t in candidates)
            {
                double p = TileWeight(t, weights) / (double)total;
                if (p > 0) h -= p * Math.Log(p);
            }
            return h;
        }

        // Weighted random tile selection. Respects biome frequency targets.
        private static WfcTile PickWeighted(
            List<WfcTile> options,
            (int floor, int wall, int wallInterior) weights,
            Random rng)
        {
            int total = options.Sum(t => TileWeight(t, weights));
            if (total == 0) return options[rng.Next(options.Count)];

            int roll = rng.Next(total);
            int cum = 0;
            foreach (var t in options)
            {
                cum += TileWeight(t, weights);
                if (roll < cum) return t;
            }
            return options[options.Count - 1];
        }

        // AC-3-style constraint propagation using directional rules.
        // Direction index d matches Directions[d]: 0=(0,+1), 1=(0,-1), 2=(+1,0), 3=(-1,0).
        //
        // For each neighbour (nx,ny) of (ox,oy) reached via direction d:
        //   allowed = union of rules[t][d] for each remaining candidate t at (ox,oy)
        //   sp[nx,ny] ∩= allowed
        //
        // If sp[nx,ny] shrank, push it onto the stack so its own neighbours are re-evaluated.
        // Returns false immediately on contradiction (empty candidate set).
        private static bool Propagate(
            HashSet<WfcTile>[,] sp,
            Dictionary<WfcTile, HashSet<WfcTile>[]> rules,
            int w, int h, int cx, int cy)
        {
            var stack = new Stack<(int x, int y)>();
            stack.Push((cx, cy));

            while (stack.Count > 0)
            {
                var (ox, oy) = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    var (dx, dy) = Directions[d];
                    int nx = ox + dx, ny = oy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    var allowed = new HashSet<WfcTile>();
                    foreach (var t in sp[ox, oy])
                        if (rules.TryGetValue(t, out var dirRules))
                            allowed.UnionWith(dirRules[d]);

                    int before = sp[nx, ny].Count;
                    sp[nx, ny].IntersectWith(allowed);

                    if (sp[nx, ny].Count == 0) return false; // Contradiction.
                    if (sp[nx, ny].Count < before)
                        stack.Push((nx, ny));
                }
            }
            return true;
        }

        private static void PlaceSpawnAndExit(TileMap map)
        {
            map.SpawnPoint = FindFloorTile(map, bottomLeft: true);
            map.ExitPoint = FindFloorTile(map, bottomLeft: false);
            map.Set(map.SpawnPoint.x, map.SpawnPoint.y, Tile.Spawn);
            map.Set(map.ExitPoint.x, map.ExitPoint.y, Tile.Exit);
        }

        private static (int x, int y) FindFloorTile(TileMap map, bool bottomLeft)
        {
            if (bottomLeft)
            {
                for (int y = 1; y < map.Height - 1; y++)
                    for (int x = 1; x < map.Width - 1; x++)
                        if (map.Get(x, y) == Tile.Floor) return (x, y);
            }
            else
            {
                for (int y = map.Height - 2; y >= 1; y--)
                    for (int x = map.Width - 2; x >= 1; x--)
                        if (map.Get(x, y) == Tile.Floor) return (x, y);
            }
            return bottomLeft ? (1, 1) : (map.Width - 2, map.Height - 2);
        }

        private static void PlaceEnemies(TileMap map, Random rng, int count)
        {
            int placed = 0, attempts = 0;
            while (placed < count && attempts < count * 20)
            {
                attempts++;
                int x = rng.Next(1, map.Width - 1);
                int y = rng.Next(1, map.Height - 1);
                if (map.Get(x, y) == Tile.Floor)
                {
                    map.Set(x, y, Tile.Enemy);
                    placed++;
                }
            }
        }
    }
}
