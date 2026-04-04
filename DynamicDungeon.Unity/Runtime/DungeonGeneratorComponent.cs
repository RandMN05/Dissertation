using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DynamicDungeon.Core;
using DynamicDungeon.Core.Models;
using CoreTile = DynamicDungeon.Core.Models.Tile;

namespace DynamicDungeon.Unity
{
    /// <summary>
    /// MonoBehaviour that generates a procedural dungeon map and renders it
    /// onto an attached Tilemap. Assign tile sprites in the Inspector, then
    /// click Generate Dungeon (or call Generate() from code).
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    [RequireComponent(typeof(TilemapRenderer))]
    public class DungeonGeneratorComponent : MonoBehaviour
    {
        // ── Generation Parameters ──────────────────────────────────────────
        [Header("Generation Parameters")]
        public AlgorithmType Algorithm = AlgorithmType.CellularAutomata;
        public BiomeType     Biome     = BiomeType.Dungeon;
        public DifficultyLevel Difficulty = DifficultyLevel.Medium;

        [Range(10, 200)] public int Width  = 50;
        [Range(10, 200)] public int Height = 50;

        [Tooltip("Set to 0 for a random seed each time.")]
        public int Seed = 0;

        [Tooltip("When enabled, computes and stores the A* shortest path from Spawn to Exit.")]
        public bool ComputeShortestPath = false;

        [Range(1, 20)] public int MaxRegenerationAttempts = 10;

        // ── Tile Assignments ───────────────────────────────────────────────
        [Header("Tile Sprites")]
        [Tooltip("Drag a Unity Tile asset for each map tile type.")]
        public TileBase WallTile;
        public TileBase FloorTile;
        public TileBase SpawnTile;
        public TileBase ExitTile;
        public TileBase EnemyTile;

        // ── Read-only output ───────────────────────────────────────────────
        [Header("Last Generation Info (read-only)")]
        [SerializeField, HideInInspector] private Vector3Int _spawnCell;
        [SerializeField, HideInInspector] private Vector3Int _exitCell;
        [SerializeField, HideInInspector] private List<Vector3Int> _enemyCells = new();
        [SerializeField, HideInInspector] private List<Vector3Int> _pathCells  = new();
        [SerializeField, HideInInspector] private long _lastGenerationMs;
        [SerializeField, HideInInspector] private int  _lastSeedUsed;

        /// <summary>World position of the player Spawn tile.</summary>
        public Vector3Int SpawnCell  => _spawnCell;
        /// <summary>World position of the Exit tile.</summary>
        public Vector3Int ExitCell   => _exitCell;
        /// <summary>All cells containing enemy spawn markers.</summary>
        public IReadOnlyList<Vector3Int> EnemyCells => _enemyCells;
        /// <summary>A* shortest path cells from Spawn to Exit (empty if ComputeShortestPath is false).</summary>
        public IReadOnlyList<Vector3Int> PathCells  => _pathCells;
        /// <summary>How long the last generation took in milliseconds.</summary>
        public long LastGenerationMs => _lastGenerationMs;
        /// <summary>The seed that was actually used (useful when Seed was 0 / random).</summary>
        public int LastSeedUsed => _lastSeedUsed;

        private Tilemap _tilemap;

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Generates a new dungeon map and renders it onto the attached Tilemap.
        /// Safe to call at runtime or from the Editor.
        /// </summary>
        public void Generate()
        {
            _tilemap = GetComponent<Tilemap>();

            int seed = Seed == 0 ? Random.Range(1, int.MaxValue) : Seed;
            _lastSeedUsed = seed;

            var parameters = new GenerationParameters
            {
                Width                   = Width,
                Height                  = Height,
                Seed                    = seed,
                Biome                   = Biome,
                Difficulty              = Difficulty,
                Algorithm               = Algorithm,
                ComputeShortestPath     = ComputeShortestPath,
                MaxRegenerationAttempts = MaxRegenerationAttempts
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var map = new MapGenerator().Generate(parameters);
            sw.Stop();
            _lastGenerationMs = sw.ElapsedMilliseconds;

            ApplyToTilemap(map);
        }

        // ── Private helpers ────────────────────────────────────────────────

        private void ApplyToTilemap(TileMap map)
        {
            _tilemap.ClearAllTiles();
            _enemyCells.Clear();
            _pathCells.Clear();

            // Build a fast lookup for path cells so we can colour them later.
            var pathSet = new HashSet<(int, int)>(map.ShortestPath);

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var coreTile  = map.Get(x, y);
                var unityTile = MapToUnityTile(coreTile);
                if (unityTile == null) continue;

                var cell = new Vector3Int(x, y, 0);
                _tilemap.SetTile(cell, unityTile);

                if (coreTile == CoreTile.Enemy)
                    _enemyCells.Add(cell);

                if (pathSet.Contains((x, y)))
                    _pathCells.Add(cell);
            }

            _spawnCell = new Vector3Int(map.SpawnPoint.x, map.SpawnPoint.y, 0);
            _exitCell  = new Vector3Int(map.ExitPoint.x,  map.ExitPoint.y,  0);

            // Centre the camera on the map (runtime only).
            if (Application.isPlaying)
                CentreCamera();
        }

        private TileBase MapToUnityTile(CoreTile tile) => tile switch
        {
            CoreTile.Wall  => WallTile,
            CoreTile.Floor => FloorTile,
            CoreTile.Spawn => SpawnTile,
            CoreTile.Exit  => ExitTile,
            CoreTile.Enemy => EnemyTile,
            _              => null
        };

        private void CentreCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(Width / 2f, Height / 2f, cam.transform.position.z);
        }
    }
}
