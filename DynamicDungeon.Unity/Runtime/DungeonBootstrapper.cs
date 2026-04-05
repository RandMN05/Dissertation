using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DynamicDungeon.Unity
{
    // Attach to a GameObject in each generated level scene.
    // Wired automatically by the Level Designer window — assign your own
    // PlayerPrefab and EnemyPrefab after the scene is created.
    public class DungeonBootstrapper : MonoBehaviour
    {
        [Header("References")]
        public DungeonGeneratorComponent DungeonGenerator;
        public Tilemap Tilemap;

        [Header("Prefabs")]
        [Tooltip("Needs PlayerController, Rigidbody2D, CircleCollider2D. Tag it as Player.")]
        public GameObject PlayerPrefab;
        [Tooltip("Needs EnemyController, Rigidbody2D, CircleCollider2D.")]
        public GameObject EnemyPrefab;

        [Header("Level")]
        [Tooltip("The saved level data for this scene. Wired automatically by the Level Designer window.")]
        public DungeonLevelData LevelData;

        [Tooltip("Scene name to load when the player reaches the exit. Leave empty on the final level.")]
        public string NextSceneName = "";

        [Header("Tuning")]
        public float WinDistance  = 0.6f;
        public float LoseDistance = 0.4f;

        private Vector3    _exitWorldPos;
        private bool       _gameOver;
        private string     _endMessage;
        private GameObject _player;

        private void Start()
        {
            // Load from the saved asset (guaranteed same map every time) or
            // fall back to whatever is configured on the generator directly.
            if (LevelData != null)
                DungeonGenerator.LoadFrom(LevelData);
            else
                DungeonGenerator.Generate();

            // LastMap is populated synchronously by Generate() / LoadFrom().
            var map = DungeonGenerator.LastMap;

            if (PlayerPrefab != null)
            {
                _player     = Instantiate(PlayerPrefab, map.SpawnWorld, Quaternion.identity);
                _player.tag = "Player";
            }

            if (EnemyPrefab != null)
                foreach (var worldPos in map.EnemyWorldPositions)
                    Instantiate(EnemyPrefab, worldPos, Quaternion.identity);

            _exitWorldPos = map.ExitWorld;
        }

        private void Update()
        {
            if (_gameOver || _player == null) return;

            // Win: player reached exit
            if (Vector2.Distance(_player.transform.position, _exitWorldPos) < WinDistance)
            {
                if (!string.IsNullOrEmpty(NextSceneName))
                    SceneManager.LoadScene(NextSceneName);
                else
                    EndGame("You escaped every dungeon!\nYOU WIN!");
                return;
            }

            // Lose: any enemy caught player
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (Vector2.Distance(_player.transform.position, enemy.transform.position) < LoseDistance)
                {
                    EndGame("Caught! GAME OVER.");
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (!_gameOver) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 40,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), _endMessage, style);

            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            float bw = 200, bh = 50;
            if (GUI.Button(new Rect(Screen.width / 2f - bw / 2f, Screen.height / 2f + 40, bw, bh), "Play Again", btnStyle))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void EndGame(string message)
        {
            _gameOver   = true;
            _endMessage = message;

            foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                rb.linearVelocity = Vector2.zero;
        }

    }
}
