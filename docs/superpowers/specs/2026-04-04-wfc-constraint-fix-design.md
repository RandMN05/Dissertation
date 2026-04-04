# WFC Constraint Fix Design

**Date:** 2026-04-04  
**File:** `DynamicDungeon.Core/Algorithms/WaveFunctionCollapse.cs`  
**Status:** Approved

---

## Problem

The current WFC implementation degenerates to random fill because the adjacency rules
allow all `{Wall, Floor}` combinations for both tile types. Constraint propagation
(`Propagate`) never prunes a candidate, so the collapse loop is equivalent to calling
`rng.Next()` for every cell. The "WFC" label is misleading — no pattern constraints
are being enforced.

Root cause: a binary `{Wall, Floor}` tile set with symmetric, fully-permissive rules
cannot express structural constraints. Both tiles allow both tiles as neighbours, so
intersection with the allowed set never reduces the candidate set.

---

## Goal

Fix `WaveFunctionCollapse` so that:

1. Constraint propagation genuinely prunes candidates during collapse.
2. The output has a structurally distinct aesthetic from Cellular Automata: open floor
   regions separated by multi-cell-wide wall masses (rooms with thick walls).
3. Biome differentiation is retained via weighted tile frequencies.
4. The implementation follows the canonical WFC algorithm (Gumin) including Shannon
   entropy for lowest-entropy cell selection.
5. Zero impact outside `WaveFunctionCollapse.cs` — no changes to `Tile` enum, `TileMap`,
   or any other class.

---

## Design

### Internal tile type

Add a `private enum WfcTile { Floor, Wall, WallInterior }` inside `WaveFunctionCollapse`.

`WallInterior` is an internal WFC concept only. At TileMap construction:
- `WfcTile.Floor` → `Tile.Floor`
- `WfcTile.Wall` → `Tile.Wall`
- `WfcTile.WallInterior` → `Tile.Wall`

The superposition grid becomes `HashSet<WfcTile>[,]`.

### Adjacency rules (universal across biomes)

```
Floor        → { Floor, Wall }
Wall         → { Floor, Wall, WallInterior }
WallInterior → { Wall, WallInterior }
```

**Key constraint:** `WallInterior` cannot be adjacent to `Floor` in either direction.

- When a cell collapses to `Floor`: `WallInterior` is pruned from all 4 neighbours.
- When a cell collapses to `WallInterior`: `Floor` is pruned from all 4 neighbours.
- `Wall` is the legal buffer — it can appear next to either type.

This enforces a `Floor ← Wall → WallInterior` layering. Open rooms (Floor) are always
separated from solid wall interiors (WallInterior) by at least one edge Wall cell.
Propagation now cascades genuinely: a Floor cell at position A constrains its neighbours,
which constrain their neighbours, and so on.

Biome rules are now universal. Biome differentiation moves entirely to tile weights.

### Weighted collapse

Replace `options[rng.Next(options.Count)]` with a weighted draw using per-biome frequencies:

| Biome   | Floor% | Wall% | WallInterior% |
|---------|--------|-------|---------------|
| Dungeon | 35     | 25    | 40            |
| Cave    | 55     | 15    | 30            |
| Ruins   | 50     | 20    | 30            |

Dungeon: tight rooms with heavy walls.  
Cave: more open space, thinner wall layers.  
Ruins: open courtyards with scattered wall clusters.

### Shannon entropy (canonical WFC)

Replace count-based entropy proxy with true Shannon entropy:

```
H(cell) = -Σ_{t ∈ candidates} p_t · ln(p_t)
         where p_t = weight(t) / Σ_{t' ∈ candidates} weight(t')
```

`PickLowestEntropy` selects the uncollapsed cell with the minimum `H`. Ties broken
randomly. This matches Gumin's original WFC specification.

### What does not change

- `Propagate` logic (AC-3 style stack) — unchanged, just parameterised on `WfcTile`
- `EnsureConnectivity` — unchanged (works on `TileMap` after WfcTile→Tile mapping)
- `FindLargestFloorRegion`, `PlaceSpawnAndExit`, `FindFloorTile`, `PlaceEnemies` — all unchanged
- 20-attempt retry loop + CA fallback — unchanged
- Border cells forced to `WfcTile.Wall` — unchanged

---

## Changes Summary

| What | Change |
|------|--------|
| `WfcTile` enum | Add (private, internal to class) |
| `BuildRules()` | Remove `BiomeType` param; return `Dictionary<WfcTile, HashSet<WfcTile>>` |
| `GetWeights(BiomeType)` | Add — returns `(int floor, int wall, int wallInterior)` |
| `PickWeighted(...)` | Add — weighted random draw from candidate list |
| `PickLowestEntropy(...)` | Update — Shannon entropy `H = -Σ p ln p` instead of count |
| `TryGenerate(...)` | Add `weights` param; superposition type `HashSet<WfcTile>[,]`; TileMap mapping updated |
| `Generate(...)` | Pass `GetWeights(p.Biome)` to `TryGenerate` |
| All other methods | Unchanged |

---

## Unchanged interfaces

`WaveFunctionCollapse` still implements `IMapAlgorithm` and `Generate(GenerationParameters)`
returns `TileMap`. No callers need updating.
