using UnityEngine;

namespace DynamicDungeon.Unity
{
    /// <summary>
    /// An ordered list of DungeonLevelData assets that represents a full game's
    /// level sequence. Create and populate this via the Level Designer window,
    /// then drag the asset into your game's bootstrapper or level manager.
    ///
    /// Example runtime usage:
    ///   [SerializeField] LevelCollection levels;
    ///   dungeonGenerator.LoadFrom(levels.Levels[currentLevel]);
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCollection", menuName = "DynamicDungeon/Level Collection")]
    public class LevelCollection : ScriptableObject
    {
        public DungeonLevelData[] Levels = new DungeonLevelData[0];

        public int Count => Levels.Length;

        public bool IsValidIndex(int index) => index >= 0 && index < Levels.Length;
    }
}
