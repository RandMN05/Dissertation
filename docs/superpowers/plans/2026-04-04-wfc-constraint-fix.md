# WFC Constraint Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `WaveFunctionCollapse` so adjacency constraints genuinely prune candidates during collapse, producing rooms with thick walls instead of random noise.

**Architecture:** Introduce a private `WfcTile { Floor, Wall, WallInterior }` enum used only during collapse. The rule `WallInterior cannot be adjacent to Floor` forces a `Floor ← Wall → WallInterior` layering. Both wall types map to `Tile.Wall` on output. Biome density is controlled via per-biome tile weights used in both Shannon-entropy-based cell selection and weighted random collapse.

**Tech Stack:** C# / netstandard2.1, xUnit 2.x, .NET 10 SDK.

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `DynamicDungeon.Core/Algorithms/WaveFunctionCollapse.cs` | Modify | Full WFC rewrite — WfcTile, rules, weights, Shannon entropy, weighted collapse |
| `DynamicDungeon.Core.Tests/DynamicDungeon.Core.Tests.csproj` | Create | xUnit test project |
| `DynamicDungeon.Core.Tests/WaveFunctionCollapseTests.cs` | Create | Behavioural tests for WFC output properties |

---

### Task 1: Create xUnit test project

**Files:**
- Create: `DynamicDungeon.Core.Tests/DynamicDungeon.Core.Tests.csproj`
- Modify: `DynamicDungeon.Core/DynamicDungeon.Core.csproj`

- [ ] **Step 1: Scaffold the test project**

```bash
cd "C:\Users\nessi\Documents\Projects\Dissertation"
dotnet new xunit -n DynamicDungeon.Core.Tests -f net10.0 --output DynamicDungeon.Core.Tests
dotnet add DynamicDungeon.Core.Tests reference DynamicDungeon.Core
```

Expected: `DynamicDungeon.Core.Tests/` directory created with a `.csproj` and `UnitTest1.cs`.

- [ ] **Step 2: Verify the generated .csproj looks like this (edit if needed)**

`DynamicDungeon.Core.Tests/DynamicDungeon.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DynamicDungeon.Core\DynamicDungeon.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Delete the scaffolded placeholder test file**

```bash
rm DynamicDungeon.Core.Tests/UnitTest1.cs
```

- [ ] **Step 4: Run the empty test suite to verify the project builds**

```bash
dotnet test DynamicDungeon.Core.Tests
```

Expected: `No test is available` or `Test Run Successful. Total tests: 0`.

- [ ] **Step 5: Commit**

```bash
git add DynamicDungeon.Core.Tests/
git commit -m "test: add DynamicDungeon.Core.Tests xUnit project"
```

---

### Task 2: Write failing behavioural tests

**Files:**
- Create: `DynamicDungeon.Core.Tests/WaveFunctionCollapseTests.cs`

- [ ] **Step 1: Create the test file**

`DynamicDungeon.Core.Tests/WaveFunctionCollapseTests.cs`:
```csharp
using System.Collections.Generic;
using DynamicDungeon.Core.Algorithms;
using DynamicDungeon.Core.Models;
using DynamicDungeon.Core.Pathfinding;
using Xunit;

namespace DynamicDungeon.Core.Tests
{
    public class WaveFunctionCollapseTests
    {
        private static GenerationParameters Params(BiomeType biome, int seed, int size = 40) =>
            new GenerationParameters
            {
                Algorithm  = AlgorithmType.WaveFunctionCollapse,
                Width      = size,
                Height     = size,
                Seed       = seed,
                Biome      = biome,
                Difficulty = DifficultyLevel.Easy,
            };

        [Fact]
        public void Generate_ReturnsMapWithCorrectDimensions()
        {
            var map = new WaveFunctionCollapse().Generate(Params(BiomeType.Dungeon, 42));
            Assert.Equal(40, map.Width);
            Assert.Equal(40, map.Height);
        }

