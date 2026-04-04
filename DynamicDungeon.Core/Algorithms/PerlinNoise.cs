using System;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Algorithms
{
    // Generates terrain-style maps by sampling 2D Perlin noise and thresholding
    // the result into Wall / Floor tiles.
    // This is a self-contained implementation — no external dependencies.
    public class PerlinNoise : IMapAlgorithm
    {
        // Biome tuning: noise threshold (0-1) below which a cell becomes Floor,
        // and the noise scale controlling terrain feature size.
        // Higher threshold = more floor tiles = better connectivity.
        // Smaller scale = larger, smoother regions = easier to connect.
        private static readonly (double threshold, double scale) DungeonSettings = (0.55, 2.0);
        private static readonly (double threshold, double scale) CaveSettings    = (0.60, 1.5);
        private static readonly (double threshold, double scale) RuinsSettings   = (0.50, 3.5);

        private static readonly int[] EnemyCountByDifficulty = { 3, 6, 12 };

        public TileMap Generate(GenerationParameters p)
        {
            var (threshold, scale) = p.Biome switch
            {
                BiomeType.Cave  => CaveSettings,
                BiomeType.Ruins => RuinsSettings,
                _               => DungeonSettings
            };

            var rng = new Random(p.Seed);
            var noise = new PerlinSampler(p.Seed);
            var map = new TileMap(p.Width, p.Height);

            double offsetX = rng.NextDouble() * 1000;
            double offsetY = rng.NextDouble() * 1000;

            for (int x = 0; x < p.Width; x++)
            for (int y = 0; y < p.Height; y++)
            {
                if (x == 0 || x == p.Width - 1 || y == 0 || y == p.Height - 1)
                {
                    map.Set(x, y, Tile.Wall);
                    continue;
                }

                double nx = (x / (double)p.Width)  * scale + offsetX;
                double ny = (y / (double)p.Height) * scale + offsetY;

                // Layered octaves for more natural-looking terrain.
                double value = noise.Sample(nx, ny) * 0.6
                             + noise.Sample(nx * 2, ny * 2) * 0.3
                             + noise.Sample(nx * 4, ny * 4) * 0.1;

                map.Set(x, y, value < threshold ? Tile.Floor : Tile.Wall);
            }

            PlaceSpawnAndExit(map);
            PlaceEnemies(map, rng, EnemyCountByDifficulty[(int)p.Difficulty]);

            return map;
        }

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
            return bottomLeft ? (1, 1) : (map.Width - 2, map.Height - 2);
        }

        private static void PlaceEnemies(TileMap map, Random rng, int count)
        {
            int placed = 0, attempts = 0;
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

        // Ken Perlin's improved noise algorithm, adapted for seeded 2D sampling.
        // Reference: https://cs.nyu.edu/~perlin/noise/
        private class PerlinSampler
        {
            private readonly int[] _p = new int[512];

            public PerlinSampler(int seed)
            {
                var rng = new Random(seed);
                var permutation = new int[256];
                for (int i = 0; i < 256; i++) permutation[i] = i;

                // Fisher-Yates shuffle seeded for reproducibility.
                for (int i = 255; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
                }

                for (int i = 0; i < 512; i++)
                    _p[i] = permutation[i & 255];
            }

            public double Sample(double x, double y)
            {
                int xi = (int)Math.Floor(x) & 255;
                int yi = (int)Math.Floor(y) & 255;
                double xf = x - Math.Floor(x);
                double yf = y - Math.Floor(y);

                double u = Fade(xf);
                double v = Fade(yf);

                int aa = _p[_p[xi]     + yi];
                int ab = _p[_p[xi]     + yi + 1];
                int ba = _p[_p[xi + 1] + yi];
                int bb = _p[_p[xi + 1] + yi + 1];

                double x1 = Lerp(Grad(aa, xf,     yf),     Grad(ba, xf - 1, yf),     u);
                double x2 = Lerp(Grad(ab, xf,     yf - 1), Grad(bb, xf - 1, yf - 1), u);
                // Remap from [-1,1] to [0,1].
                return (Lerp(x1, x2, v) + 1) / 2.0;
            }

            private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
            private static double Lerp(double a, double b, double t) => a + t * (b - a);

            private static double Grad(int hash, double x, double y)
            {
                // Use lowest 2 bits to pick one of 4 gradient directions.
                return (hash & 3) switch
                {
                    0 =>  x + y,
                    1 => -x + y,
                    2 =>  x - y,
                    _ => -x - y
                };
            }
        }
    }
}
