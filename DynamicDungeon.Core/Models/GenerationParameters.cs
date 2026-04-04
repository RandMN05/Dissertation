namespace DynamicDungeon.Core.Models
{
    public class GenerationParameters
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 50;
        public int Seed { get; set; } = 0;
        public BiomeType Biome { get; set; } = BiomeType.Dungeon;
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
        public AlgorithmType Algorithm { get; set; } = AlgorithmType.CellularAutomata;
        public bool ComputeShortestPath { get; set; } = false;
        public int MaxRegenerationAttempts { get; set; } = 10;
    }
}