        [Theory]
        [InlineData(BiomeType.Dungeon, 1)]
        [InlineData(BiomeType.Cave,    2)]
        [InlineData(BiomeType.Ruins,   3)]
        public void Generate_ExitIsReachableFromSpawn(BiomeType biome, int seed)
        {
            var map      = new WaveFunctionCollapse().Generate(Params(biome, seed));
            var reachable = FloodFill.GetReachable(map, map.SpawnPoint.x, map.SpawnPoint.y);
            Assert.Contains(map.ExitPoint, reachable);
        }

        [Fact]
        public void Generate_IsDeterministicForSameSeed()
        {
            var p    = Params(BiomeType.Dungeon, 99);
            var map1 = new WaveFunctionCollapse().Generate(p);
            var map2 = new WaveFunctionCollapse().Generate(p);

            for (int x = 0; x < map1.Width; x++)
            for (int y = 0; y < map1.Height; y++)
                Assert.Equal(map1.Get(x, y), map2.Get(x, y));
        }

        // This test documents the biome density contract: Cave must produce more open
        // space than Dungeon because Cave tiles carry 55% floor weight vs Dungeon's 35%.
        // It FAILS before the fix (current WFC ignores biome in its random selection).
        [Fact]
        public void Generate_CaveBiome_HasMoreFloorTilesThanDungeonBiome()
        {
            static int FloorCount(TileMap m)
            {
                int count = 0;
                for (int x = 1; x < m.Width  - 1; x++)
                for (int y = 1; y < m.Height - 1; y++)
                    if (m.Get(x, y) != Tile.Wall) count++;
                return count;
            }

            // Average over several seeds so EnsureConnectivity carving doesn't skew a single run.
            int caveTotal = 0, dungeonTotal = 0;
            for (int s = 1; s <= 5; s++)
            {
                caveTotal    += FloorCount(new WaveFunctionCollapse().Generate(Params(BiomeType.Cave,    s * 13, 50)));
                dungeonTotal += FloorCount(new WaveFunctionCollapse().Generate(Params(BiomeType.Dungeon, s * 13, 50)));
            }

            Assert.True(caveTotal > dungeonTotal,
                $"Cave avg floor ({caveTotal / 5}) should exceed Dungeon avg floor ({dungeonTotal / 5})");
        }
    }
}
```

- [ ] **Step 2: Run tests and confirm the density test fails**

```bash
dotnet test DynamicDungeon.Core.Tests --verbosity normal
```

Expected:
- `Generate_ReturnsMapWithCorrectDimensions` → PASS
- `Generate_ExitIsReachableFromSpawn` (×3) → PASS
- `Generate_IsDeterministicForSameSeed` → PASS
- `Generate_CaveBiome_HasMoreFloorTilesThanDungeonBiome` → **FAIL**

The density test fails because the current WFC uses uniform random selection (`rng.Next(options.Count)`), ignoring biome weights.

- [ ] **Step 3: Commit the failing tests**

```bash
git add DynamicDungeon.Core.Tests/WaveFunctionCollapseTests.cs
git commit -m "test: add failing WFC density and correctness tests"
```

---

### Task 3: Rewrite WaveFunctionCollapse — types, rules, weights, collapse

**Files:**
- Modify: `DynamicDungeon.Core/Algorithms/WaveFunctionCollapse.cs`

- [ ] **Step 1: Replace the top of the file — add WfcTile enum, BuildRules, GetWeights, TileWeight**

Replace everything from the opening `using` lines down to and including the old `BuildRules(BiomeType biome)` method with the following. Leave all methods from `EnsureConnectivity` onwards **completely unchanged**.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Algorithms
{
    // Wave Function Collapse: each cell starts in superposition over {Floor, Wall, WallInterior}.
    //
    // Key constraint: WallInterior cannot be adjacent to Floor.
    // This enforces a  Floor ← Wall → WallInterior  layering:
    //   open rooms (Floor) are always separated from solid wall mass (WallInterior)
    //   by at least one edge-wall (Wall) cell — i.e. rooms have thick walls.
    //
    // WallInterior is an internal WFC concept; both Wall and WallInterior map to
    // Tile.Wall in the final output. Nothing outside this class knows about WfcTile.
    public class WaveFunctionCollapse : IMapAlgorithm
    {
        private static readonly (int dx, int dy)[] Directions =
            { (0, 1), (0, -1), (1, 0), (-1, 0) };

        private static readonly int[] EnemyCountByDifficulty = { 3, 6, 12 };

        // ── Internal tile states ──────────────────────────────────────────────
        // Using a private enum keeps WFC internals out of the game model (Tile enum).
        private enum WfcTile { Floor, Wall, WallInterior }

        // Universal adjacency rules.  The single asymmetry that does real work:
        //   WallInterior.adj does NOT contain Floor, and Floor.adj does NOT contain WallInterior.
        // Consequence: collapsing to Floor prunes WallInterior from neighbours;
        //              collapsing to WallInterior prunes Floor from neighbours.
        //              Wall is the legal buffer between them.
        private static Dictionary<WfcTile, HashSet<WfcTile>> BuildRules() =>
            new Dictionary<WfcTile, HashSet<WfcTile>>
            {
                [WfcTile.Floor]        = new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall },
                [WfcTile.Wall]         = new HashSet<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior },
                [WfcTile.WallInterior] = new HashSet<WfcTile> { WfcTile.Wall,  WfcTile.WallInterior },
            };

        // Per-biome tile weights (integer percentages; need not sum to 100 — only ratios matter).
        // Higher floor weight → more open space in the final map.
        private static (int floor, int wall, int wallInterior) GetWeights(BiomeType biome) =>
            biome switch
            {
                BiomeType.Cave  => (55, 15, 30),  // Open chambers, thin rock walls
                BiomeType.Ruins => (50, 20, 30),  // Open courtyards, scattered wall clusters
                _               => (35, 25, 40),  // Dungeon: tight rooms, thick structural walls
            };

        private static int TileWeight(WfcTile t, (int floor, int wall, int wallInterior) w) =>
            t switch
            {
                WfcTile.Floor        => w.floor,
                WfcTile.Wall         => w.wall,
                WfcTile.WallInterior => w.wallInterior,
                _                    => 0,
            };
```

