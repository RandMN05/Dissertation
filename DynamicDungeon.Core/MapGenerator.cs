using System;
using DynamicDungeon.Core.Algorithms;
using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;
using DynamicDungeon.Core.Validation;

namespace DynamicDungeon.Core
{
    // Primary public entry point for the library.
    // Selects an algorithm, generates a map, validates it with flood fill,
    // regenerates if invalid, and optionally computes the A* shortest path.
    public class MapGenerator
    {
        public TileMap Generate(GenerationParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            IMapAlgorithm algorithm = parameters.Algorithm switch
            {
                AlgorithmType.PerlinNoise          => new PerlinNoise(),
                AlgorithmType.WaveFunctionCollapse => new WaveFunctionCollapse(),
                _                                  => new CellularAutomata()
            };

            for (int attempt = 0; attempt < parameters.MaxRegenerationAttempts; attempt++)
            {
                // Offset seed each attempt so we don't regenerate an identical invalid map.
                var attemptParams = attempt == 0
                    ? parameters
                    : new GenerationParameters
                    {
                        Width                   = parameters.Width,
                        Height                  = parameters.Height,
                        Seed                    = parameters.Seed + attempt,
                        Biome                   = parameters.Biome,
                        Difficulty              = parameters.Difficulty,
                        Algorithm               = parameters.Algorithm,
                        ComputeShortestPath     = parameters.ComputeShortestPath,
                        MaxRegenerationAttempts = parameters.MaxRegenerationAttempts
                    };

                var map = algorithm.Generate(attemptParams);

                if (!MapValidator.IsValid(map)) continue;

                if (parameters.ComputeShortestPath)
                    map.ShortestPath = AStar.FindPath(map, map.SpawnPoint, map.ExitPoint);

                return map;
            }

            throw new InvalidOperationException(
                $"Could not generate a valid map after {parameters.MaxRegenerationAttempts} attempts. " +
                "Try adjusting Width, Height, or Biome parameters.");
        }
    }
}
