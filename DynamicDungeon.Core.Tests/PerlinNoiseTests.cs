using DynamicDungeon.Core.Algorithms;
using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;
using Xunit;

namespace DynamicDungeon.Core.Tests
{
    public class PerlinNoiseTests
    {
        private static GenerationParameters Params(BiomeType biome, int seed, int size = 40) =>
            new GenerationParameters
            {
                Algorithm  = AlgorithmType.PerlinNoise,
                Width      = size,
                Height     = size,
                Seed       = seed,
                Biome      = biome,
                Difficulty = DifficultyLevel.Easy,
            };

        [Fact]
        public void Generate_ReturnsMapWithCorrectDimensions()
        {
            var map = new PerlinNoise().Generate(Params(BiomeType.Dungeon, 42));
            Assert.Equal(40, map.Width);
            Assert.Equal(40, map.Height);
        }

        [Theory]
        [InlineData(BiomeType.Dungeon, 1)]
        [InlineData(BiomeType.Cave,    5)]
        [InlineData(BiomeType.Ruins,   3)]
        public void Generate_ExitIsReachableFromSpawn(BiomeType biome, int seed)
        {
            var map      = new PerlinNoise().Generate(Params(biome, seed));
            var reachable = FloodFill.GetReachable(map, map.SpawnPoint.x, map.SpawnPoint.y);
            Assert.Contains(map.ExitPoint, reachable);
        }

        [Fact]
        public void Generate_IsDeterministicForSameSeed()
        {
            var p    = Params(BiomeType.Dungeon, 99);
            var map1 = new PerlinNoise().Generate(p);
            var map2 = new PerlinNoise().Generate(p);

            for (int x = 0; x < map1.Width; x++)
            for (int y = 0; y < map1.Height; y++)
                Assert.Equal(map1.Get(x, y), map2.Get(x, y));
        }

        // This test documents the biome density contract for Perlin Noise:
        // Ruins must produce more open space than Cave because Perlin uses a noise
        // threshold — a tile becomes Floor if the sampled value is below the threshold.
        // Ruins threshold is 0.60 vs Cave's 0.50, so Ruins accepts a larger fraction
        // of the noise range as floor. Ordering: Ruins > Dungeon > Cave in floor tile count.
        // Ruins = collapsed structures with open areas; Cave = dense rock with narrow passages.
        [Fact]
        public void Generate_RuinsBiome_HasMoreFloorTilesThanCaveBiome()
        {
            static int FloorCount(TileMap m)
            {
                int count = 0;
                for (int x = 1; x < m.Width  - 1; x++)
                for (int y = 1; y < m.Height - 1; y++)
                    if (m.Get(x, y) != Tile.Wall) count++;
                return count;
            }

            // Average over several seeds so single-run variance does not skew the result.
            int ruinsTotal = 0, caveTotal = 0;
            for (int s = 1; s <= 5; s++)
            {
                ruinsTotal += FloorCount(new PerlinNoise().Generate(Params(BiomeType.Ruins, s * 13, 50)));
                caveTotal  += FloorCount(new PerlinNoise().Generate(Params(BiomeType.Cave,  s * 13, 50)));
            }

            Assert.True(ruinsTotal > caveTotal,
                $"Ruins avg floor ({ruinsTotal / 5}) should exceed Cave avg floor ({caveTotal / 5})");
        }
    }
}
