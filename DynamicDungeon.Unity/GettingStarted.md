# DynamicDungeon — Getting Started

A step-by-step guide for brand new developers. By the end you will have a playable procedurally generated dungeon running in Unity with a moving player, chasing enemies, and a win/lose condition — and you'll know how to swap in your own game logic.

---

## Prerequisites

- **Unity 6** (version 6000.0 or newer) — the package will not work on earlier versions
- **Git** — to clone the repository

---

## Step 1 — Get the Package

Clone the repository from GitHub:

```bash
git clone https://github.com/<your-repo-url>/DynamicDungeon.git
```

You do not need to build anything. The compiled Core library (`DynamicDungeon.Core.dll`) is already included in the package.

---

## Step 2 — Install in Unity

1. Open your Unity 6 project (or create a new **2D** project).
2. Open **Package Manager**: `Window → Package Manager`.
3. Click the **+** button (top left) → **Add package from disk**.
4. Navigate to the cloned repository, open the `DynamicDungeon.Unity/` folder, and select `package.json`.
5. Unity will import the package. You should see **Dynamic Dungeon Generator** appear in the Package Manager list.

> You will see a new menu item: **Window → DynamicDungeon → Level Designer**. This confirms the install worked.

---

## Step 3 — Create Tile Sprites

The generator needs a Unity Tile asset for each of the five tile types: `WallTile`, `FloorTile`, `SpawnTile`, `ExitTile`, `EnemyTile`. Once you have sprites imported into your project:

1. In the Project window, create a folder `Assets/Tiles`.
2. Right-click inside it → **Create → 2D → Tiles → Tile**.
3. Create five tiles and name them: `WallTile`, `FloorTile`, `SpawnTile`, `ExitTile`, `EnemyTile`.
4. Select each tile asset and drag your sprite into the **Sprite** field in the Inspector.

> **Important:** Select your **Wall tile** and set its **Collider Type** to **Sprite**. Set all other tiles (Floor, Spawn, Exit, Enemy) to **None**. This ensures physics colliders only generate on wall tiles — see Step 9 for the full wall collision setup.

---

## Step 4 — Set Up Your Scene

You have two options. **Option A** (Level Designer) is recommended for most developers.

---

### Option A — Level Designer Window (recommended)

The Level Designer creates everything for you — a complete scene with all GameObjects wired up.