- [ ] **Step 2: Replace Generate() and TryGenerate()**

Replace the old `Generate` and `TryGenerate` methods with:

```csharp
        public TileMap Generate(GenerationParameters p)
        {
            var rules   = BuildRules();
            var weights = GetWeights(p.Biome);
            var pool    = new List<WfcTile> { WfcTile.Floor, WfcTile.Wall, WfcTile.WallInterior };

            for (int attempt = 0; attempt < 20; attempt++)
            {
                var result = TryGenerate(p, rules, pool, weights, p.Seed + attempt);
                if (result != null) return result;
            }

            // Fallback: delegate to Cellular Automata if WFC can't converge.
            return new CellularAutomata().Generate(p);
        }

        private static TileMap? TryGenerate(
            GenerationParameters p,
            Dictionary<WfcTile, HashSet<WfcTile>> rules,
            List<WfcTile> pool,
            (int floor, int wall, int wallInterior) weights,
            int seed)
        {
            var rng = new Random(seed);
            int w = p.Width, h = p.Height;

            // Superposition: interior cells start with all three candidates.
            // Border cells are pre-collapsed to Wall (keeps maps enclosed).
            var superposition = new HashSet<WfcTile>[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                superposition[x, y] = (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    ? new HashSet<WfcTile> { WfcTile.Wall }
                    : new HashSet<WfcTile>(pool);
            }

            // Collapse loop.
            while (true)
            {
                var cell = PickLowestEntropy(superposition, w, h, weights, rng);
                if (cell == null) break; // All cells collapsed — done.

                var (cx, cy) = cell.Value;
                var options  = superposition[cx, cy].ToList();
                if (options.Count == 0) return null; // Contradiction.

                var chosen = PickWeighted(options, weights, rng);
                superposition[cx, cy] = new HashSet<WfcTile> { chosen };

                if (!Propagate(superposition, rules, w, h, cx, cy))
                    return null; // Propagation reached contradiction.
            }

            // Map WfcTile → Tile.  Both wall types become Tile.Wall.
            var map = new TileMap(w, h);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                map.Set(x, y, superposition[x, y].First() == WfcTile.Floor ? Tile.Floor : Tile.Wall);

            EnsureConnectivity(map);
            PlaceSpawnAndExit(map);
            PlaceEnemies(map, rng, EnemyCountByDifficulty[(int)p.Difficulty]);

            return map;
        }
```

