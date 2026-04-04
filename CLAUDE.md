# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

```bash
# Run the interactive console test app (prompts for algorithm, biome, etc.)
dotnet run --project mapGenerationConsole

# Run with CLI args (skips interactive prompt)
dotnet run --project mapGenerationConsole -- --algorithm ca --biome cave --difficulty hard --width 60 --height 40 --seed 42 --path

# Build everything
dotnet build

# Build the Core library only
dotnet build DynamicDungeon.Core/DynamicDungeon.Core.csproj

# Rebuild the Release DLL for the Unity package (run after changing Core)
dotnet publish DynamicDungeon.Core/DynamicDungeon.Core.csproj -c Release -o DynamicDungeon.Core/publish
cp DynamicDungeon.Core/publish/DynamicDungeon.Core.dll DynamicDungeon.Unity/Runtime/Plugins/DynamicDungeon.Core.dll
```

CLI args for `mapGenerationConsole`: `--algorithm [ca|perlin|wfc]`, `--biome [dungeon|cave|ruins]`, `--difficulty [easy|medium|hard]`, `--width N`, `--height N`, `--seed N`, `--path`.

## Architecture

The repo has three components:

### 1. `DynamicDungeon.Core` (netstandard2.1 class library → `.dll`)

The engine-agnostic core library. Everything Unity/console-specific is excluded.

**Data flow:** `MapGenerator.Generate(GenerationParameters)` → picks algorithm → generates `TileMap` → `MapValidator.IsValid()` (flood fill) → auto-regenerates if invalid (up to `MaxRegenerationAttempts`, each with seed+offset) → optional A\* path → returns `TileMap`.

Key types:

- `GenerationParameters` — all inputs (algorithm, biome, difficulty, width, height, seed, etc.)
- `TileMap` — the output; holds `Tile[,]` grid, `SpawnPoint`, `ExitPoint`, `ShortestPath`
- `Tile` enum — `Wall, Floor, Spawn, Exit, Enemy`
- `IMapAlgorithm` — interface implemented by all three generators

**Algorithms** (`Algorithms/`):

- `CellularAutomata` — random fill → Moore neighbourhood threshold smoothing (≥5 neighbours→wall, ≤2→floor) → spawn/exit/enemy placement
- `PerlinNoise` — seeded permutation table → 3-octave 2D noise → threshold to Wall/Floor → placement
- `WaveFunctionCollapse` — AC-3 constraint propagation on `{Floor, Wall, WallInterior}` superposition → 50-attempt retry loop discards disconnected maps → placement

**Pathfinding** (`Pathfinding/`):

- `FloodFill.GetReachable()` — BFS from spawn; used only for validation
- `AStar.FindPath()` — Manhattan heuristic, cardinal movement only, returns `List<(int x, int y)>`

**Validation** (`Validation/MapValidator.IsValid()`) — checks Exit is in FloodFill reachable set from Spawn.

Biome and difficulty tuning is done via static constants inside each algorithm class (fill %, noise threshold/scale, enemy count arrays, WFC adjacency rule dictionaries).

### 2. `mapGenerationConsole` (net10.0 executable)

ASCII test harness. References `DynamicDungeon.Core`. `Program.cs` contains all logic:

- With no CLI args → `PromptUser()` interactive menu
- With CLI args → `ParseArgs()` and generate immediately
- `Render()` writes coloured ASCII to console (`#` wall, `.` floor, `S` spawn, `E` exit, `M` enemy, `*` path)

### 3. `DynamicDungeon.Unity` (Unity Package Manager package)

Drop into any Unity 6+ project via **Package Manager → Add package from disk → `package.json`**.

- `Runtime/Plugins/DynamicDungeon.Core.dll` — the compiled core library
- `Runtime/DungeonGeneratorComponent.cs` — `MonoBehaviour` with `[RequireComponent(Tilemap)]`; exposes all `GenerationParameters` as serialized fields plus `TileBase` slots for each tile type; call `Generate()` at runtime or from the Editor
- `Editor/DungeonGeneratorEditor.cs` — custom Inspector: draws parameters, Randomize seed button, green **Generate Dungeon** button, post-generation stats panel

**Important:** After any change to `DynamicDungeon.Core`, rebuild the Release DLL and copy it to `DynamicDungeon.Unity/Runtime/Plugins/` (see commands above) before testing in Unity.

## Key Constraints

- `DynamicDungeon.Core` must remain **zero external dependencies** — no NuGet packages. The Perlin noise implementation is self-contained.
- Target `netstandard2.1` for the Core library (required for Unity 6 compatibility).
- WFC uses a `{Floor, Wall, WallInterior}` candidate pool during collapse; `Spawn`, `Exit`, `Enemy` are placed as post-process steps, never during constraint propagation.
- The solution file is `mapGenerationConsole.slnx` (new XML format, .NET 10 SDK default). VS Code's C# extension may struggle with `.slnx` — use the terminal for builds.