1. Open the Level Designer: **Window → DynamicDungeon → Level Designer**.
2. Click **Add** to create a new level slot.
3. Fill in the level settings (see [Generation Options](#generation-options) below).
4. Assign your five tile sprites in the **Tile Sprites** section.
5. Assign the **Scene Generator** field — drag the `DungeonGeneratorComponent` from your scene hierarchy. If you don't have one yet, create a new empty scene first, add a **Grid** GameObject, add a child **Tilemap** + **TilemapRenderer**, and attach **DungeonGeneratorComponent** to the Tilemap.
6. Click **Generate Preview** — the map appears in the Scene view.
7. Click **Save Level** — this creates a `.unity` scene file and a `.asset` data file and adds the scene to Build Settings automatically.
8. Open the generated scene. You will see a `DungeonBootstrapper` GameObject already wired up.

---

### Option B — Manual Scene Setup

If you want to build the scene by hand:

1. Create a new scene.
2. Create a **Grid** GameObject (`GameObject → 2D Object → Tilemap → Rectangular`). This creates a Grid with a child Tilemap automatically.
3. Select the **Tilemap** child object and add the **DungeonGeneratorComponent** component.
4. Assign your five tile sprites in the Inspector under **Tile Sprites**.
5. Create an empty GameObject called `DungeonBootstrapper` and add the **DungeonBootstrapper** component.
6. In the `DungeonBootstrapper` Inspector, assign:
   - **Dungeon Generator** → the `DungeonGeneratorComponent` on your Tilemap
7. Hit Play — the map generates automatically.

---

## Step 5 — Generation Options

These settings live on the `DungeonGeneratorComponent` in the Inspector.

### Algorithm

Controls how the map is generated. Each produces a distinct visual style.

| Algorithm | Description |
|---|---|
| **Cellular Automata** | Cave-like organic shapes. Random fill smoothed by neighbour rules. Fast and reliable. Good default. |
| **Perlin Noise** | Noise-based generation. Produces flowing, natural-looking layouts. |
| **Wave Function Collapse** | Constraint propagation. Produces structured, tile-rule-consistent maps. Most complex internally. |

### Biome

Tunes the density and feel of the generated map within the chosen algorithm.

| Biome | Description |
|---|---|
| **Dungeon** | Tight corridors, more walls, fewer open spaces. Classic dungeon feel. |
| **Cave** | Open caverns, more floor space, organic boundary shapes. |
| **Ruins** | Partially open, mix of corridors and open areas. |

### Difficulty

Controls enemy count and placement density.

| Difficulty | Description |
|---|---|
| **Easy** | Fewer enemies, more breathing room. |
| **Medium** | Balanced enemy placement. |
| **Hard** | More enemies, closer to the spawn point. |

### Other Settings

| Setting | Description |
|---|---|
| **Width / Height** | Map size in tiles. Range: 10–200. Larger maps take longer to generate. |
| **Seed** | Set to `0` for a random map every time. Set a specific number to reproduce the same map. The exact seed used is always saved with the level. |
| **Compute Shortest Path** | Enables A* pathfinding from Spawn to Exit. Accessible via `map.PathCells`. Useful for hint systems or difficulty scoring. Off by default. |
| **Max Regeneration Attempts** | If the generator produces an invalid map (exit not reachable from spawn), it retries up to this many times. Default: 10. |

---

## Step 6 — Add Player and Enemy Prefabs

The package includes two reference scripts: `PlayerController` and `EnemyController`. These are starting points — replace them with your own at any time.

### Create a Player Prefab

1. In the Hierarchy, right-click → **Create Empty**. Name it `Player`.
2. With it selected, click **Add Component** in the Inspector and add:
   - **Sprite Renderer** — in the Sprite field, click the small circle on the right and pick your player sprite, or drag a sprite from the Project window directly onto the field. Under **Additional Settings**, set **Order in Layer** to `1` so the player renders above the tilemap.
   - **Rigidbody 2D** — set **Gravity Scale** to `0`, tick **Freeze Rotation Z** under Constraints.
   - **Circle Collider 2D**
   - **Player Controller** (search for it in the Add Component menu)
3. Set the **Tag** dropdown at the top of the Inspector to `Player`.
4. Drag the GameObject from the Hierarchy into your `Assets/` folder to save it as a prefab. Delete the scene copy.

**PlayerController settings:**

| Field | Default | Description |
|---|---|---|
| `Speed` | `3` | Movement speed in units per second. WASD and arrow keys. |

### Create an Enemy Prefab

1. In the Hierarchy, right-click → **Create Empty**. Name it `Enemy`.
2. Add the following components:
   - **Sprite Renderer** — assign your enemy sprite the same way as the player. Set **Order in Layer** to `1`.
   - **Rigidbody 2D** — set **Gravity Scale** to `0`, tick **Freeze Rotation Z**.
   - **Circle Collider 2D**
   - **Enemy Controller** (from the Add Component menu)
3. Drag the GameObject into `Assets/` to save it as a prefab. Delete the scene copy.

**EnemyController settings:**

| Field | Default | Description |
|---|---|---|
| `Speed` | `1.5` | Chase speed. The enemy moves directly toward the player every frame. |

### Attach the Prefabs to the Bootstrapper

1. Open your level scene.
2. Select the **DungeonBootstrapper** GameObject in the Hierarchy.
3. Drag your **Player prefab** from the Project window into the **Player Prefab** slot in the Inspector.
4. Drag your **Enemy prefab** into the **Enemy Prefab** slot.

The Bootstrapper spawns the player at the spawn point and one enemy at every enemy marker automatically when the scene loads.

| Field | Description |
|---|---|
| **Next Scene Name** | Scene name of the next level — leave empty on the final level |
| **Win Distance** | How close the player must be to the exit to win (default `0.6`) |
| **Lose Distance** | How close an enemy must be to trigger game over (default `0.4`) |

---

## Step 7 — Play

Hit **Play**. The dungeon generates, your player spawns at the start, enemies spawn at their markers, and the game runs. Reach the exit to win. Get caught by an enemy to lose.

---

## Step 8 — Make It Your Own

The reference scripts (`PlayerController`, `EnemyController`, `DungeonBootstrapper`) are intentionally simple. You are not required to use them. To add your own game logic, subscribe to the `OnMapGenerated` event in any of your scripts:

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
        // Spawn player
        Instantiate(playerPrefab, map.SpawnWorld, Quaternion.identity);

        // Spawn enemies
        foreach (var pos in map.EnemyWorldPositions)
            Instantiate(enemyPrefab, pos, Quaternion.identity);

        // Place a collectible on a random floor tile
        Instantiate(coinPrefab, map.GetRandomFloorWorld(), Quaternion.identity);
    }
}
```

> **Always subscribe in `Awake`, not `Start`.** The map generates during `Start` — if you subscribe in `Start` you may miss the event.

See **[DeveloperGuide.md](DeveloperGuide.md)** for the full `DungeonMap` API reference, all worked examples, and common mistakes.

---

## Step 9 — Wall Collisions (optional)

By default the map has no physics colliders. See the **Wall Collisions** section in [DeveloperGuide.md](DeveloperGuide.md) for three ways to add them, including a zero-code Inspector approach.

---

## What You Can Build With This

Here are some ideas of what `DungeonMap` makes easy:

| Idea | How |
|---|---|
| Random item/loot drops | `map.GetRandomFloorWorld()` — call it multiple times |
| Custom enemy AI | Subscribe to `OnMapGenerated`, pass `map.FloorCells` to your pathfinding system |
| Minimap | Iterate `map.FloorCells` and `map.WallCells` to draw a minimap texture |
| Hint / path highlight | Enable `ComputeShortestPath`, iterate `map.PathCells` |
| Trap placement | Spawn traps on `map.FloorCells` away from `map.SpawnCell` |
| Difficulty scoring | Use `map.FloorCells.Count` vs `map.WallCells.Count` ratio as an openness metric |
| Grid-locked movement | Use `map.IsWall(cell)` to validate moves without physics |
| Procedural story beats | Place story triggers at `map.ExitCell` or random floor positions |

---

## Troubleshooting

**Map generates but nothing is visible**
→ Check that all five tile sprites are assigned on `DungeonGeneratorComponent`. A missing `WallTile` or `FloorTile` will leave those cells blank.

**Player/enemies don't spawn**
→ Check that `PlayerPrefab` and `EnemyPrefab` are assigned on `DungeonBootstrapper`.

**My script doesn't receive `OnMapGenerated`**
→ Make sure you subscribed in `Awake()`, not `Start()`. See the Execution Order section in [DeveloperGuide.md](DeveloperGuide.md).

**Generation fails / exception in Console**
→ Increase `MaxRegenerationAttempts` on `DungeonGeneratorComponent`. WFC in particular occasionally needs more attempts for small maps.

**Walls aren't solid / player walks through walls**
→ Add `Tilemap Collider 2D` to the Tilemap GameObject and `Rigidbody2D` to your player prefab. See Wall Collisions in [DeveloperGuide.md](DeveloperGuide.md).
