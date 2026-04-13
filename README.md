# Dynamic Dungeon Generator

A Unity 6 package for procedural 2D dungeon generation. Drop it into any Unity project and get a fully generated tilemap — with spawn point, exit, and enemy markers — in a single `Generate()` call.

Three generation algorithms, three biomes, three difficulty levels, A\* pathfinding, a built-in Level Designer editor window, and a clean C\# API for hooking in your own game logic.

---

## Table of Contents

1. [Features](#1-features)
2. [Prerequisites](#2-prerequisites)
3. [Installation](#3-installation)
4. [Create Tile Sprites](#4-create-tile-sprites)
5. [Scene Setup](#5-scene-setup)
   - [Option A — Level Designer Window (recommended)](#option-a--level-designer-window-recommended)
   - [Option B — Manual Scene Setup](#option-b--manual-scene-setup)
6. [Generation Settings Reference](#6-generation-settings-reference)
   - [Algorithm](#algorithm)
   - [Biome](#biome)
   - [Difficulty](#difficulty)
   - [Other Settings](#other-settings)
7. [Player and Enemy Prefabs](#7-player-and-enemy-prefabs)
8. [DungeonBootstrapper Reference](#8-dungeonbootstrapper-reference)
9. [Scripting API](#9-scripting-api)
   - [DungeonGeneratorComponent](#dungeonGeneratorComponent)
   - [DungeonMap](#dungeonmap)
   - [DungeonLevelData](#dungeonleveldata)
   - [Events](#events)
10. [Worked Examples](#10-worked-examples)
11. [Wall Collisions](#11-wall-collisions)
12. [Level Designer Window Reference](#12-level-designer-window-reference)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Features

- **Three generation algorithms** — Cellular Automata, Perlin Noise, Wave Function Collapse
- **Three biomes** — Dungeon, Cave, Ruins — each tuning density and feel independently per algorithm
- **Three difficulty levels** — controls enemy count and placement density
- **Flood-fill validation** — every generated map is checked; invalid maps are automatically retried
- **A\* shortest path** — optional, computed from spawn to exit; accessible via `DungeonMap.PathCells`
- **Level Designer editor window** — create, preview, and save multiple levels without writing code
- **`DungeonMap` scripting API** — tile cells, world positions, random floor helpers, wall/floor queries
- **`DungeonLevelData` ScriptableObject** — save a level's exact seed so the same map loads every time
- **Reference scripts** — `PlayerController`, `EnemyController`, and `DungeonBootstrapper` included as starting points
- **Zero external dependencies** — no NuGet packages required

---

## 2. Prerequisites

- **Unity 6** (version 6000.0 or newer) — the package targets `netstandard2.1` and will not work on earlier Unity versions
- **Git** — to clone the repository

---

## 3. Installation

**Step 1** — Clone the repository:

```bash
git clone https://github.com/<your-repo>/DynamicDungeon.git
```

You do not need to build anything. The compiled Core library (`DynamicDungeon.Core.dll`) is already included in the package.

**Step 2** — Open your Unity 6 project (or create a new **2D** project).

**Step 3** — Open **Package Manager**: `Window → Package Manager`.

**Step 4** — Click the **+** button (top-left) → **Add package from disk**.

**Step 5** — Navigate to the cloned repository, open the `DynamicDungeon.Unity/` folder, and select `package.json`.

Unity will import the package. You should see **Dynamic Dungeon Generator** appear in the Package Manager list.

> **Confirmation** — a new menu item appears: **Window → DynamicDungeon → Level Designer**. If you see it, the install worked.

---

## 4. Create Tile Sprites

The generator needs a Unity Tile asset for each of the five tile types. If you already have tile assets, skip this section.

**Quickest option — Unity's built-in solid colour tiles:**

1. In the Project window, create a folder `Assets/Tiles`.
2. Right-click → **Create → 2D → Tiles → Tile**.
3. Create five tiles and name them: `WallTile`, `FloorTile`, `SpawnTile`, `ExitTile`, `EnemyTile`.
4. Select each tile asset and assign a **Sprite** to it. Unity's default white square works — tint each one a different colour in the tile's **Color** field.

> Suggested colour scheme for testing: Wall = dark grey, Floor = light grey, Spawn = green, Exit = yellow, Enemy = red.

---

## 5. Scene Setup

You have two options. **Option A** (Level Designer) handles all the wiring for you and is recommended for most cases.

---

### Option A — Level Designer Window (recommended)

The Level Designer creates a complete, ready-to-run scene with all GameObjects wired up.

1. Open the Level Designer: **Window → DynamicDungeon → Level Designer**.
2. Click **Add** to create a new level slot.
3. Fill in the level name and generation settings (see [Section 6](#6-generation-settings-reference)).
4. Assign your five tile sprites in the **Tile Sprites** section of the slot.
5. Assign the **Scene Generator** field at the top of the window — drag the `DungeonGeneratorComponent` from your scene hierarchy. If you don't have one yet:
   - Create a new scene.
   - Add `GameObject → 2D Object → Tilemap → Rectangular` (creates a Grid + Tilemap automatically).
   - Select the **Tilemap** child object and add a **DungeonGeneratorComponent** component.
6. Click **Generate Preview** — the map appears in the Scene view.
7. When happy with the result, click **Save Level** — this writes a `.unity` scene file and a `.asset` data file, and adds the scene to Build Settings automatically.
8. Open the generated scene. A `DungeonBootstrapper` GameObject is already wired up.
9. Assign your **Player Prefab** and **Enemy Prefab** on the `DungeonBootstrapper` (see [Section 7](#7-player-and-enemy-prefabs)).

---

### Option B — Manual Scene Setup

Use this if you want full control over scene structure.

1. Create a new scene.
2. Add `GameObject → 2D Object → Tilemap → Rectangular`. This creates a **Grid** with a child **Tilemap** automatically.
3. Select the **Tilemap** child object and add the **DungeonGeneratorComponent** component.
4. Assign your five tile sprites in the Inspector under **Tile Sprites**.
5. Create an empty GameObject named `DungeonBootstrapper` and add the **DungeonBootstrapper** component.
6. In the `DungeonBootstrapper` Inspector, assign the **Dungeon Generator** field to the `DungeonGeneratorComponent` on your Tilemap.
7. Hit **Play** — the map generates automatically.

---

## 6. Generation Settings Reference

These settings live on the `DungeonGeneratorComponent` in the Inspector (or in the Level Designer window's slot fields).

---

### Algorithm

Controls the generation method. Each produces a visually distinct style.

| Algorithm | Description |
|---|---|
| **Cellular Automata** | Random fill smoothed by Moore neighbourhood rules. Produces cave-like organic shapes. Fast and reliable — good default choice. |
| **Perlin Noise** | Seeded 3-octave 2D noise threshold to Wall/Floor. Produces flowing, natural-looking layouts with smooth boundaries. |
| **Wave Function Collapse** | AC-3 constraint propagation over a `{Floor, Wall, WallInterior}` superposition. Produces structured, tile-rule-consistent maps. Internally the most complex — may need more `MaxRegenerationAttempts` on small maps. |

---

### Biome

Tunes tile density and feel within the chosen algorithm, independently of algorithm choice.

| Biome | Description | Openness (approx.) |
|---|---|---|
| **Dungeon** | Tight corridors, dense walls, few open spaces. Classic dungeon feel. | ~23% floor |
| **Cave** | Spacious caverns, organic boundaries, more open space. | ~76% floor |
| **Ruins** | Mix of corridors and open areas. Partially open. | ~46% floor |

---

### Difficulty

Controls how many enemy markers are placed during generation.

| Difficulty | Enemy Count |
|---|---|
| **Easy** | 3 enemies |
| **Medium** | 6 enemies |
| **Hard** | 12 enemies |

---

### Other Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| **Width** | `int` (10–200) | `50` | Map width in tiles. |
| **Height** | `int` (10–200) | `50` | Map height in tiles. |
| **Seed** | `int` | `0` | Set to `0` for a random map every time. Set a specific number to reproduce the same map exactly. The actual seed used is saved with the level. |
| **Compute Shortest Path** | `bool` | `false` | Enables A\* pathfinding from Spawn to Exit after generation. Accessible via `DungeonMap.PathCells`. Useful for hint systems or difficulty scoring. |
| **Max Regeneration Attempts** | `int` (1–20) | `10` | If a generated map fails flood-fill validation (Exit not reachable from Spawn), the generator retries up to this many times with incremented seeds. |

---

## 7. Player and Enemy Prefabs

The package includes two reference scripts as starting points. You are not required to use them — replace them with your own at any time.

---

### PlayerController

Attach to your player prefab. Requires `Rigidbody2D` and `CircleCollider2D`.

**To create a player prefab:**
1. `GameObject → 2D Object → Sprite`.
2. Add **Rigidbody2D** — set Gravity Scale to `0`, enable **Freeze Rotation Z**.
3. Add **CircleCollider2D**.
4. Add **PlayerController**.
5. Set the GameObject's **Tag** to `Player`.
6. Drag into `Assets/` to save as a prefab.

**PlayerController fields:**

| Field | Type | Default | Description |
|---|---|---|---|
| `Speed` | `float` | `3` | Movement speed in units per second. Reads WASD / arrow keys via `Input.GetAxisRaw`. |

---

### EnemyController

Attach to your enemy prefab. Requires `Rigidbody2D` and `CircleCollider2D`.

**To create an enemy prefab:**
1. `GameObject → 2D Object → Sprite`.
2. Add **Rigidbody2D** — set Gravity Scale to `0`, enable **Freeze Rotation Z**.
3. Add **CircleCollider2D**.
4. Add **EnemyController**.
5. Drag into `Assets/` to save as a prefab.

**EnemyController fields:**

| Field | Type | Default | Description |
|---|---|---|---|
| `Speed` | `float` | `1.5` | Chase speed. The enemy moves directly toward the Player tag every `FixedUpdate`. |

---

## 8. DungeonBootstrapper Reference

`DungeonBootstrapper` is a `MonoBehaviour` that wires together the generator, prefab spawning, and win/lose logic. It is created automatically by the Level Designer, but you can also add it manually.

**Inspector fields:**

| Field | Type | Description |
|---|---|---|
| `DungeonGenerator` | `DungeonGeneratorComponent` | The generator in this scene. |
| `PlayerPrefab` | `GameObject` | Spawned at `map.SpawnWorld` on Start. Needs `PlayerController`, `Rigidbody2D`, `CircleCollider2D`, and `Player` tag. |
| `EnemyPrefab` | `GameObject` | Spawned at every `map.EnemyWorldPositions` entry on Start. Needs `EnemyController`, `Rigidbody2D`, `CircleCollider2D`. |
| `LevelData` | `DungeonLevelData` | The saved level asset. If assigned, the bootstrapper calls `LoadFrom(LevelData)` to reproduce the exact saved map. If null, falls back to the generator's baked-in settings. |
| `NextSceneName` | `string` | Scene name to load when the player reaches the exit. Leave empty on the final level. |
| `WinDistance` | `float` | Distance (in world units) from the exit at which the player wins. Default: `0.6`. |
| `LoseDistance` | `float` | Distance (in world units) from an enemy at which the player loses. Default: `0.4`. |

**Behaviour:**
- On `Start`: calls `Generate()` (or `LoadFrom(LevelData)` if a saved asset is wired), then spawns the player at `map.SpawnWorld` and one enemy per `map.EnemyWorldPositions` entry.
- On `Update`: checks win condition (player near exit) and lose condition (any enemy near player). Loads `NextSceneName` on win, or shows a game-over screen with a replay button.

---

## 9. Scripting API

If you are writing your own game logic and don't want to use `DungeonBootstrapper`, subscribe to `OnMapGenerated` in `Awake` and use the `DungeonMap` object it provides.

> **Always subscribe in `Awake`, not `Start`.** `DungeonBootstrapper.Start()` calls `Generate()`, which fires `OnMapGenerated` synchronously. If you subscribe in `Start`, you may run after the event has already fired and miss it. `Awake` is always called before any `Start`.

---

### DungeonGeneratorComponent

The main `MonoBehaviour`. Attach to a `Tilemap` object.

#### Serialized fields (Inspector-configurable)

| Field | Type | Default | Description |
|---|---|---|---|
| `Algorithm` | `AlgorithmType` | `CellularAutomata` | Generation algorithm. |
| `Biome` | `BiomeType` | `Dungeon` | Map biome. |
| `Difficulty` | `DifficultyLevel` | `Medium` | Enemy density. |
| `Width` | `int` | `50` | Map width in tiles (10–200). |
| `Height` | `int` | `50` | Map height in tiles (10–200). |
| `Seed` | `int` | `0` | Generation seed. `0` = random each call. |
| `ComputeShortestPath` | `bool` | `false` | Whether to run A\* after generation. |
| `MaxRegenerationAttempts` | `int` | `10` | Max retry attempts if validation fails. |
| `WallTile` | `TileBase` | `null` | Unity Tile asset for wall cells. |
| `FloorTile` | `TileBase` | `null` | Unity Tile asset for floor cells. |
| `SpawnTile` | `TileBase` | `null` | Unity Tile asset for the spawn point. |
| `ExitTile` | `TileBase` | `null` | Unity Tile asset for the exit. |
| `EnemyTile` | `TileBase` | `null` | Unity Tile asset for enemy markers. |

#### Read-only output properties

| Property | Type | Description |
|---|---|---|
| `SpawnCell` | `Vector3Int` | Tilemap cell of the player spawn point from the last generation. |
| `ExitCell` | `Vector3Int` | Tilemap cell of the exit from the last generation. |
| `EnemyCells` | `IReadOnlyList<Vector3Int>` | All enemy spawn cells from the last generation. |
| `PathCells` | `IReadOnlyList<Vector3Int>` | A\* path cells from spawn to exit (empty if `ComputeShortestPath` was false). |
| `LastGenerationMs` | `long` | How long the last generation took in milliseconds. |
| `LastSeedUsed` | `int` | The seed that was actually used — may differ from `Seed` when retries occurred. |
| `LastMap` | `DungeonMap` | The full map object from the last generation. `null` before the first call. |

#### Methods

| Method | Description |
|---|---|
| `Generate()` | Generates a new dungeon and renders it onto the attached Tilemap. Safe to call at runtime or from the Editor. Fires `OnMapGenerated` and `OnGenerationComplete` on completion. |
| `LoadFrom(DungeonLevelData data)` | Applies all settings from a saved `DungeonLevelData` asset, then calls `Generate()`. The seed stored in the asset guarantees the identical map is produced every time. |

#### Events

| Event | Signature | Description |
|---|---|---|
| `OnMapGenerated` | `Action<DungeonMap>` | C\# event fired at the end of every `Generate()` call. The `DungeonMap` argument contains all map data. Subscribe in `Awake`. |
| `OnGenerationComplete` | `UnityEvent` | Inspector-wirable event fired immediately after `OnMapGenerated`. Carries no map data — use `OnMapGenerated` if you need the map. |

---

### DungeonMap

Delivered to your `OnMapGenerated` handler. Store it as a field — it is immutable and safe to query at any time after generation. World positions are pre-computed at construction.

> Note: the `Tilemap` property references the live Unity component. Do not use it after the scene is unloaded.

#### Tile-space properties (Tilemap cells)

| Property | Type | Description |
|---|---|---|
| `SpawnCell` | `Vector3Int` | Tilemap cell of the player spawn point. |
| `ExitCell` | `Vector3Int` | Tilemap cell of the level exit. |
| `EnemyCells` | `IReadOnlyList<Vector3Int>` | All enemy spawn marker cells. |
| `FloorCells` | `IReadOnlyList<Vector3Int>` | All walkable floor cells. Does **not** include spawn, exit, or enemy cells — those have their own tile types. |
| `WallCells` | `IReadOnlyList<Vector3Int>` | All wall cells. |
| `PathCells` | `IReadOnlyList<Vector3Int>` | A\* shortest path cells from spawn to exit. Empty if `ComputeShortestPath` was false. |

#### World-space properties

| Property | Type | Description |
|---|---|---|
| `SpawnWorld` | `Vector3` | World position (cell centre) of the spawn point. |
| `ExitWorld` | `Vector3` | World position (cell centre) of the exit. |
| `EnemyWorldPositions` | `IReadOnlyList<Vector3>` | World positions of all enemy cells. |
| `FloorWorldPositions` | `IReadOnlyList<Vector3>` | World positions of all floor cells. |

#### Map info properties

| Property | Type | Description |
|---|---|---|
| `Tilemap` | `Tilemap` | The live Tilemap this map was rendered onto. |
| `Width` | `int` | Map width in tiles. |
| `Height` | `int` | Map height in tiles. |
| `Seed` | `int` | The actual seed used to generate this map. Assign to `DungeonGeneratorComponent.Seed` and call `Generate()` to reproduce the identical map. |

#### Methods

| Method | Returns | Description |
|---|---|---|
| `GetRandomFloorCell()` | `Vector3Int` | A random walkable floor cell. Uses `System.Random` seeded from `Seed` — the same seed always produces the same sequence. Throws if there are no floor cells. |
| `GetRandomFloorWorld()` | `Vector3` | World position (cell centre) of a random floor cell. |
| `IsFloor(Vector3Int cell)` | `bool` | `true` if the cell is a walkable floor tile. O(1) lookup. |
| `IsWall(Vector3Int cell)` | `bool` | `true` if the cell is a wall tile. O(1) lookup. |

---

### DungeonLevelData

A `ScriptableObject` that stores the exact parameters needed to reproduce one generated level. The seed is always the actual value used during generation — never `0` — so the same map loads every time.

**Create via:** `Assets → Create → DynamicDungeon → Level Data`, or automatically via the Level Designer window.

| Field | Type | Description |
|---|---|---|
| `LevelName` | `string` | Display name for the level. |
| `Algorithm` | `AlgorithmType` | Generation algorithm. |
| `Biome` | `BiomeType` | Map biome. |
| `Difficulty` | `DifficultyLevel` | Difficulty level. |
| `Width` | `int` | Map width in tiles. |
| `Height` | `int` | Map height in tiles. |
| `Seed` | `int` | The exact seed used when the level was saved. Guarantees the identical map on reload. |
| `WallTile` | `TileBase` | Wall tile asset reference. |
| `FloorTile` | `TileBase` | Floor tile asset reference. |
| `SpawnTile` | `TileBase` | Spawn tile asset reference. |
| `ExitTile` | `TileBase` | Exit tile asset reference. |
| `EnemyTile` | `TileBase` | Enemy tile asset reference. |

---

## 10. Worked Examples

All examples subscribe to `OnMapGenerated` in `Awake` to guarantee the event is not missed.

---

### Basic setup — subscribe to the map event

```csharp
using UnityEngine;
using DynamicDungeon.Unity;

public class MyGameManager : MonoBehaviour
{
    private void Awake()
    {
        FindFirstObjectByType<DungeonGeneratorComponent>().OnMapGenerated += OnMapReady;
    }

    private void OnMapReady(DungeonMap map)
    {
        Debug.Log($"Map ready — {map.FloorCells.Count} floor tiles, seed {map.Seed}");
    }
}
```

---

### Spawn the player at the spawn point

```csharp
[SerializeField] private GameObject playerPrefab;

private void OnMapReady(DungeonMap map)
{
    Instantiate(playerPrefab, map.SpawnWorld, Quaternion.identity);
}
```

---

### Spawn enemies at every enemy marker

```csharp
[SerializeField] private GameObject enemyPrefab;

private void OnMapReady(DungeonMap map)
{
    foreach (Vector3 pos in map.EnemyWorldPositions)
        Instantiate(enemyPrefab, pos, Quaternion.identity);
}
```

---

### Place random loot on floor tiles

```csharp
[SerializeField] private GameObject chestPrefab;

private void OnMapReady(DungeonMap map)
{
    for (int i = 0; i < 5; i++)
        Instantiate(chestPrefab, map.GetRandomFloorWorld(), Quaternion.identity);
}
```

---

### Highlight the shortest path (hint system)

Enable `ComputeShortestPath` on `DungeonGeneratorComponent` first.

```csharp
[SerializeField] private GameObject highlightPrefab;

private void OnMapReady(DungeonMap map)
{
    foreach (Vector3Int cell in map.PathCells)
        Instantiate(highlightPrefab, map.Tilemap.GetCellCenterWorld(cell), Quaternion.identity);
}
```

---

### Grid-locked movement (no physics)

```csharp
private DungeonMap _map;
private Vector3Int _currentCell;

private void OnMapReady(DungeonMap map)
{
    _map = map;
    _currentCell = map.SpawnCell;
}

private void TryMove(Vector3Int direction)
{
    Vector3Int target = _currentCell + direction;
    if (_map.IsWall(target)) return; // blocked
    _currentCell = target;
    transform.position = _map.Tilemap.GetCellCenterWorld(_currentCell);
}
```

---

### Validate tile placement (e.g. turret building)

```csharp
private DungeonMap _map;

public bool TryPlaceTurret(Vector3Int cell)
{
    if (!_map.IsFloor(cell)) return false;
    Instantiate(turretPrefab, _map.Tilemap.GetCellCenterWorld(cell), Quaternion.identity);
    return true;
}
```

---

### Reproduce a specific map (deterministic reload)

```csharp
// After generation, store the seed:
int savedSeed = map.Seed;

// Later, to reload the identical map:
generator.Seed = savedSeed;
generator.Generate();
```

---

### If you need all walkable tiles (floor + spawn + exit + enemy)

`FloorCells` only contains `Floor`-typed tiles. Spawn, exit, and enemy cells are separate.

```csharp
// Requires: using System.Linq;
var allWalkable = map.FloorCells
    .Append(map.SpawnCell)
    .Append(map.ExitCell)
    .Concat(map.EnemyCells);
```

---

## 11. Wall Collisions

The package does not add physics colliders by default — some games use physics, others use grid logic or raycasting. Choose the approach that suits your game.

---

### Option A — Tilemap Collider 2D (recommended, no code)

1. Select the **Tilemap** GameObject in the hierarchy.
2. Add **Tilemap Collider 2D** — Unity automatically generates a collider for every tile with a sprite. Walls become solid immediately.
3. Optionally add **Composite Collider 2D** — merges all tile colliders into one shape for better performance on large maps. Adding this also adds a **Rigidbody2D** — set its **Body Type** to **Static**.

Your player and enemy prefabs need a **Rigidbody2D** and a collider (e.g. **CircleCollider2D**) for physics interaction to work.

> This is the approach used by the built-in `PlayerController` and `EnemyController`.

---

### Option B — Code-driven colliders from `WallCells`

Use this when you need programmatic control — custom shapes, trigger zones, or walls that change at runtime.

```csharp
// wallColliderPrefab = empty GameObject with a BoxCollider2D sized to your tile (e.g. 1x1 units)

private void OnMapReady(DungeonMap map)
{
    foreach (Vector3Int cell in map.WallCells)
    {
        Vector3 worldPos = map.Tilemap.GetCellCenterWorld(cell);
        Instantiate(wallColliderPrefab, worldPos, Quaternion.identity);
    }
}
```

---

### Option C — No colliders (grid-locked or raycast movement)

Valid for games that validate movement via `map.IsWall()` rather than physics — see the grid-locked movement example in [Section 10](#10-worked-examples).

---

## 12. Level Designer Window Reference

Open via: **Window → DynamicDungeon → Level Designer**

The Level Designer is the main authoring tool. It manages multiple level slots, previews maps in the scene view, and saves complete scenes with all GameObjects wired up.

---

### Header controls

| Control | Description |
|---|---|
| **Add New Levels** / **Add** | Creates one or more new level slots in the window. |
| **Scene Generator** | Assign the `DungeonGeneratorComponent` from your open scene. Required for Generate Preview. |
| **Save Folder** / **Browse** | Project folder where level scenes and data assets are saved. Defaults to `Assets/DynamicDungeon/Levels`. Persisted per-project via `EditorPrefs`. |

---

### Per-level slot

Each slot is a collapsible foldout labelled with the level name and its status (`○ Not generated`, `● Previewed`, `✓ Saved`).

| Control | Description |
|---|---|
| **Name** | Display name for the level. Used in the saved file names (e.g. `Level_01_MyLevel.asset`). |
| **Algorithm** | Generation algorithm for this level. |
| **Biome** | Biome for this level. |
| **Difficulty** | Difficulty for this level. |
| **Width / Height** | Map dimensions for this level (10–200). |
| **Seed** / **Random** | Seed for this level. Click **Random** to set it back to `0` (random on next preview). |
| **Tile Sprites** | Wall, Floor, Spawn, Exit, Enemy tile asset slots for this level. |
| **Generate Preview** | Applies this slot's settings to the Scene Generator and calls `Generate()`. Shows the result in the Scene view. Requires a Scene Generator to be assigned. |
| **Save Level** | Writes a `DungeonLevelData` asset and a `.unity` scene file. The scene contains a fully wired `Grid → Tilemap → DungeonGeneratorComponent` and `DungeonBootstrapper`. Adds the scene to Build Settings automatically. Only available after Generate Preview. |
| **Last Generation** | Read-only stats: seed used, generation time (ms), enemy count. |
| **×** | Deletes the saved scene and data asset after confirmation (saved slots only). |

---

### Bottom controls

| Control | Description |
|---|---|
| **Generate All Previews** | Calls Generate Preview for every slot in order. |
| **Save All Levels** | Calls Save Level for every slot that has been previewed. |
| **Delete All Levels** | Deletes all saved scenes and data assets after confirmation. Cannot be undone. |

---

### What Save Level produces

For a slot named `"My Level"` saved as level 2:

```
Assets/DynamicDungeon/Levels/
  Level_02_My_Level.asset   ← DungeonLevelData ScriptableObject
  Level_02_My_Level.unity   ← Scene with Grid, Tilemap, DungeonGeneratorComponent, DungeonBootstrapper
```

The scene is added to Build Settings. Open it and assign your **Player Prefab** and **Enemy Prefab** on the `DungeonBootstrapper` component. `NextSceneName` is pre-filled with the following level's scene name automatically.

---

## 13. Troubleshooting

**Map generates but nothing is visible**
Assign all five tile sprites on `DungeonGeneratorComponent`. A missing `WallTile` or `FloorTile` leaves those cells blank — no error is thrown.

**Player or enemies don't spawn**
Assign `PlayerPrefab` and `EnemyPrefab` on `DungeonBootstrapper`. If `LevelData` is null in the Inspector, re-save the level via the Level Designer window.

**My script does not receive `OnMapGenerated`**
Subscribe in `Awake`, not `Start`. See [Section 9 — Events](#events) for the execution order explanation.

**`DungeonGeneratorComponent.LastMap` is null**
`LastMap` is `null` until the first `Generate()` call. Do not read it in `Awake` — use the `OnMapGenerated` event instead.

**`PathCells` is always empty**
Enable **Compute Shortest Path** on `DungeonGeneratorComponent`. It is off by default.

**`FloorCells` does not contain the spawn or exit tile**
This is correct. Spawn and exit have their own tile types. Combine manually if needed — see the worked example in [Section 10](#10-worked-examples).

**Generation throws an exception / fails after all retries**
Increase **Max Regeneration Attempts** on `DungeonGeneratorComponent`. WFC in particular can need more attempts on small maps (below ~30×30). The default is 10; try 20.

**Walls are not solid — player walks through them**
Add **Tilemap Collider 2D** to the Tilemap GameObject. Also ensure your player prefab has a **Rigidbody2D** and a collider. See [Section 11 — Wall Collisions](#11-wall-collisions).

**The Level Designer window does not appear**
Confirm the package is installed correctly — check Package Manager for **Dynamic Dungeon Generator**. If it is listed but the menu is missing, try **Assets → Reimport All**.

**Saved level loads a different map than the preview**
This should not happen. `DungeonLevelData` stores the exact seed used during the preview, and the generator is deterministic — the same seed and settings always produce the same map. If they differ, check that you are not accidentally calling `Generate()` after `LoadFrom()` with `Seed = 0`.
