# Developer API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose the full generated map to developers via a `DungeonMap` result object delivered through an event, giving complete freedom to build any game mechanics without coupling to generator internals.

**Architecture:** A new `DungeonMap` class holds all tile positions (cells + world), query helpers, and a `Tilemap` reference. `DungeonGeneratorComponent` builds and fires a `DungeonMap` via a C# event and a UnityEvent at the end of every `Generate()` call. `DungeonBootstrapper` is updated to demonstrate the new pattern. A `DeveloperGuide.md` is written at the package root.

**Tech Stack:** C# / Unity 6, `UnityEngine.Tilemaps`, `System.Collections.Generic`, `System.Random`

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| **Create** | `DynamicDungeon.Unity/Runtime/DungeonMap.cs` | Self-contained map result object |
| **Modify** | `DynamicDungeon.Unity/Runtime/DungeonGeneratorComponent.cs` | Build DungeonMap, fire events, expose LastMap |
| **Modify** | `DynamicDungeon.Unity/Runtime/DungeonBootstrapper.cs` | Use DungeonMap instead of direct generator properties |
| **Create** | `DynamicDungeon.Unity/DeveloperGuide.md` | Full developer reference |

No Core library changes. No DLL rebuild required.

---

## Task 1: Create `DungeonMap`

**Files:**
- Create: `DynamicDungeon.Unity/Runtime/DungeonMap.cs`

- [ ] **Step 1: Create the file with the full class**

Create `DynamicDungeon.Unity/Runtime/DungeonMap.cs` with this exact content:

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add DynamicDungeon.Unity/Runtime/DungeonMap.cs
git commit -m "feat: add DungeonMap result object"
```

---

## Task 2: Update `DungeonGeneratorComponent` to build `DungeonMap` and fire events

**Files:**
- Modify: `DynamicDungeon.Unity/Runtime/DungeonGeneratorComponent.cs`

- [ ] **Step 1: Add the two missing `using` directives at the top of the file**

Add after the existing usings (line 7, after `using CoreTile = ...`):

```csharp
using System;
using UnityEngine.Events;
```

- [ ] **Step 2: Add event declarations and `LastMap` property**

Add this block immediately after the existing `[Header("Last Generation Info (read-only)")]` block (after line 65, after the `public int LastSeedUsed => _lastSeedUsed;` line):

```csharp
        // ── Developer API ──────────────────────────────────────────────────────

        /// <summary>
        /// The map produced by the most recent Generate() call.
        /// Null before the first generation. Use OnMapGenerated to react at the
        /// moment generation completes rather than polling this property.
        /// </summary>
        public DungeonMap LastMap { get; private set; }

        /// <summary>
        /// Fired at the end of every Generate() call. The DungeonMap argument
        /// contains every tile position, world position, and query helper you need.
        /// Subscribe in Awake() to guarantee you don't miss the first generation.
        /// </summary>
        public event Action<DungeonMap> OnMapGenerated;

        /// <summary>
        /// Inspector-wirable event fired immediately after OnMapGenerated.
        /// No map data is passed — use OnMapGenerated (C# event) if you need the map.
        /// </summary>
        [Header("Events")]
        public UnityEvent OnGenerationComplete = new UnityEvent();
```

- [ ] **Step 3: Replace `ApplyToTilemap` to collect floor/wall cells, build `DungeonMap`, and fire events**

Replace the entire `ApplyToTilemap` method (lines 159–191 in the original file):

```csharp
        private void ApplyToTilemap(TileMap map)
        {
            _tilemap.ClearAllTiles();
            _enemyCells.Clear();
            _pathCells.Clear();

            var pathSet    = new HashSet<(int, int)>(map.ShortestPath);
            var floorCells = new List<Vector3Int>();
            var wallCells  = new List<Vector3Int>();

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var coreTile  = map.Get(x, y);
                var unityTile = MapToUnityTile(coreTile);
                if (unityTile == null) continue;

                var cell = new Vector3Int(x, y, 0);
                _tilemap.SetTile(cell, unityTile);

                switch (coreTile)
                {
                    case CoreTile.Floor: floorCells.Add(cell);  break;
                    case CoreTile.Wall:  wallCells.Add(cell);   break;
                    case CoreTile.Enemy: _enemyCells.Add(cell); break;
                }

                if (pathSet.Contains((x, y)))
                    _pathCells.Add(cell);
            }

            _spawnCell = new Vector3Int(map.SpawnPoint.x, map.SpawnPoint.y, 0);
            _exitCell  = new Vector3Int(map.ExitPoint.x,  map.ExitPoint.y,  0);

            LastMap = new DungeonMap(
                _spawnCell, _exitCell,
                _enemyCells, floorCells, wallCells, _pathCells,
                _tilemap, map.Width, map.Height, _lastSeedUsed);

            OnMapGenerated?.Invoke(LastMap);
            OnGenerationComplete?.Invoke();

            if (Application.isPlaying)
                CentreCamera();
        }
