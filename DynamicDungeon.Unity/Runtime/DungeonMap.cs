using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DynamicDungeon.Unity
{
    /// <summary>
    /// Snapshot of a generated dungeon map delivered to subscribers of
    /// DungeonGeneratorComponent.OnMapGenerated. Fully self-contained —
    /// store it and query it without holding a reference to the generator.
    /// </summary>
    public class DungeonMap
    {
        // ── Tilemap cells ──────────────────────────────────────────────────────
        /// <summary>Tilemap cell of the player spawn point.</summary>
        public Vector3Int SpawnCell { get; }
        /// <summary>Tilemap cell of the exit.</summary>
        public Vector3Int ExitCell { get; }
        /// <summary>All enemy spawn cells.</summary>
        public IReadOnlyList<Vector3Int> EnemyCells { get; }
        /// <summary>All walkable floor cells (excludes spawn, exit, enemy markers).</summary>
        public IReadOnlyList<Vector3Int> FloorCells { get; }
        /// <summary>All wall cells.</summary>
        public IReadOnlyList<Vector3Int> WallCells { get; }
        /// <summary>A* path cells from spawn to exit. Empty if ComputeShortestPath was false.</summary>
        public IReadOnlyList<Vector3Int> PathCells { get; }

        // ── World positions ────────────────────────────────────────────────────
        /// <summary>World position of the spawn point (cell centre).</summary>
        public Vector3 SpawnWorld { get; }
        /// <summary>World position of the exit (cell centre).</summary>
        public Vector3 ExitWorld { get; }
        /// <summary>World position of every enemy cell (cell centres).</summary>
        public IReadOnlyList<Vector3> EnemyWorldPositions { get; }
        /// <summary>World position of every floor cell (cell centres).</summary>
        public IReadOnlyList<Vector3> FloorWorldPositions { get; }

        // ── Map info ───────────────────────────────────────────────────────────
        /// <summary>The live Tilemap this map was rendered onto.</summary>
        public Tilemap Tilemap { get; }
        /// <summary>Map width in tiles.</summary>
        public int Width { get; }
        /// <summary>Map height in tiles.</summary>
        public int Height { get; }
        /// <summary>The seed used to generate this map.</summary>
        public int Seed { get; }

        // ── Private ────────────────────────────────────────────────────────────
        private readonly List<Vector3Int> _floorList;
        private readonly HashSet<Vector3Int> _floorSet;
        private readonly HashSet<Vector3Int> _wallSet;
        private readonly Random _random;

        internal DungeonMap(
            Vector3Int spawnCell,
            Vector3Int exitCell,
            List<Vector3Int> enemyCells,
            List<Vector3Int> floorCells,
            List<Vector3Int> wallCells,
            List<Vector3Int> pathCells,
            Tilemap tilemap,
            int width,
            int height,
            int seed)
        {
            SpawnCell = spawnCell;
            ExitCell  = exitCell;
            Tilemap   = tilemap;
            Width     = width;
            Height    = height;
            Seed      = seed;

            // Defensive copies — caller's lists may be cleared on next generation.
            EnemyCells = new List<Vector3Int>(enemyCells).AsReadOnly();
            FloorCells = new List<Vector3Int>(floorCells).AsReadOnly();
            WallCells  = new List<Vector3Int>(wallCells).AsReadOnly();
            PathCells  = new List<Vector3Int>(pathCells).AsReadOnly();

            _floorList = new List<Vector3Int>(floorCells);
            _floorSet  = new HashSet<Vector3Int>(floorCells);
            _wallSet   = new HashSet<Vector3Int>(wallCells);
            _random    = new Random(seed);

            // Pre-compute world positions once.
            SpawnWorld = CellCentre(spawnCell);
            ExitWorld  = CellCentre(exitCell);

            var enemyWorld = new List<Vector3>(enemyCells.Count);
            foreach (var c in enemyCells) enemyWorld.Add(CellCentre(c));
            EnemyWorldPositions = enemyWorld.AsReadOnly();

            var floorWorld = new List<Vector3>(floorCells.Count);
            foreach (var c in floorCells) floorWorld.Add(CellCentre(c));
            FloorWorldPositions = floorWorld.AsReadOnly();
        }

        // ── Query helpers ──────────────────────────────────────────────────────

        /// <summary>Returns a random walkable floor cell. Same seed = same sequence.</summary>
        public Vector3Int GetRandomFloorCell()
        {
            if (_floorList.Count == 0)
                throw new InvalidOperationException("[DungeonMap] No floor cells available.");
            return _floorList[_random.Next(_floorList.Count)];
        }

        /// <summary>Returns the world position (cell centre) of a random floor cell.</summary>
        public Vector3 GetRandomFloorWorld() => CellCentre(GetRandomFloorCell());

        /// <summary>True if the cell is a walkable floor tile.</summary>
        public bool IsFloor(Vector3Int cell) => _floorSet.Contains(cell);

        /// <summary>True if the cell is a wall tile.</summary>
        public bool IsWall(Vector3Int cell) => _wallSet.Contains(cell);

        private Vector3 CellCentre(Vector3Int cell)
            => Tilemap.CellToWorld(cell) + Tilemap.cellSize / 2f;
    }
}
