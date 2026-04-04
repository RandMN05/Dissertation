using UnityEngine;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Unity
{
    /// <summary>
    /// Stores the exact parameters needed to reproduce one generated level.
    /// Seed is always the actual value used during generation — never 0 — so
    /// the same map is guaranteed every time this level is loaded at runtime.
    ///
    /// Create via the DynamicDungeon Level Designer window:
    ///   Window → DynamicDungeon → Level Designer
    /// </summary>
    [CreateAssetMenu(fileName = "Level_01", menuName = "DynamicDungeon/Level Data")]
    public class DungeonLevelData : ScriptableObject
    {
        public string         LevelName  = "Level";
        public AlgorithmType  Algorithm  = AlgorithmType.CellularAutomata;
        public BiomeType      Biome      = BiomeType.Dungeon;
        public DifficultyLevel Difficulty = DifficultyLevel.Medium;
        public int            Width      = 50;
        public int            Height     = 50;

        /// <summary>
        /// The exact seed used when the level was generated in the Level Designer.
        /// Passing this back to the generator reproduces the identical map.
        /// </summary>
        public int Seed;
    }
}