```

- [ ] **Step 4: Build the project to verify it compiles**

```bash
dotnet build DynamicDungeon.Core/DynamicDungeon.Core.csproj
```

Expected output: `Build succeeded.` (Core builds cleanly — Unity-layer errors would only surface in the Unity Editor, not here.)

- [ ] **Step 5: Commit**

```bash
git add DynamicDungeon.Unity/Runtime/DungeonGeneratorComponent.cs
git commit -m "feat: build DungeonMap and fire OnMapGenerated after generation"
```

---

## Task 3: Update `DungeonBootstrapper` to use `DungeonMap`

**Files:**
- Modify: `DynamicDungeon.Unity/Runtime/DungeonBootstrapper.cs`

- [ ] **Step 1: Replace the `Start` method and remove the now-unused private `CellToWorld` helper**

The bootstrapper calls `Generate()` in `Start`. Since `Generate()` fires `OnMapGenerated` synchronously before returning, `LastMap` is guaranteed to be set immediately after the call. Replace `Start` and remove the `CellToWorld` helper at the bottom of the file:

```csharp
        private void Start()
        {
            // Load from the saved asset (guaranteed same map every time) or
            // fall back to whatever is configured on the generator directly.
            if (LevelData != null)
                DungeonGenerator.LoadFrom(LevelData);
            else
                DungeonGenerator.Generate();

            // LastMap is populated synchronously by Generate() / LoadFrom().
            var map = DungeonGenerator.LastMap;

            if (PlayerPrefab != null)
            {
                _player     = Instantiate(PlayerPrefab, map.SpawnWorld, Quaternion.identity);
                _player.tag = "Player";
            }

            if (EnemyPrefab != null)
                foreach (var worldPos in map.EnemyWorldPositions)
                    Instantiate(EnemyPrefab, worldPos, Quaternion.identity);

            _exitWorldPos = map.ExitWorld;
        }
```

Also remove the `CellToWorld` private method at the bottom of `DungeonBootstrapper` — it is no longer called:

```csharp
        // DELETE this entire method — replaced by map.SpawnWorld / map.ExitWorld / map.EnemyWorldPositions
        private Vector3 CellToWorld(Vector3Int cell)
            => Tilemap.CellToWorld(cell) + Tilemap.cellSize / 2f;
```

- [ ] **Step 2: Commit**

```bash
git add DynamicDungeon.Unity/Runtime/DungeonBootstrapper.cs
git commit -m "refactor: DungeonBootstrapper uses DungeonMap instead of direct generator access"
```

---

## Task 4: Write `DeveloperGuide.md`

**Files:**
- Create: `DynamicDungeon.Unity/DeveloperGuide.md`

- [ ] **Step 1: Create the guide**

Create `DynamicDungeon.Unity/DeveloperGuide.md` with the following content:

````markdown
# DynamicDungeon — Developer Guide

This package generates a procedural dungeon and hands you the full map data so you can build whatever game mechanics you want on top. You are not required to use `DungeonBootstrapper`, `PlayerController`, or `EnemyController` — those are reference implementations you can replace entirely.

---

## Quick Start

**Three steps to get full map access in your own script:**

### Step 1 — Make sure `DungeonGeneratorComponent` is in the scene

The Level Designer window (Window → DynamicDungeon → Level Designer) creates scenes for you with the generator already set up. If you are building your own scene, add a `Grid` GameObject, a child `Tilemap` + `TilemapRenderer`, and attach `DungeonGeneratorComponent` to the Tilemap object.

`DungeonBootstrapper` calls `Generate()` at `Start`. If you are not using `DungeonBootstrapper`, call `generator.Generate()` yourself at startup.

### Step 2 — Subscribe to `OnMapGenerated` in `Awake`

```csharp
using UnityEngine;
using DynamicDungeon.Unity;

public class MyGameManager : MonoBehaviour
{
    private DungeonGeneratorComponent _generator;

    private void Awake()
    {
        _generator = FindFirstObjectByType<DungeonGeneratorComponent>();
        _generator.OnMapGenerated += OnMapReady;
    }

