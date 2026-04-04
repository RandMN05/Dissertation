using UnityEditor;
using UnityEngine;
using DynamicDungeon.Unity;

namespace DynamicDungeon.Unity.Editor
{
    /// <summary>
    /// Read-only status inspector for DungeonGeneratorComponent.
    /// All configuration is done in the Level Designer window.
    /// </summary>
    [CustomEditor(typeof(DungeonGeneratorComponent))]
    public class DungeonGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var generator = (DungeonGeneratorComponent)target;

            EditorGUILayout.HelpBox(
                "Configure and generate levels via the Level Designer window.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
            if (GUILayout.Button("Open Level Designer", GUILayout.Height(30)))
                LevelDesignerWindow.Open();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("Preview Map", GUILayout.Height(30)))
            {
                var tilemap = generator.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                if (tilemap != null) Undo.RecordObject(tilemap, "Preview Map");
                Undo.RecordObject(generator, "Preview Map");
                generator.Generate();
                EditorUtility.SetDirty(generator);
                if (tilemap != null) EditorUtility.SetDirty(tilemap);
            }
            GUI.backgroundColor = Color.white;

            // Read-only stats from the last generation
            if (generator.LastGenerationMs > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Last Generation", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Seed Used",      generator.LastSeedUsed.ToString());
                    EditorGUILayout.TextField("Time",           $"{generator.LastGenerationMs} ms");
                    EditorGUILayout.TextField("Spawn Cell",     generator.SpawnCell.ToString());
                    EditorGUILayout.TextField("Exit Cell",      generator.ExitCell.ToString());
                    EditorGUILayout.TextField("Enemies Placed", generator.EnemyCells.Count.ToString());
                    if (generator.ComputeShortestPath)
                        EditorGUILayout.TextField("Path Length", generator.PathCells.Count + " tiles");
                }
            }
        }
    }
}
