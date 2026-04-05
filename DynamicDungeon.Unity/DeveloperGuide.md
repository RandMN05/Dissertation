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
| `Seed` | `int` | The actual seed used — assign to `DungeonGeneratorComponent.Seed` and call `Generate()` again to reproduce this exact map. Do not leave `Seed` at 0, as that triggers random re-seeding. |

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
    Instantiate(turretPrefab, _map.Tilemap.GetCellCenterWorld(cell), Quaternion.identity);
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
// Requires: using System.Linq;
var allWalkable = map.FloorCells
    .Append(map.SpawnCell)
    .Append(map.ExitCell)
    .Concat(map.EnemyCells);
```

**Using `PathCells` when it's empty**
`PathCells` is only populated when `ComputeShortestPath` is enabled on `DungeonGeneratorComponent`. Check `map.PathCells.Count > 0` before using it.