    private void OnMapReady(DungeonMap map)
    {
        // map has everything — do whatever you want here
        Debug.Log($"Map ready. {map.FloorCells.Count} floor tiles, seed {map.Seed}");
    }
}
```

> **Why `Awake` and not `Start`?**
> `DungeonBootstrapper.Start()` calls `Generate()`, which fires `OnMapGenerated` synchronously. If your script subscribes in its own `Start()`, there is no guarantee it runs before `DungeonBootstrapper.Start()` — you may miss the event. `Awake` always runs before any `Start`, so subscribing there is safe.

### Step 3 — Use the map

That's it. The `DungeonMap` object passed to your callback contains everything listed below.

---

## `DungeonMap` Reference

`DungeonMap` is delivered to your `OnMapGenerated` handler. Store it as a field and query it any time.

### Tile-Space Properties (Tilemap cells)

| Property | Type | Description |
|---|---|---|
| `SpawnCell` | `Vector3Int` | Tilemap cell of the player spawn point |
| `ExitCell` | `Vector3Int` | Tilemap cell of the level exit |
| `EnemyCells` | `IReadOnlyList<Vector3Int>` | All enemy spawn marker cells |
| `FloorCells` | `IReadOnlyList<Vector3Int>` | All walkable floor cells |
| `WallCells` | `IReadOnlyList<Vector3Int>` | All wall cells |
| `PathCells` | `IReadOnlyList<Vector3Int>` | A* shortest path from spawn to exit (empty if `ComputeShortestPath` was false) |

### World-Space Properties

| Property | Type | Description |
|---|---|---|
| `SpawnWorld` | `Vector3` | World position (cell centre) of the spawn point |
| `ExitWorld` | `Vector3` | World position (cell centre) of the exit |
| `EnemyWorldPositions` | `IReadOnlyList<Vector3>` | World positions of all enemy cells |
| `FloorWorldPositions` | `IReadOnlyList<Vector3>` | World positions of all floor cells |

### Map Info

| Property | Type | Description |
|---|---|---|
| `Tilemap` | `Tilemap` | The live Tilemap this map was rendered onto |
| `Width` | `int` | Map width in tiles |
| `Height` | `int` | Map height in tiles |
| `Seed` | `int` | The seed used — pass back to `DungeonGeneratorComponent.Seed` to reproduce this exact map |

### Methods

| Method | Returns | Description |
|---|---|---|
| `GetRandomFloorCell()` | `Vector3Int` | A random walkable floor cell |
| `GetRandomFloorWorld()` | `Vector3` | World position of a random floor cell |
| `IsFloor(Vector3Int cell)` | `bool` | True if the cell is a walkable floor tile |
| `IsWall(Vector3Int cell)` | `bool` | True if the cell is a wall tile |

> `GetRandomFloorCell()` and `GetRandomFloorWorld()` use a `System.Random` seeded from `Seed`, so the same seed always produces the same sequence of random picks.

---

## Worked Examples

### Spawn your player at the spawn point

```csharp
private void OnMapReady(DungeonMap map)
{
    Instantiate(playerPrefab, map.SpawnWorld, Quaternion.identity);
}
```

### Place a chest on a random floor tile

```csharp
private void OnMapReady(DungeonMap map)
{
    // Pick 5 random floor positions and place a chest at each
    for (int i = 0; i < 5; i++)
    {
        Vector3 pos = map.GetRandomFloorWorld();
        Instantiate(chestPrefab, pos, Quaternion.identity);
    }
}
```

### Spawn your own enemies at every enemy marker

```csharp
private void OnMapReady(DungeonMap map)
{
    foreach (Vector3 pos in map.EnemyWorldPositions)
        Instantiate(myEnemyPrefab, pos, Quaternion.identity);
}
```

### Validate a position before placing an object

```csharp
// Example: player tries to place a turret — only allow placement on floor tiles
public bool TryPlaceTurret(Vector3Int cell)
{
    if (!_map.IsFloor(cell)) return false;
    Instantiate(turretPrefab, _map.Tilemap.CellToWorld(cell), Quaternion.identity);
    return true;
}
```

### Highlight the shortest path (e.g. for a hint system)

```csharp
private void OnMapReady(DungeonMap map)
{
    foreach (Vector3Int cell in map.PathCells)
        Instantiate(highlightTile, map.Tilemap.CellToWorld(cell), Quaternion.identity);
}
```

---

## Inspector Wiring (no code)

`DungeonGeneratorComponent` also exposes a `OnGenerationComplete` UnityEvent in the Inspector. This fires at the same time as `OnMapGenerated` but carries no data — it is useful for triggering animations, audio, or enabling UI panels without writing code.

1. Select the GameObject that has `DungeonGeneratorComponent`.
2. Find **Events → On Generation Complete** in the Inspector.
3. Click **+**, drag in the target GameObject, and pick a method.

> If you need the map data in your callback, use the C# `OnMapGenerated` event instead.

---

## Execution Order — Why `Awake`?

Unity calls all `Awake()` methods before any `Start()` method. `DungeonBootstrapper.Start()` triggers map generation. If your script subscribes to `OnMapGenerated` in its own `Start()`, it may run *after* `DungeonBootstrapper.Start()` — and you miss the event entirely.

```
Frame 1:
  Awake() on ALL scripts   ← subscribe here — safe
  Start() on ALL scripts   ← DungeonBootstrapper.Start() fires Generate() here
