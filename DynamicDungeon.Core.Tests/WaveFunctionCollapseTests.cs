using System.Collections.Generic;
using DynamicDungeon.Core.Algorithms;
using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;
using Xunit;

namespace DynamicDungeon.Core.Tests
{
    public class WaveFunctionCollapseTests
    {
        private static GenerationParameters Params(BiomeType biome, int seed, int size = 40) =>
            new GenerationParameters
            {
                Algorithm  = AlgorithmType.WaveFunctionCollapse,
                Width      = size,
                Height     = size,
                Seed       = seed,
                Biome      = biome,
                Difficulty = DifficultyLevel.Easy,
            };

        [Fact]
        public void Generate_ReturnsMapWithCorrectDimensions()
        {
            var map = new WaveFunctionCollapse().Generate(Params(BiomeType.Dungeon, 42));
            Assert.Equal(40, map.Width);
            Assert.Equal(40, map.Height);
        }

        [Theory]
        [InlineData(BiomeType.Dungeon, 1)]
        [InlineData(BiomeType.Cave,    2)]
        [InlineData(BiomeType.Ruins,   3)]
        public void Generate_ExitIsReachableFromSpawn(BiomeType biome, int seed)
        {
            var map      = new WaveFunctionCollapse().Generate(Params(biome, seed));
            var reachable = FloodFill.GetReachable(map, map.SpawnPoint.x, map.SpawnPoint.y);
            Assert.Contains(map.ExitPoint, reachable);
        }

        [Fact]
        public void Generate_IsDeterministicForSameSeed()
        {
            var p    = Params(BiomeType.Dungeon, 99);
            var map1 = new WaveFunctionCollapse().Generate(p);
            var map2 = new WaveFunctionCollapse().Generate(p);

            for (int x = 0; x < map1.Width; x++)
            for (int y = 0; y < map1.Height; y++)
                Assert.Equal(map1.Get(x, y), map2.Get(x, y));
        }

        // This test documents the biome density contract: Cave must produce more open
        // space than Dungeon because Cave tiles carry 55% floor weight vs Dungeon's 35%.
        // It FAILS before the fix (current WFC ignores biome in its random selection).
        [Fact]
        public void Generate_CaveBiome_HasMoreFloorTilesThanDungeonBiome()
        {
            static int FloorCount(TileMap m)
            {
                int count = 0;
                for (int x = 1; x < m.Width  - 1; x++)
                for (int y = 1; y < m.Height - 1; y++)
                    if (m.Get(x, y) != Tile.Wall) count++;
                return count;
            }

            // Average over several seeds so EnsureConnectivity carving doesn't skew a single run.
            int caveTotal = 0, dungeonTotal = 0;
            for (int s = 1; s <= 5; s++)
            {
                caveTotal    += FloorCount(new WaveFunctionCollapse().Generate(Params(BiomeType.Cave,    s * 13, 50)));
                dungeonTotal += FloorCount(new WaveFunctionCollapse().Generate(Params(BiomeType.Dungeon, s * 13, 50)));
            }

            Assert.True(caveTotal > dungeonTotal,
                $"Cave avg floor ({caveTotal / 5}) should exceed Dungeon avg floor ({dungeonTotal / 5})");
        }
    }
}
