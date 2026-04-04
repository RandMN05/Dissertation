using UnityEngine;
using UnityEngine.Tilemaps;

namespace DynamicDungeon.Unity
{
    // Attach to an empty GameObject in the scene.
    // Generates the dungeon on Start, spawns player and enemies,
    // and handles win/lose detection.
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

        [Header("Tuning")]
        public float WinDistance  = 0.6f;
        public float LoseDistance = 0.4f;

        private Vector3 _exitWorldPos;
        private bool    _gameOver;
        private string  _endMessage;

        private void Start()
        {
            DungeonGenerator.Generate();

            // Spawn player
            var spawnWorld = CellToWorld(DungeonGenerator.SpawnCell);
            var player = Instantiate(PlayerPrefab, spawnWorld, Quaternion.identity);
            player.tag = "Player";

            // Spawn enemies
            foreach (var cell in DungeonGenerator.EnemyCells)
                Instantiate(EnemyPrefab, CellToWorld(cell), Quaternion.identity);

            _exitWorldPos = CellToWorld(DungeonGenerator.ExitCell);
        }

        private void Update()
        {
            if (_gameOver) return;

            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            // Win: player reached exit
            if (Vector2.Distance(player.transform.position, _exitWorldPos) < WinDistance)
            {
                EndGame("You escaped! YOU WIN!");
                return;
            }

            // Lose: any enemy caught player
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (Vector2.Distance(player.transform.position, enemy.transform.position) < LoseDistance)
                {
                    EndGame("Caught! GAME OVER.");
                    return;
                }
            }
        }

        // Draws win/lose message on screen — no Canvas setup needed.
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
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void EndGame(string message)
        {
            _gameOver   = true;
            _endMessage = message;

            // Stop all movement
            foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                rb.linearVelocity = Vector2.zero;
        }

        private Vector3 CellToWorld(Vector3Int cell)
            => Tilemap.CellToWorld(cell) + Tilemap.cellSize / 2f;
    }
}
