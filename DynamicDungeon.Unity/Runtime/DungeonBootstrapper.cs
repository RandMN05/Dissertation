using UnityEngine;
using UnityEngine.Tilemaps;

namespace DynamicDungeon.Unity
{
    // Attach to an empty GameObject in the scene.
    // Drag a LevelCollection asset (created in the Level Designer window) into
    // the Levels slot to enable multi-level progression. Leave it empty to use
    // whatever is currently configured on the DungeonGeneratorComponent.
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

        [Header("Levels")]
        [Tooltip("Drag a LevelCollection asset here to use authored levels in order. Leave empty to use the generator's current settings.")]
        public LevelCollection Levels;

        [Header("Tuning")]
        public float WinDistance  = 0.6f;
        public float LoseDistance = 0.4f;

        private int     _currentLevel;
        private Vector3 _exitWorldPos;
        private bool    _gameOver;
        private string  _endMessage;
        private bool    _transitioning;

        private GameObject _player;

        private void Start() => LoadLevel(0);

        private void Update()
        {
            if (_gameOver || _transitioning || _player == null) return;

            // Win: player reached exit
            if (Vector2.Distance(_player.transform.position, _exitWorldPos) < WinDistance)
            {
                int next = _currentLevel + 1;
                if (Levels != null && Levels.IsValidIndex(next))
                    StartCoroutine(TransitionToLevel(next));
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

        private void LoadLevel(int index)
        {
            _currentLevel  = index;
            _transitioning = false;

            // Load from collection if available, otherwise use generator as-is.
            if (Levels != null && Levels.IsValidIndex(index))
                DungeonGenerator.LoadFrom(Levels.Levels[index]);
            else
                DungeonGenerator.Generate();

            // Destroy old enemies before spawning new ones.
            foreach (var e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                Destroy(e.gameObject);

            // Reposition existing player or spawn a new one.
            var spawnWorld = CellToWorld(DungeonGenerator.SpawnCell);
            if (_player != null)
            {
                _player.transform.position = spawnWorld;
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            else
            {
                _player = Instantiate(PlayerPrefab, spawnWorld, Quaternion.identity);
                _player.tag = "Player";
            }

            foreach (var cell in DungeonGenerator.EnemyCells)
                Instantiate(EnemyPrefab, CellToWorld(cell), Quaternion.identity);

            _exitWorldPos = CellToWorld(DungeonGenerator.ExitCell);
        }

        private System.Collections.IEnumerator TransitionToLevel(int index)
        {
            _transitioning = true;
            yield return new WaitForSeconds(0.4f);
            LoadLevel(index);
        }

        private void OnGUI()
        {
            if (!_gameOver)
            {
                string levelName = Levels != null && Levels.IsValidIndex(_currentLevel)
                    ? $"Level {_currentLevel + 1} — {Levels.Levels[_currentLevel].LevelName}"
                    : $"Level {_currentLevel + 1}";

                var labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    normal   = { textColor = Color.white }
                };
                GUI.Label(new Rect(12, 8, 400, 36), levelName, labelStyle);
            }

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

            foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                rb.linearVelocity = Vector2.zero;
        }

        private Vector3 CellToWorld(Vector3Int cell)
            => Tilemap.CellToWorld(cell) + Tilemap.cellSize / 2f;
    }
}