- [ ] **Step 3: Replace PickLowestEntropy() — leave a TODO stub for Shannon entropy**

Replace the old `PickLowestEntropy` with:

```csharp
        // Selects the uncollapsed cell with the lowest Shannon entropy H = -Σ p_i ln(p_i).
        // Collapsing lowest-entropy cells first is the canonical WFC heuristic — it minimises
        // the chance that a forced constraint propagates back to invalidate earlier decisions.
        private static (int x, int y)? PickLowestEntropy(
            HashSet<WfcTile>[,] sp, int w, int h,
            (int floor, int wall, int wallInterior) weights,
            Random rng)
        {
            double minEntropy = double.MaxValue;
            var candidates = new List<(int x, int y)>();

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (sp[x, y].Count <= 1) continue; // Already collapsed.

                double entropy = ShannonEntropy(sp[x, y], weights);
                if (entropy < minEntropy - 1e-9)
                {
                    minEntropy = entropy;
                    candidates.Clear();
                }
                if (Math.Abs(entropy - minEntropy) < 1e-9)
                    candidates.Add((x, y));
            }

            return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
        }

        // TODO: implement ShannonEntropy — see Task 4.
        private static double ShannonEntropy(
            HashSet<WfcTile> candidates,
            (int floor, int wall, int wallInterior) weights)
        {
            throw new NotImplementedException();
        }
```

- [ ] **Step 4: Add PickWeighted()**

Add after `ShannonEntropy`:

```csharp
        // Weighted random tile selection.  Respects biome frequency targets so the
        // collapse tends toward the intended floor/wall ratio regardless of neighbourhood.
        private static WfcTile PickWeighted(
            List<WfcTile> options,
            (int floor, int wall, int wallInterior) weights,
            Random rng)
        {
            int total = options.Sum(t => TileWeight(t, weights));
            if (total == 0) return options[rng.Next(options.Count)];

            int roll = rng.Next(total);
            int cum  = 0;
            foreach (var t in options)
            {
                cum += TileWeight(t, weights);
                if (roll < cum) return t;
            }
            return options[options.Count - 1];
        }
```

- [ ] **Step 5: Replace Propagate() to use WfcTile**

Replace the old `Propagate` method (which used `HashSet<Tile>`) with:

```csharp
        // AC-3-style constraint propagation: after collapsing (cx, cy), remove candidates
        // from each neighbour that no remaining option at (cx, cy) would allow.
        // Cascades outward via a stack until stable or a cell reaches 0 candidates (contradiction).
        private static bool Propagate(
            HashSet<WfcTile>[,] sp,
            Dictionary<WfcTile, HashSet<WfcTile>> rules,
            int w, int h, int cx, int cy)
        {
            var stack = new Stack<(int x, int y)>();
            stack.Push((cx, cy));

            while (stack.Count > 0)
            {
                var (ox, oy) = stack.Pop();
                foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    int nx = ox + dx, ny = oy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    // Build union of all tiles that any current candidate at (ox,oy) permits
                    // at (nx,ny).
                    var allowed = new HashSet<WfcTile>();
                    foreach (var t in sp[ox, oy])
                        if (rules.TryGetValue(t, out var adj))
                            allowed.UnionWith(adj);

                    int before = sp[nx, ny].Count;
                    sp[nx, ny].IntersectWith(allowed);

                    if (sp[nx, ny].Count == 0) return false; // Contradiction.
                    if (sp[nx, ny].Count < before)
                        stack.Push((nx, ny));   // This cell changed — propagate further.
                }
            }
            return true;
        }
```

