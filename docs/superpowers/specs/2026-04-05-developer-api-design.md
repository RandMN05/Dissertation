# Developer API — Design Spec
**Date:** 2026-04-05
**Status:** Approved

## Goal

Expose the full generated map to game developers via a clean, self-contained result object (`DungeonMap`) delivered through an event, so developers can build any game mechanics they want without coupling their code to the generator internals.

---

## Architecture

Two new files. Minimal changes to existing code.

```
DynamicDungeon.Unity/
├── DeveloperGuide.md              ← NEW — markdown reference for developers (package root)
└── Runtime/
    ├── DungeonGeneratorComponent.cs   ← adds event firing + LastMap property
    ├── DungeonMap.cs                  ← NEW — rich result object
    └── (everything else unchanged)
```

### Changes to `DungeonGeneratorComponent`

After `ApplyToTilemap()` completes inside `Generate()`:

1. Build a `DungeonMap` from the tile data and the live `Tilemap` reference.
2. Store it as `public DungeonMap LastMap` on the component.
3. Fire `public event Action<DungeonMap> OnMapGenerated` — passes the map to all C# subscribers.
4. Fire `public UnityEvent OnGenerationComplete` — no parameters, Inspector-wirable.

`DungeonBootstrapper` is updated to subscribe to `OnMapGenerated` instead of reading `SpawnCell` / `EnemyCells` directly off the generator — demonstrating the new pattern.

---

## `DungeonMap` Class (`Runtime/DungeonMap.cs`)

A plain C# class. Constructed internally by `DungeonGeneratorComponent` after generation. Not serialized, not written to disk — lives in memory for the duration of the scene.

### Properties

| Member | Type | Description |
|---|---|---|
| `SpawnCell` | `Vector3Int` | Tilemap cell of the spawn point |
| `ExitCell` | `Vector3Int` | Tilemap cell of the exit |
| `EnemyCells` | `IReadOnlyList<Vector3Int>` | All enemy spawn cells |
| `FloorCells` | `IReadOnlyList<Vector3Int>` | All walkable floor cells |
| `WallCells` | `IReadOnlyList<Vector3Int>` | All wall cells |
| `PathCells` | `IReadOnlyList<Vector3Int>` | A* path cells (empty if `ComputeShortestPath` is false) |
| `SpawnWorld` | `Vector3` | World position of the spawn point |
| `ExitWorld` | `Vector3` | World position of the exit |
| `EnemyWorldPositions` | `IReadOnlyList<Vector3>` | World position of every enemy cell |
| `FloorWorldPositions` | `IReadOnlyList<Vector3>` | World position of every floor cell |
| `Tilemap` | `Tilemap` | The live Tilemap — for custom queries |
| `Width` | `int` | Map width in tiles |
| `Height` | `int` | Map height in tiles |
| `Seed` | `int` | The seed used to generate this map |

### Methods

| Method | Returns | Description |
|---|---|---|
| `GetRandomFloorCell()` | `Vector3Int` | A random floor tile cell |
| `GetRandomFloorWorld()` | `Vector3` | World position of a random floor tile |
| `IsFloor(Vector3Int cell)` | `bool` | True if the cell is a walkable floor tile |
| `IsWall(Vector3Int cell)` | `bool` | True if the cell is a wall tile |

`DungeonMap` takes a `System.Random` seeded from the map seed for `GetRandom*` methods — reproducible results when the same seed is used.

---

## Event System

### C# Event
```csharp
public event Action<DungeonMap> OnMapGenerated;
```
Fired synchronously at the end of `Generate()`. Passes the fully populated `DungeonMap`. Subscribers receive everything they need in one shot and have no further dependency on the generator.

### UnityEvent
```csharp
public UnityEvent OnGenerationComplete;
```
Fired immediately after `OnMapGenerated`. No parameters. Wired in the Inspector — useful for triggering UI, audio, or animations without code.

---

## Timing Contract

`DungeonBootstrapper.Start()` calls `Generate()`, which fires both events synchronously before returning.

Any developer script must subscribe in `Awake()` — this is guaranteed to run before any `Start()` in Unity's execution order, so the subscription is always in place before generation fires.

```csharp
// Correct
void Awake() => GetComponent<DungeonGeneratorComponent>().OnMapGenerated += OnMapReady;

// Risky — may miss the event depending on script execution order
void Start() => GetComponent<DungeonGeneratorComponent>().OnMapGenerated += OnMapReady;
```

---

## Deletion — No Impact

`DungeonMap` is a transient, in-memory object. It is never serialized to disk. The Level Designer's existing deletion logic (removes `.asset`, `.unity`, Build Settings entry, rechains scenes) requires no changes.

---

## Developer Guide (`DynamicDungeon.Unity/DeveloperGuide.md`)

Covers:
1. **Quick Start** — 3-step setup, copy-paste code block
2. **`DungeonMap` Reference** — full property and method table
3. **Worked Examples** — spawn player, place chest on random floor, custom enemy spawning, `IsFloor`/`IsWall` validation
4. **Inspector Wiring** — using `OnGenerationComplete` UnityEvent without code
5. **Execution Order** — `Awake` vs `Start`, why it matters
6. **Common Mistakes** — subscribing in `Start`, querying before generation, assuming `LastMap` is non-null before generation runs

---

## Out of Scope

- Saving `DungeonMap` to disk / persistence across scenes — out of scope; `DungeonLevelData` already handles level reproduction via seed.
- Streaming or async generation — not required; maps generate in milliseconds.
- Changes to Core library (`DynamicDungeon.Core`) — all changes are Unity-layer only.
