# Unity Verification Checklist

This checklist guides manual verification of the Unity integration for the DynamicDungeon library. Complete these steps in the Unity Editor after building the Core library and copying the DLL to the package.

## Prerequisites

Before starting this checklist:

1. Build the Core library:
   ```bash
   dotnet build DynamicDungeon.Core/DynamicDungeon.Core.csproj
   ```

2. Rebuild the Release DLL and copy it to the Unity package:
   ```bash
   dotnet publish DynamicDungeon.Core/DynamicDungeon.Core.csproj -c Release -o DynamicDungeon.Core/publish
   cp DynamicDungeon.Core/publish/DynamicDungeon.Core.dll DynamicDungeon.Unity/Runtime/Plugins/DynamicDungeon.Core.dll
   ```

3. Open a Unity 6+ project with DynamicDungeon as a package (via Package Manager → Add package from disk → `package.json`)

## Verification Steps

### 1. Open a Generated Level Scene

- [ ] In the Project Browser, navigate to a generated level scene
- [ ] Double-click to open it in the Scene view
- [ ] Confirm the scene loads without errors

### 2. Enter Play Mode and Verify Basic Functionality

- [ ] Press Play in the Editor toolbar
- [ ] Observe the map generates automatically (the DungeonGeneratorComponent should call Generate() at startup)
- [ ] Confirm the player/agent spawns at the spawn position
- [ ] Confirm enemies spawn at their designated positions
- [ ] Verify no errors or warnings appear in the Console
- [ ] Exit Play Mode

### 3. Add Temporary Test MonoBehaviour

Create a temporary test script to verify the public API returns sensible values:

**Step 3a:** Create a new C# script called `MapVerifier.cs` in Assets/

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
            Debug.Log($"IsFloor(SpawnCell): {m.IsFloor(m.SpawnCell)}");
            Debug.Log($"IsWall at (0,0): {m.IsWall(new UnityEngine.Vector3Int(0, 0, 0))}");
        };
    }
}
```

**Step 3b:** Attach this script to any GameObject in the scene

**Step 3c:** Enter Play Mode

### 4. Verify Console Output

In the Console window, verify all 8 of these log lines appear with sensible values:

- [ ] `SpawnWorld: (X, Y, Z)` — spawn position in world coordinates (should be a Vector3)
- [ ] `ExitWorld: (X, Y, Z)` — exit position in world coordinates (should be a Vector3)
- [ ] `Floor tiles: N` — floor tile count (should be > 0 and reasonable for map size)
- [ ] `Wall tiles: N` — wall tile count (should be > 0)
- [ ] `Enemy count: N` — number of enemies placed (should match difficulty setting)
- [ ] `Random floor: (X, Y, Z)` — a random floor position (should be a Vector3)
- [ ] `IsFloor(SpawnCell): True` — confirming spawn point is walkable
- [ ] `IsWall at (0,0): True or False` — wall state at boundary (sensible value)
- [ ] **No errors or exceptions** in the Console

### 5. Verify Inspector Events Section

- [ ] In the Hierarchy, select the GameObject with the Tilemap component
- [ ] In the Inspector, find the **DungeonGeneratorComponent** section
- [ ] Scroll down to find **Events** or **On Generation Complete** section
- [ ] Confirm the `OnMapGenerated` UnityEvent is visible
- [ ] Verify the MapVerifier callback is listed as a listener (if Step 3b was completed)

### 6. Cleanup

- [ ] Exit Play Mode
- [ ] Delete the temporary `MapVerifier.cs` script from Assets/
- [ ] Delete the MapVerifier component from any GameObjects it was attached to
- [ ] Save the scene (Ctrl+S or Cmd+S)

## Success Criteria

All of the following must be true:

1. All 8 Console log lines in Step 4 printed with sensible values
2. No errors, exceptions, or stack traces in the Console
3. The `OnMapGenerated` UnityEvent is visible in the Inspector
4. The map renders visually in the Scene view with correct player/enemy spawn positions
5. Biome and difficulty settings affect the generated map (observe differences between runs with different settings)

## Troubleshooting

| Issue | Solution |
|-------|----------|
| DLL not found or version mismatch | Verify the DLL was copied to `DynamicDungeon.Unity/Runtime/Plugins/` and Unity reimported it (wait 2-3s) |
| `OnMapGenerated` callback never fires | Check that DungeonGeneratorComponent.Generate() is being called (add breakpoint or log) |
| Spawn/Exit not visible | Confirm the Tilemap has TileBase assignments for Spawn and Exit in the Inspector |
| Enemies not spawning | Check that enemy count for difficulty level > 0 (refer to EnemyCountByDifficulty array) |
| Maps are disconnected (exit unreachable) | This is normal during retry loops; if it persists, regenerate with a different seed |

## Notes

- The verification is manual because Unity Editor integration testing requires Editor mode access, which is difficult to automate in CI/CD.
- The MapVerifier script tests the public developer API surface, not the Core algorithm internals (those are tested by dotnet test).
- If any step fails, check the CLAUDE.md file for build/publish commands and ensure the DLL is current.
