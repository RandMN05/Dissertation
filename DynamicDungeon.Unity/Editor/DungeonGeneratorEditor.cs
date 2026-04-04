using UnityEditor;
using UnityEngine;
using DynamicDungeon.Unity;

namespace DynamicDungeon.Unity.Editor
{
    /// <summary>
    /// Custom Inspector for DungeonGeneratorComponent.
    /// Draws all parameters with helpful labels, then shows a prominent
    /// Generate button and a stats readout from the last generation.
    /// </summary>
    [CustomEditor(typeof(DungeonGeneratorComponent))]
    public class DungeonGeneratorEditor : UnityEditor.Editor
    {
        // Generation parameter properties
        private SerializedProperty _algorithm;
        private SerializedProperty _biome;
        private SerializedProperty _difficulty;
        private SerializedProperty _width;
        private SerializedProperty _height;
        private SerializedProperty _seed;
        private SerializedProperty _computePath;
        private SerializedProperty _maxAttempts;

        // Tile sprite properties
        private SerializedProperty _wallTile;
        private SerializedProperty _floorTile;
        private SerializedProperty _spawnTile;
        private SerializedProperty _exitTile;
        private SerializedProperty _enemyTile;

        private void OnEnable()
        {
            _algorithm    = serializedObject.FindProperty("Algorithm");
            _biome        = serializedObject.FindProperty("Biome");
            _difficulty   = serializedObject.FindProperty("Difficulty");
            _width        = serializedObject.FindProperty("Width");
            _height       = serializedObject.FindProperty("Height");
            _seed         = serializedObject.FindProperty("Seed");
            _computePath  = serializedObject.FindProperty("ComputeShortestPath");
            _maxAttempts  = serializedObject.FindProperty("MaxRegenerationAttempts");

            _wallTile     = serializedObject.FindProperty("WallTile");
            _floorTile    = serializedObject.FindProperty("FloorTile");
            _spawnTile    = serializedObject.FindProperty("SpawnTile");
            _exitTile     = serializedObject.FindProperty("ExitTile");
            _enemyTile    = serializedObject.FindProperty("EnemyTile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var generator = (DungeonGeneratorComponent)target;

            // ── Generation Parameters ──────────────────────────────────────
            EditorGUILayout.LabelField("Generation Parameters", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_algorithm,   new GUIContent("Algorithm"));
            EditorGUILayout.PropertyField(_biome,       new GUIContent("Biome"));
            EditorGUILayout.PropertyField(_difficulty,  new GUIContent("Difficulty"));

            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_width,  new GUIContent("Width",  "Map width in tiles (10–200)"));
            EditorGUILayout.PropertyField(_height, new GUIContent("Height", "Map height in tiles (10–200)"));

            EditorGUILayout.Space(4);

            // Seed field + Randomize button on same line
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_seed, new GUIContent("Seed", "0 = random each time"));
            if (GUILayout.Button("Randomize", GUILayout.Width(90)))
                _seed.intValue = Random.Range(1, int.MaxValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_computePath,  new GUIContent("Show Shortest Path"));
            EditorGUILayout.PropertyField(_maxAttempts, new GUIContent("Max Regen Attempts"));

            // ── Tile Sprites ───────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tile Sprites", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_wallTile,  new GUIContent("Wall"));
            EditorGUILayout.PropertyField(_floorTile, new GUIContent("Floor"));
            EditorGUILayout.PropertyField(_spawnTile, new GUIContent("Spawn"));
            EditorGUILayout.PropertyField(_exitTile,  new GUIContent("Exit"));
            EditorGUILayout.PropertyField(_enemyTile, new GUIContent("Enemy"));

            serializedObject.ApplyModifiedProperties();

            // ── Generate Button ────────────────────────────────────────────
            EditorGUILayout.Space(12);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate Dungeon", GUILayout.Height(36)))
            {
                Undo.RecordObject(generator, "Generate Dungeon");

                var tilemap = generator.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                if (tilemap != null)
                    Undo.RecordObject(tilemap, "Generate Dungeon");

                generator.Generate();

                EditorUtility.SetDirty(generator);
                if (tilemap != null) EditorUtility.SetDirty(tilemap);
            }
            GUI.backgroundColor = Color.white;

            // ── Last Generation Stats ──────────────────────────────────────
            if (generator.LastGenerationMs > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Last Generation", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Seed Used",      generator.LastSeedUsed.ToString());
                    EditorGUILayout.TextField("Time",           $"{generator.LastGenerationMs} ms");
                    EditorGUILayout.TextField("Spawn Cell",     generator.SpawnCell.ToString());
                    EditorGUILayout.TextField("Exit Cell",      generator.ExitCell.ToString());
                    EditorGUILayout.TextField("Enemies Placed", generator.EnemyCells.Count.ToString());
                    if (generator.ComputeShortestPath)
                        EditorGUILayout.TextField("Path Length", generator.PathCells.Count.ToString() + " tiles");
                }
            }
        }
    }
}