- [ ] **Step 6: Verify the file compiles (ShannonEntropy throws but compiles)**

```bash
dotnet build DynamicDungeon.Core/DynamicDungeon.Core.csproj
```

Expected: build succeeds (no compiler errors; `NotImplementedException` is runtime, not compile-time).

---

### Task 4: Implement ShannonEntropy (your turn)

**File:** `DynamicDungeon.Core/Algorithms/WaveFunctionCollapse.cs`

The `ShannonEntropy` stub currently throws `NotImplementedException`. This is the mathematical heart of the WFC algorithm — replace the `throw` with the actual implementation.

**What it needs to do:**

For a cell whose remaining candidates are a subset of `{Floor, Wall, WallInterior}`, compute the Shannon entropy of the probability distribution over those candidates, where each tile's probability is proportional to its biome weight:

```
p(t) = weight(t) / Σ weight(t') for t' in candidates
H    = -Σ p(t) · ln(p(t))       for t  in candidates
```

**Constraints / hints:**
- Use `TileWeight(t, weights)` (already defined) to get each tile's weight.
- Sum the weights of *only the candidates present*, not all three tile types.
- If the total weight is 0 (all candidates have zero weight), return `0.0` — treat it as fully collapsed.
- `Math.Log` in C# computes the natural logarithm.
- The return value will be in the range `[0, ln(3)]` (roughly 0 to 1.099).

**Location in the file:** replace the `throw new NotImplementedException();` line inside `ShannonEntropy`.

Once implemented, run:

```bash
dotnet test DynamicDungeon.Core.Tests --verbosity normal
```

Expected after your implementation:
- All 6 tests pass including `Generate_CaveBiome_HasMoreFloorTilesThanDungeonBiome`.

- [ ] **Commit once all tests are green**

```bash
git add DynamicDungeon.Core/Algorithms/WaveFunctionCollapse.cs
git commit -m "feat: fix WFC — WfcTile constraints, weighted collapse, Shannon entropy cell selection"
```

---

### Task 5: Rebuild Release DLL for Unity

**Files:** `DynamicDungeon.Unity/Runtime/Plugins/DynamicDungeon.Core.dll`

- [ ] **Step 1: Publish Release build and copy DLL**

```bash
dotnet publish DynamicDungeon.Core/DynamicDungeon.Core.csproj -c Release -o DynamicDungeon.Core/publish
cp DynamicDungeon.Core/publish/DynamicDungeon.Core.dll DynamicDungeon.Unity/Runtime/Plugins/DynamicDungeon.Core.dll
```

Expected: no errors; `.dll` file timestamp updated.

- [ ] **Step 2: Verify console app still builds and runs**

```bash
dotnet run --project mapGenerationConsole -- --algorithm wfc --biome dungeon --difficulty medium --width 60 --height 40 --seed 42
```

Expected: coloured ASCII map rendered to console; `S` and `E` visible; no crashes.

- [ ] **Step 3: Spot-check the other two algorithms still work**

```bash
dotnet run --project mapGenerationConsole -- --algorithm ca    --biome cave   --seed 1
dotnet run --project mapGenerationConsole -- --algorithm perlin --biome ruins  --seed 2
```

Expected: both render without errors.

- [ ] **Step 4: Commit**

```bash
git add DynamicDungeon.Unity/Runtime/Plugins/DynamicDungeon.Core.dll
git commit -m "build: rebuild Release DLL after WFC fix"
```
