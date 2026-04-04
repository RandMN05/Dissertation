using System;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Algorithms
{
    // Generates organic cave/dungeon maps using Moore neighbourhood threshold smoothing.
    // Each cell is a Wall or Floor; after N smoothing passes the map converges to
    // connected cave-like shapes.
    public class CellularAutomata : IMapAlgorithm
    {
        // Biome-specific tuning: initial fill probability (0-100) and smoothing iterations.
        // Higher fill % = more walls = tighter corridors.
        private static readonly (int fillPercent, int iterations) DungeonSettings = (45, 5);
        private static readonly (int fillPercent, int iterations) CaveSettings    = (55, 4);
        private static readonly (int fillPercent, int iterations) RuinsSettings   = (40, 6);

        // Difficulty scales enemy count (Enemy tiles replace some Floor tiles post-generation).
        private static readonly int[] EnemyCountByDifficulty = { 3, 6, 12 }; // Easy/Medium/Hard

        public TileMap Generate(GenerationParameters p)
        {
            var (fillPercent, iterations) = p.Biome switch
            {
                BiomeType.Cave   => CaveSettings,
                BiomeType.Ruins  => RuinsSettings,
                _                => DungeonSettings
            };

            var rng = new Random(p.Seed);
            var map = new TileMap(p.Width, p.Height);

            RandomFill(map, rng, fillPercent);

            for (int i = 0; i < iterations; i++)
                Smooth(map);

            PlaceSpawnAndExit(map);
            PlaceEnemies(map, rng, EnemyCountByDifficulty[(int)p.Difficulty]);

            return map;
        }

        private static void RandomFill(TileMap map, Random rng, int fillPercent)
        {
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                // Border is always wall to keep maps enclosed.
                if (x == 0 || x == map.Width - 1 || y == 0 || y == map.Height - 1)
                    map.Set(x, y, Tile.Wall);
                else
                    map.Set(x, y, rng.Next(0, 100) < fillPercent ? Tile.Wall : Tile.Floor);
            }
        }

        private static void Smooth(TileMap map)
        {
            var next = new Tile[map.Width, map.Height];

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                int wallNeighbours = CountWallNeighbours(map, x, y);

                // Threshold smoothing rule: become wall if >=5 wall neighbours,
                // become floor if <=2 wall neighbours, otherwise keep current state.
                if (wallNeighbours > 4)
                    next[x, y] = Tile.Wall;
                else if (wallNeighbours < 3)
                    next[x, y] = Tile.Floor;
                else
                    next[x, y] = map.Get(x, y);
            }

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                map.Set(x, y, next[x, y]);
        }

        private static int CountWallNeighbours(TileMap map, int cx, int cy)
        {
            int count = 0;
            for (int nx = cx - 1; nx <= cx + 1; nx++)
            for (int ny = cy - 1; ny <= cy + 1; ny++)
            {
                if (nx == cx && ny == cy) continue;
                // Out-of-bounds counts as wall to keep borders solid.
                if (!map.InBounds(nx, ny) || map.Get(nx, ny) == Tile.Wall)
                    count++;
            }
            return count;
        }

        // Spawn at first floor tile scanning from bottom-left,
        // Exit at first floor tile scanning from top-right.
        private static void PlaceSpawnAndExit(TileMap map)
        {
            map.SpawnPoint = FindFloorTile(map, bottomLeft: true);
            map.ExitPoint  = FindFloorTile(map, bottomLeft: false);

            map.Set(map.SpawnPoint.x, map.SpawnPoint.y, Tile.Spawn);
            map.Set(map.ExitPoint.x,  map.ExitPoint.y,  Tile.Exit);
        }

        private static (int x, int y) FindFloorTile(TileMap map, bool bottomLeft)
        {
            if (bottomLeft)
            {
                for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width  - 1; x++)
                    if (map.Get(x, y) == Tile.Floor) return (x, y);
            }
            else
            {
                for (int y = map.Height - 2; y >= 1; y--)
                for (int x = map.Width  - 2; x >= 1; x--)
                    if (map.Get(x, y) == Tile.Floor) return (x, y);
            }

            // Fallback: guaranteed positions (might be overwritten if all walls, triggers regen).
            return bottomLeft ? (1, 1) : (map.Width - 2, map.Height - 2);
        }

        private static void PlaceEnemies(TileMap map, Random rng, int count)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < count && attempts < count * 20)
            {
                attempts++;
                int x = rng.Next(1, map.Width  - 1);
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
