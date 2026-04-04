using System;
using System.Diagnostics;
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
        /// <summary>
        /// Generates a map. Throws <see cref="InvalidOperationException"/> if all attempts fail.
        /// </summary>
        public TileMap Generate(GenerationParameters parameters)
            => GenerateWithReport(parameters, out _);

        /// <summary>
        /// Generates a map and also returns a <see cref="GenerationReport"/> with timing,
        /// per-attempt failure reasons, and retry counts.
        /// Throws <see cref="InvalidOperationException"/> if all attempts fail (report is still populated).
        /// </summary>
        public TileMap GenerateWithReport(GenerationParameters parameters, out GenerationReport report)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            report = new GenerationReport();
            var sw = Stopwatch.StartNew();

            IMapAlgorithm algorithm = parameters.Algorithm switch
            {
                AlgorithmType.PerlinNoise          => new PerlinNoise(),
                AlgorithmType.WaveFunctionCollapse => new WaveFunctionCollapse(),
                _                                  => new CellularAutomata()
            };

            for (int attempt = 0; attempt < parameters.MaxRegenerationAttempts; attempt++)
            {
                // Offset seed each attempt so we don't regenerate an identical invalid map.
                int attemptSeed = parameters.Seed + attempt;
                var attemptParams = attempt == 0
                    ? parameters
                    : new GenerationParameters
                    {
                        Width                   = parameters.Width,
                        Height                  = parameters.Height,
                        Seed                    = attemptSeed,
                        Biome                   = parameters.Biome,
                        Difficulty              = parameters.Difficulty,
                        Algorithm               = parameters.Algorithm,
                        ComputeShortestPath     = parameters.ComputeShortestPath,
                        MaxRegenerationAttempts = parameters.MaxRegenerationAttempts
                    };

                var map = algorithm.Generate(attemptParams);

                if (!MapValidator.Validate(map, out string failureReason))
                {
                    report.AttemptFailureReasons.Add($"Attempt {attempt + 1} (seed {attemptParams.Seed}): {failureReason}");
                    report.FailedAttempts++;
                    continue;
                }

                if (parameters.ComputeShortestPath)
                    map.ShortestPath = AStar.FindPath(map, map.SpawnPoint, map.ExitPoint);

                sw.Stop();
                report.TotalMs            = sw.ElapsedMilliseconds;
                report.SuccessfulAttempt  = attempt + 1;
                report.SuccessfulSeed     = attemptParams.Seed;
                return map;
            }

            sw.Stop();
            report.TotalMs = sw.ElapsedMilliseconds;

            throw new InvalidOperationException(
                $"Could not generate a valid map after {parameters.MaxRegenerationAttempts} attempts. " +
                "Try adjusting Width, Height, or Biome parameters.");
        }
    }
}
