using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Unity.Editor
{
    /// <summary>
    /// Single authoring tool for DynamicDungeon levels.
    /// Open via: Window → DynamicDungeon → Level Designer
    ///
    /// Workflow:
    ///   1. Assign the DungeonGeneratorComponent from your scene.
    ///   2. Click "Add" to create new level slots.
    ///   3. Configure each level (settings + tile sprites).
    ///   4. Click Generate Preview — see the map in the scene view.
    ///   5. Repeat until happy, then click Save Level.
    ///   6. Save Level writes a DungeonLevelData asset AND a ready-to-use scene.
    ///   7. Open each generated scene, assign your Player and Enemy prefabs.
    ///   8. Use SceneManager.LoadScene("Level_02_...") to move between levels.
    /// </summary>
    public class LevelDesignerWindow : EditorWindow
    {
        private const string PrefSaveFolder = "DynamicDungeon_SaveFolder";

        [MenuItem("Window/DynamicDungeon/Level Designer")]
        public static void Open() => GetWindow<LevelDesignerWindow>("Level Designer");

        // ── Window state ───────────────────────────────────────────────────────

        private DungeonGeneratorComponent _generator;
        private string  _saveFolder;
        private int     _addCount  = 1;
        private Vector2 _scroll;

        private readonly List<LevelSlot> _slots = new List<LevelSlot>();

        // ── LevelSlot ──────────────────────────────────────────────────────────

        private class LevelSlot
        {
            public string          Name       = "Level";
            public AlgorithmType   Algorithm  = AlgorithmType.CellularAutomata;
            public BiomeType       Biome      = BiomeType.Dungeon;
            public DifficultyLevel Difficulty = DifficultyLevel.Medium;
            public int             Width      = 50;
            public int             Height     = 50;
            public int             Seed       = 0;

            // Tile sprites
            public TileBase WallTile;
            public TileBase FloorTile;
            public TileBase SpawnTile;
            public TileBase ExitTile;
            public TileBase EnemyTile;

            // Authoring state
            public bool             Generated    = false;
            public int              LastSeedUsed = 0;
            public long             LastGenMs    = 0;
            public int              LastEnemies  = 0;
            public bool             Saved        = false;
            public DungeonLevelData SavedAsset   = null;
            public bool             Foldout      = true;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _saveFolder = EditorPrefs.GetString(PrefSaveFolder, "Assets/DynamicDungeon/Levels");
            LoadSlotsFromDisk();
        }

        // ── OnGUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();
            DrawSeparator();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _slots.Count; i++)
                DrawSlot(i);
            EditorGUILayout.EndScrollView();

            DrawSeparator();
            DrawBottomButtons();
        }

        // ── Header ─────────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("DynamicDungeon  —  Level Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Add new levels
            EditorGUILayout.BeginHorizontal();
            _addCount = Mathf.Clamp(EditorGUILayout.IntField("Add New Levels", _addCount), 1, 50);
            if (GUILayout.Button("Add", GUILayout.Width(70)))
                AddNewSlots(_addCount);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Scene generator reference
            _generator = (DungeonGeneratorComponent)EditorGUILayout.ObjectField(
                new GUIContent("Scene Generator",
                    "The DungeonGeneratorComponent in your scene — used for Generate Preview."),
                _generator, typeof(DungeonGeneratorComponent), allowSceneObjects: true);

            if (_generator == null)
                EditorGUILayout.HelpBox(
                    "Assign the DungeonGeneratorComponent from your scene to enable Generate Preview.",
                    MessageType.Info);

            EditorGUILayout.Space(4);

            // Save folder
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _saveFolder = EditorGUILayout.TextField(
                new GUIContent("Save Folder", "Where level assets and scenes will be saved."),
                _saveFolder);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefSaveFolder, _saveFolder);

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                {
                    _saveFolder = "Assets" + picked.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString(PrefSaveFolder, _saveFolder);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
        }

        // ── Level slot ─────────────────────────────────────────────────────────

        private void DrawSlot(int index)
        {
            var slot = _slots[index];

            string status = slot.Saved     ? "✓ Saved"
                          : slot.Generated ? "● Previewed"
                          :                  "○ Not generated";
            string header = $"Level {index + 1}  —  {slot.Name}     [{status}]";

            // Manual foldout header row so we can place the × button inline
            EditorGUILayout.BeginHorizontal();
            slot.Foldout = EditorGUILayout.Foldout(slot.Foldout, header, toggleOnLabelClick: true,
                EditorStyles.foldoutHeader);

            if (slot.Saved)
            {
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(18)))
                    DeleteLevel(index);
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            if (!slot.Foldout)
                return;

            EditorGUI.indentLevel++;

            // Name
            slot.Name = EditorGUILayout.TextField("Name", slot.Name);

            EditorGUILayout.Space(4);

            // ── Generation Parameters ──────────────────────────────────────
            EditorGUILayout.LabelField("Generation Parameters", EditorStyles.boldLabel);

            slot.Algorithm  = (AlgorithmType)EditorGUILayout.EnumPopup("Algorithm",   slot.Algorithm);
            slot.Biome      = (BiomeType)EditorGUILayout.EnumPopup("Biome",           slot.Biome);
            slot.Difficulty = (DifficultyLevel)EditorGUILayout.EnumPopup("Difficulty", slot.Difficulty);

            EditorGUILayout.BeginHorizontal();
            slot.Width  = Mathf.Clamp(EditorGUILayout.IntField("Width",  slot.Width),  10, 200);
            slot.Height = Mathf.Clamp(EditorGUILayout.IntField("Height", slot.Height), 10, 200);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            slot.Seed = EditorGUILayout.IntField(
                new GUIContent("Seed", "0 = random each preview. The actual seed used is saved with the level."),
                slot.Seed);
            if (GUILayout.Button("Random", GUILayout.Width(70)))
                slot.Seed = 0;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // ── Tile Sprites ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Tile Sprites", EditorStyles.boldLabel);

            slot.WallTile  = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent("Wall"),  slot.WallTile,  typeof(TileBase), false);
            slot.FloorTile = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent("Floor"), slot.FloorTile, typeof(TileBase), false);
            slot.SpawnTile = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent("Spawn"), slot.SpawnTile, typeof(TileBase), false);
            slot.ExitTile  = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent("Exit"),  slot.ExitTile,  typeof(TileBase), false);
            slot.EnemyTile = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent("Enemy"), slot.EnemyTile, typeof(TileBase), false);

            EditorGUILayout.Space(6);

            // ── Action buttons ─────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_generator == null))
            {
                if (GUILayout.Button("Generate Preview", GUILayout.Height(28)))
                    GeneratePreview(index);
            }

            using (new EditorGUI.DisabledScope(!slot.Generated))
            {
                GUI.backgroundColor = slot.Generated ? new Color(0.4f, 0.85f, 0.4f) : Color.white;
                if (GUILayout.Button("Save Level", GUILayout.Height(28)))
                    SaveLevel(index);
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // ── Last Generation stats ──────────────────────────────────────
            if (slot.Generated)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Last Generation", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField(
                        new GUIContent("Seed Used", "This exact seed is saved with the level."),
                        slot.LastSeedUsed);
                    EditorGUILayout.LabelField("Time",    $"{slot.LastGenMs} ms");
                    EditorGUILayout.LabelField("Enemies", slot.LastEnemies.ToString());
                }
            }

            // ── Saved asset reference ──────────────────────────────────────
            if (slot.Saved && slot.SavedAsset != null)
            {
                EditorGUILayout.Space(2);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Saved Asset", slot.SavedAsset,
                        typeof(DungeonLevelData), false);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Bottom buttons ─────────────────────────────────────────────────────

        private void DrawBottomButtons()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_generator == null))
            {
                if (GUILayout.Button("Generate All Previews", GUILayout.Height(28)))
                    GenerateAll();
            }

            if (GUILayout.Button("Save All Levels", GUILayout.Height(28)))
                SaveAll();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete All Levels", GUILayout.Height(28)))
                DeleteAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
        }

        // ── Disk actions ───────────────────────────────────────────────────────

        /// Scans the save folder for existing DungeonLevelData assets and
        /// restores saved slots. Called on window open/recompile.
        private void LoadSlotsFromDisk()
        {
            // Keep any unsaved (in-progress) slots the user was working on
            var unsaved = _slots.Where(s => !s.Saved).ToList();
            _slots.Clear();

            if (!AssetDatabase.IsValidFolder(_saveFolder))
            {
                _slots.AddRange(unsaved);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:DungeonLevelData", new[] { _saveFolder });
            var saved = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<DungeonLevelData>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => AssetDatabase.GetAssetPath(a))
                .Select(data => new LevelSlot
                {
                    Name         = data.LevelName,
                    Algorithm    = data.Algorithm,
                    Biome        = data.Biome,
                    Difficulty   = data.Difficulty,
                    Width        = data.Width,
                    Height       = data.Height,
                    Seed         = data.Seed,
                    WallTile     = data.WallTile,
                    FloorTile    = data.FloorTile,
                    SpawnTile    = data.SpawnTile,
                    ExitTile     = data.ExitTile,
                    EnemyTile    = data.EnemyTile,
                    Generated    = true,
                    LastSeedUsed = data.Seed,
                    Saved        = true,
                    SavedAsset   = data,
                    Foldout      = false   // collapsed by default
                });

            _slots.AddRange(saved);
            _slots.AddRange(unsaved);
        }

        private void DeleteLevel(int index)
        {
            var slot = _slots[index];

            // Unsaved slot — just remove it, no confirmation needed
            if (!slot.Saved || slot.SavedAsset == null)
            {
                _slots.RemoveAt(index);
                return;
            }

            string assetPath   = AssetDatabase.GetAssetPath(slot.SavedAsset);
            string scenePath   = Path.ChangeExtension(assetPath, ".unity");
            string displayName = Path.GetFileNameWithoutExtension(assetPath);

            if (!EditorUtility.DisplayDialog(
                "Delete Level",
                $"Permanently delete {displayName}?\n\nThis removes both the scene and the data asset and cannot be undone.",
                "Delete", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(assetPath);
            if (File.Exists(Path.GetFullPath(scenePath)))
                AssetDatabase.DeleteAsset(scenePath);

            RemoveSceneFromBuildSettings(scenePath);
            _slots.RemoveAt(index);
            RechainScenes();
            AssetDatabase.Refresh();
        }

        private void DeleteAll()
        {
            if (!EditorUtility.DisplayDialog(
                "Delete All Levels",
                "Permanently delete ALL level scenes and data assets?\n\nThis cannot be undone.",
                "Delete All", "Cancel"))
                return;

            foreach (var slot in _slots.ToList())
            {
                if (slot.SavedAsset == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(slot.SavedAsset);
                string scenePath = Path.ChangeExtension(assetPath, ".unity");
                AssetDatabase.DeleteAsset(assetPath);
                if (File.Exists(Path.GetFullPath(scenePath)))
                    AssetDatabase.DeleteAsset(scenePath);
                RemoveSceneFromBuildSettings(scenePath);
            }

            _slots.Clear();
            AssetDatabase.Refresh();
        }

        /// After any delete, walk all remaining saved scenes and update their
        /// NextSceneName so the chain stays correct.
        private void RechainScenes()
        {
            var savedSlots = _slots.Where(s => s.Saved && s.SavedAsset != null).ToList();

            for (int i = 0; i < savedSlots.Count; i++)
            {
                string assetPath = AssetDatabase.GetAssetPath(savedSlots[i].SavedAsset);
                string scenePath = Path.ChangeExtension(assetPath, ".unity");

                if (!File.Exists(Path.GetFullPath(scenePath))) continue;

                string nextName = i + 1 < savedSlots.Count
                    ? Path.GetFileNameWithoutExtension(
                        AssetDatabase.GetAssetPath(savedSlots[i + 1].SavedAsset))
                    : "";

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                {
                    var bootstrapper = root.GetComponentInChildren<DungeonBootstrapper>();
                    if (bootstrapper == null) continue;
                    bootstrapper.NextSceneName = nextName;
                    EditorUtility.SetDirty(bootstrapper);
                    break;
                }
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ── Generation / save actions ──────────────────────────────────────────

        private void AddNewSlots(int count)
        {
            int startIndex = _slots.Count;
            for (int i = 0; i < count; i++)
                _slots.Add(new LevelSlot { Name = $"Level {startIndex + i + 1}" });
        }

        private void GeneratePreview(int index)
        {
            if (_generator == null) return;

            var slot = _slots[index];
            ApplySlotToGenerator(slot);
            _generator.Generate();

            slot.LastSeedUsed = _generator.LastSeedUsed;
            slot.LastGenMs    = _generator.LastGenerationMs;
            slot.LastEnemies  = _generator.EnemyCells.Count;
            slot.Generated    = true;
            slot.Saved        = false;

            var tilemap = _generator.GetComponent<Tilemap>();
            if (tilemap != null) EditorUtility.SetDirty(tilemap);
            SceneView.RepaintAll();
        }

        private void SaveLevel(int index)
        {
            var slot = _slots[index];
            if (!slot.Generated)
            {
                Debug.LogWarning($"[LevelDesigner] Level {index + 1} has not been previewed yet.");
                return;
            }

            EnsureFolderExists(_saveFolder);

            string safeName  = slot.Name.Replace(" ", "_");
            string baseName  = $"Level_{(index + 1):D2}_{safeName}";
            string assetPath = $"{_saveFolder}/{baseName}.asset";
            string scenePath = $"{_saveFolder}/{baseName}.unity";

            // ── Save DungeonLevelData asset ────────────────────────────────
            var data = AssetDatabase.LoadAssetAtPath<DungeonLevelData>(assetPath)
                       ?? CreateAndSave<DungeonLevelData>(assetPath);

            data.LevelName  = slot.Name;
            data.Algorithm  = slot.Algorithm;
            data.Biome      = slot.Biome;
            data.Difficulty = slot.Difficulty;
            data.Width      = slot.Width;
            data.Height     = slot.Height;
            data.Seed       = slot.LastSeedUsed;
            data.WallTile   = slot.WallTile;
            data.FloorTile  = slot.FloorTile;
            data.SpawnTile  = slot.SpawnTile;
            data.ExitTile   = slot.ExitTile;
            data.EnemyTile  = slot.EnemyTile;

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            slot.Saved      = true;
            slot.SavedAsset = data;

            // ── Create level scene ─────────────────────────────────────────
            CreateLevelScene(index, data, scenePath);

            // Single Refresh per level, after both the asset and scene are
            // fully written. Calling Refresh inside CreateLevelScene while
            // SaveAll loops over levels caused mid-sequence reimports that
            // broke serialised cross-asset references (GUID mismatches).
            AssetDatabase.Refresh();

            Debug.Log($"[LevelDesigner] Level {index + 1} saved → {assetPath} + {scenePath}  (seed {data.Seed})");
        }

        private void CreateLevelScene(int index, DungeonLevelData data, string scenePath)
        {
            // Re-fetch from the asset database to guarantee a live reference.
            // AssetDatabase operations between SaveLevel and here (e.g. SaveAssets)
            // can invalidate the in-memory ScriptableObject instance, causing
            // bootstrapper.LevelData to serialise as {fileID: 0} (null) in the scene.
            string dataPath = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(dataPath))
                data = AssetDatabase.LoadAssetAtPath<DungeonLevelData>(dataPath) ?? data;

            // Preserve player/enemy prefabs and camera size from an existing scene so
            // re-saving a level doesn't wipe the developer's manual assignments.
            GameObject existingPlayerPrefab = null;
            GameObject existingEnemyPrefab  = null;
            float      existingCameraSize   = 15f;
            if (File.Exists(Path.GetFullPath(scenePath)))
            {
                var existing = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                foreach (var root in existing.GetRootGameObjects())
                {
                    var bs = root.GetComponentInChildren<DungeonBootstrapper>();
                    if (bs != null) { existingPlayerPrefab = bs.PlayerPrefab; existingEnemyPrefab = bs.EnemyPrefab; }
                    var cam = root.GetComponentInChildren<Camera>();
                    if (cam != null) existingCameraSize = cam.orthographicSize;
                }
                EditorSceneManager.CloseScene(existing, true);
            }

            // Unity blocks NewScene(Additive) if any loaded scene is untitled (no path).
            // Instead of destroying the user's working scene with NewScene(Single), we give
            // every untitled scene a real path by saving it to a workspace file. This keeps
            // all scene objects (including _generator) alive and allows Additive mode.
            EnsureFolderExists(_saveFolder);
            int loadedCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int si = 0; si < loadedCount; si++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(si);
                if (string.IsNullOrEmpty(s.path))
                {
                    string workspacePath = $"{_saveFolder}/_preview_workspace.unity";
                    EditorSceneManager.SaveScene(s, workspacePath);
                    // _generator still points to the same component — only the path changed.
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObj, scene);
            var cam2d = cameraObj.AddComponent<Camera>();
            cam2d.orthographic     = true;
            cam2d.orthographicSize = existingCameraSize;
            cameraObj.transform.position = new Vector3(data.Width / 2f, data.Height / 2f, -10f);

            // Grid → Tilemap
            var gridObj = new GameObject("Grid");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gridObj, scene);
            gridObj.AddComponent<Grid>();

            var tilemapObj = new GameObject("Tilemap");
            tilemapObj.transform.SetParent(gridObj.transform);
            var tilemap   = tilemapObj.AddComponent<Tilemap>();
            tilemapObj.AddComponent<TilemapRenderer>();
            var generator = tilemapObj.AddComponent<DungeonGeneratorComponent>();

            generator.Algorithm  = data.Algorithm;
            generator.Biome      = data.Biome;
            generator.Difficulty = data.Difficulty;
            generator.Width      = data.Width;
            generator.Height     = data.Height;
            generator.Seed       = data.Seed;
            generator.WallTile   = data.WallTile;
            generator.FloorTile  = data.FloorTile;
            generator.SpawnTile  = data.SpawnTile;
            generator.ExitTile   = data.ExitTile;
            generator.EnemyTile  = data.EnemyTile;

            // Bake the map into the tilemap now so the scene shows the correct
            // layout in the editor without needing to enter Play mode.
            // At runtime DungeonBootstrapper re-generates from the same seed,
            // producing an identical map (deterministic), so editor and runtime views match.
            generator.Generate();
            EditorUtility.SetDirty(tilemap);
            EditorUtility.SetDirty(generator);

            // Bootstrapper
            var bootstrapObj = new GameObject("DungeonBootstrapper");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bootstrapObj, scene);
            var bootstrapper = bootstrapObj.AddComponent<DungeonBootstrapper>();

            bootstrapper.DungeonGenerator = generator;
            bootstrapper.LevelData        = data;
            bootstrapper.NextSceneName    = BuildNextSceneName(index);
            bootstrapper.PlayerPrefab     = existingPlayerPrefab;
            bootstrapper.EnemyPrefab      = existingEnemyPrefab;

            EditorSceneManager.SaveScene(scene, scenePath);
            // Re-fetch the handle by path: SaveScene with a new path can invalidate
            // the handle returned by NewScene, causing CloseScene to fail silently.
            var sceneToClose = EditorSceneManager.GetSceneByPath(scenePath);
            EditorSceneManager.CloseScene(sceneToClose.IsValid() ? sceneToClose : scene, true);
            AddSceneToBuildSettings(scenePath);
        }

        private string BuildNextSceneName(int currentIndex)
        {
            // Find the next saved slot or next slot in the list
            for (int i = currentIndex + 1; i < _slots.Count; i++)
            {
                var next = _slots[i];
                if (next.SavedAsset != null)
                    return Path.GetFileNameWithoutExtension(
                        AssetDatabase.GetAssetPath(next.SavedAsset));

                // Not yet saved — derive name from what it will be called
                string safeName = next.Name.Replace(" ", "_");
                return $"Level_{(i + 1):D2}_{safeName}";
            }
            return "";
        }

        private void GenerateAll()
        {
            for (int i = 0; i < _slots.Count; i++)
                GeneratePreview(i);
        }

        private void SaveAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Generated)
                    SaveLevel(i);
                else
                    Debug.LogWarning($"[LevelDesigner] Level {i + 1} skipped — generate a preview first.");
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplySlotToGenerator(LevelSlot slot)
        {
            _generator.Algorithm  = slot.Algorithm;
            _generator.Biome      = slot.Biome;
            _generator.Difficulty = slot.Difficulty;
            _generator.Width      = slot.Width;
            _generator.Height     = slot.Height;
            _generator.Seed       = slot.Seed;
            _generator.WallTile   = slot.WallTile;
            _generator.FloorTile  = slot.FloorTile;
            _generator.SpawnTile  = slot.SpawnTile;
            _generator.ExitTile   = slot.ExitTile;
            _generator.EnemyTile  = slot.EnemyTile;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RemoveSceneFromBuildSettings(string scenePath)
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(s => s.path != scenePath)
                .ToArray();
        }

        private static T CreateAndSave<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts   = folderPath.Split('/');
            string   current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
        }
    }
}
