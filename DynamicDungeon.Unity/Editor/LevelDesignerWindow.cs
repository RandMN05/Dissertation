using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Unity.Editor
{
    /// <summary>
    /// Multi-level authoring tool for DynamicDungeon.
    /// Open via: Window → DynamicDungeon → Level Designer
    ///
    /// Workflow:
    ///   1. Assign the DungeonGeneratorComponent from your scene.
    ///   2. Set how many levels you want and click Apply.
    ///   3. Configure each level, click Generate Preview to see it in the scene.
    ///   4. Repeat until you're happy with the map, then click Save Level.
    ///   5. Once all levels are saved, click Create LevelCollection Asset.
    ///   6. Drag the LevelCollection into your DungeonBootstrapper in the scene.
    /// </summary>
    public class LevelDesignerWindow : EditorWindow
    {
        [MenuItem("Window/DynamicDungeon/Level Designer")]
        public static void Open() => GetWindow<LevelDesignerWindow>("Level Designer");

        // ── State ──────────────────────────────────────────────────────────────

        private DungeonGeneratorComponent _generator;
        private string _saveFolder = "Assets/DynamicDungeon/Levels";
        private int    _pendingCount = 3;
        private Vector2 _scroll;

        private readonly List<LevelSlot> _slots = new List<LevelSlot>();

        // ── Slot data class ────────────────────────────────────────────────────

        private class LevelSlot
        {
            public string         Name       = "Level";
            public AlgorithmType  Algorithm  = AlgorithmType.CellularAutomata;
            public BiomeType      Biome      = BiomeType.Dungeon;
            public DifficultyLevel Difficulty = DifficultyLevel.Medium;
            public int            Width      = 50;
            public int            Height     = 50;
            public int            Seed       = 0;   // 0 = random on next preview

            // Runtime authoring state (not persisted)
            public bool            Generated    = false;
            public int             LastSeedUsed = 0;
            public bool            Saved        = false;
            public DungeonLevelData SavedAsset  = null;
            public bool            Foldout      = true;
        }

        // ── GUI ────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();
            DrawGeneratorField();
            DrawSaveFolderField();

            EditorGUILayout.Space(8);
            DrawSeparator();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _slots.Count; i++)
                DrawSlot(i);
            EditorGUILayout.EndScrollView();

            DrawSeparator();
            DrawBottomButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("DynamicDungeon  —  Level Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Level count row
            EditorGUILayout.BeginHorizontal();
            _pendingCount = EditorGUILayout.IntField("Number of Levels", _pendingCount);
            _pendingCount = Mathf.Clamp(_pendingCount, 1, 50);
            if (GUILayout.Button("Apply", GUILayout.Width(70)))
                ApplyLevelCount(_pendingCount);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGeneratorField()
        {
            EditorGUILayout.Space(4);
            _generator = (DungeonGeneratorComponent)EditorGUILayout.ObjectField(
                new GUIContent("Scene Generator",
                    "The DungeonGeneratorComponent in your scene — used for Generate Preview."),
                _generator, typeof(DungeonGeneratorComponent), allowSceneObjects: true);

            if (_generator == null)
                EditorGUILayout.HelpBox(
                    "Assign the DungeonGeneratorComponent from your scene to enable Generate Preview.",
                    MessageType.Info);
        }

        private void DrawSaveFolderField()
        {
            EditorGUILayout.BeginHorizontal();
            _saveFolder = EditorGUILayout.TextField(
                new GUIContent("Save Folder", "Where level assets will be written inside your project."),
                _saveFolder);

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                    _saveFolder = "Assets" + picked.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSlot(int index)
        {
            var slot = _slots[index];

            // Status label for the foldout header
            string status = slot.Saved      ? "✓ Saved"
                          : slot.Generated  ? "● Previewed — not saved"
                          :                   "○ Not generated";
            string header = $"Level {index + 1}  —  {slot.Name}     [{status}]";

            slot.Foldout = EditorGUILayout.BeginFoldoutHeaderGroup(slot.Foldout, header);
            if (slot.Foldout)
            {
                EditorGUI.indentLevel++;

                slot.Name       = EditorGUILayout.TextField("Name", slot.Name);
                slot.Algorithm  = (AlgorithmType)EditorGUILayout.EnumPopup("Algorithm", slot.Algorithm);
                slot.Biome      = (BiomeType)EditorGUILayout.EnumPopup("Biome", slot.Biome);
                slot.Difficulty = (DifficultyLevel)EditorGUILayout.EnumPopup("Difficulty", slot.Difficulty);

                EditorGUILayout.BeginHorizontal();
                slot.Width  = Mathf.Clamp(EditorGUILayout.IntField("Width",  slot.Width),  10, 200);
                slot.Height = Mathf.Clamp(EditorGUILayout.IntField("Height", slot.Height), 10, 200);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                slot.Seed = EditorGUILayout.IntField(
                    new GUIContent("Seed", "0 = pick a random seed on the next Generate Preview."),
                    slot.Seed);
                if (GUILayout.Button("Random", GUILayout.Width(70)))
                    slot.Seed = 0;
                EditorGUILayout.EndHorizontal();

                // Show the seed that was actually used after a preview
                if (slot.Generated)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.IntField(
                            new GUIContent("Seed Used", "This exact seed will be saved with the level."),
                            slot.LastSeedUsed);
                }

                EditorGUILayout.Space(6);

                // Action buttons
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

                if (slot.Saved && slot.SavedAsset != null)
                {
                    EditorGUILayout.Space(2);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Saved Asset", slot.SavedAsset, typeof(DungeonLevelData), false);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

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

            GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
            if (GUILayout.Button("Create LevelCollection Asset", GUILayout.Height(34)))
                CreateLevelCollection();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
        }

        // ── Actions ────────────────────────────────────────────────────────────

        private void ApplyLevelCount(int count)
        {
            while (_slots.Count < count)
                _slots.Add(new LevelSlot { Name = $"Level {_slots.Count + 1}" });
            while (_slots.Count > count)
                _slots.RemoveAt(_slots.Count - 1);
        }

        private void GeneratePreview(int index)
        {
            if (_generator == null) return;

            var slot = _slots[index];

            _generator.Algorithm  = slot.Algorithm;
            _generator.Biome      = slot.Biome;
            _generator.Difficulty = slot.Difficulty;
            _generator.Width      = slot.Width;
            _generator.Height     = slot.Height;
            _generator.Seed       = slot.Seed;

            _generator.Generate();

            slot.LastSeedUsed = _generator.LastSeedUsed;
            slot.Generated    = true;
            slot.Saved        = false; // new preview invalidates any previous save

            var tilemap = _generator.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (tilemap != null) EditorUtility.SetDirty(tilemap);
            SceneView.RepaintAll();
        }

        private void SaveLevel(int index)
        {
            var slot = _slots[index];
            if (!slot.Generated)
            {
                Debug.LogWarning($"[LevelDesigner] Level {index + 1} has not been previewed yet — generate it first.");
                return;
            }

            EnsureFolderExists(_saveFolder);

            string safeName = slot.Name.Replace(" ", "_");
            string assetPath = $"{_saveFolder}/Level_{(index + 1):D2}_{safeName}.asset";

            var data = AssetDatabase.LoadAssetAtPath<DungeonLevelData>(assetPath)
                       ?? CreateAndSaveAsset<DungeonLevelData>(assetPath);

            data.LevelName  = slot.Name;
            data.Algorithm  = slot.Algorithm;
            data.Biome      = slot.Biome;
            data.Difficulty = slot.Difficulty;
            data.Width      = slot.Width;
            data.Height     = slot.Height;
            data.Seed       = slot.LastSeedUsed; // always the actual seed, never 0

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            slot.Saved     = true;
            slot.SavedAsset = data;

            Debug.Log($"[LevelDesigner] Level {index + 1} saved → {assetPath}  (seed {data.Seed})");
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

        private void CreateLevelCollection()
        {
            var savedSlots = _slots.FindAll(s => s.SavedAsset != null);
            if (savedSlots.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Saved Levels",
                    "Save at least one level before creating a LevelCollection.",
                    "OK");
                return;
            }

            EnsureFolderExists(_saveFolder);
            string collectionPath = $"{_saveFolder}/LevelCollection.asset";

            var collection = AssetDatabase.LoadAssetAtPath<LevelCollection>(collectionPath)
                             ?? CreateAndSaveAsset<LevelCollection>(collectionPath);

            collection.Levels = savedSlots.ConvertAll(s => s.SavedAsset).ToArray();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(collection);
            Debug.Log($"[LevelDesigner] LevelCollection saved → {collectionPath}  ({collection.Levels.Length} levels)");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static T CreateAndSaveAsset<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