```

**Always subscribe in `Awake`.**

---

## Common Mistakes

**Subscribing in `Start` instead of `Awake`**
You may miss `OnMapGenerated` if `DungeonBootstrapper.Start()` runs first. Subscribe in `Awake`.

**Checking `LastMap` before generation**
`DungeonGeneratorComponent.LastMap` is `null` until the first `Generate()` call. Don't read it at `Awake` time — use the `OnMapGenerated` event instead.

**Calling `Generate()` twice**
Calling `Generate()` a second time replaces `LastMap` with a new map and fires the event again. Any code that stored references to the old `DungeonMap` is safe — `DungeonMap` is immutable — but subscribers will be called again with the new map.

**Expecting `FloorCells` to include the spawn or exit tile**
`FloorCells` contains only `Floor`-typed tiles. Spawn and exit have their own tiles (`SpawnCell`, `ExitCell`). If you want all walkable tiles, combine them yourself:
```csharp
var allWalkable = map.FloorCells
    .Append(map.SpawnCell)
    .Append(map.ExitCell)
    .Concat(map.EnemyCells);
```

**Using `PathCells` when it's empty**
`PathCells` is only populated when `ComputeShortestPath` is enabled on `DungeonGeneratorComponent`. Check `map.PathCells.Count > 0` before using it.
````

- [ ] **Step 2: Commit**

```bash
git add DynamicDungeon.Unity/DeveloperGuide.md
git commit -m "docs: add DeveloperGuide.md for game developers"
```

---

## Task 5: Verify

- [ ] **Step 1: Run existing Core tests to confirm nothing regressed**

```bash
dotnet test DynamicDungeon.Core.Tests/DynamicDungeon.Core.Tests.csproj
```

Expected: all tests pass. (No Core code changed — this is a sanity check.)

- [ ] **Step 2: Manual Unity verification checklist**

Open the Unity project with the package loaded, then verify:

1. Open a generated level scene in the Editor.
2. Enter Play Mode — map generates, player and enemies spawn as before (DungeonBootstrapper still works).
3. Add a test MonoBehaviour to any GameObject in the scene:

```csharp
using UnityEngine;
using DynamicDungeon.Unity;

public class MapVerifier : MonoBehaviour
{
    private void Awake()
    {
        FindFirstObjectByType<DungeonGeneratorComponent>().OnMapGenerated += m =>
        {
            Debug.Log($"SpawnWorld: {m.SpawnWorld}");
            Debug.Log($"ExitWorld: {m.ExitWorld}");
            Debug.Log($"Floor tiles: {m.FloorCells.Count}");
            Debug.Log($"Wall tiles: {m.WallCells.Count}");
            Debug.Log($"Enemy count: {m.EnemyCells.Count}");
            Debug.Log($"Random floor: {m.GetRandomFloorWorld()}");
            Debug.Log($"IsFloor(SpawnCell): {m.IsFloor(m.SpawnCell)}");  // false — spawn is not floor
            Debug.Log($"IsWall at (0,0): {m.IsWall(new UnityEngine.Vector3Int(0, 0, 0))}"); // true — border is wall
        };
    }
}
```

4. Enter Play Mode. All eight log lines should print with sensible values. No errors.
5. Remove `MapVerifier` when done.

- [ ] **Step 3: Verify `DungeonGeneratorComponent` Inspector shows the Events section**

Select the Tilemap object in the scene. In the Inspector under `DungeonGeneratorComponent`, confirm an **Events / On Generation Complete** UnityEvent field is visible.

- [ ] **Step 4: Final commit**

```bash
git add .
git commit -m "chore: developer API implementation complete"
```
