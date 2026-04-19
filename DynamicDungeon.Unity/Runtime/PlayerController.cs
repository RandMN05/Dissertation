using UnityEngine;
using UnityEngine.InputSystem;

namespace DynamicDungeon.Unity
{
    // Attach to your Player prefab.
    // Requires: Rigidbody2D, CircleCollider2D
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public float Speed = 3f;

        private Rigidbody2D _rb;
        private Vector2 _move;
        private DungeonMap _map;

        private void Awake()
        {
            // Subscribe for any future regenerations.
            var generator = FindFirstObjectByType<DungeonGeneratorComponent>();
            if (generator != null)
            {
                generator.OnMapGenerated += OnMapReady;

                // The player is spawned after generation, so LastMap is already
                // populated — seed directly rather than waiting for the next event.
                if (generator.LastMap != null)
                    _map = generator.LastMap;
            }
        }

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        private void OnDestroy()
        {
            var generator = FindFirstObjectByType<DungeonGeneratorComponent>();
            if (generator != null)
                generator.OnMapGenerated -= OnMapReady;
        }

        private void OnMapReady(DungeonMap map) => _map = map;

        private void Update()
        {
            _move = new Vector2(
                Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1 :
                Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed  ? -1 : 0,
                Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed    ? 1 :
                Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed  ? -1 : 0
            );
        }

        private void FixedUpdate()
        {
            var vel = _move.normalized * Speed;

            if (_map != null)
            {
                var pos = (Vector2)transform.position;

                // Check each axis independently so the player slides along walls
                // rather than stopping dead on diagonal contacts.
                if (vel.x != 0)
                {
                    var nextX = pos + new Vector2(vel.x * Time.fixedDeltaTime, 0);
                    if (_map.IsWall(_map.Tilemap.WorldToCell(nextX)))
                        vel.x = 0;
                }
                if (vel.y != 0)
                {
                    var nextY = pos + new Vector2(0, vel.y * Time.fixedDeltaTime);
                    if (_map.IsWall(_map.Tilemap.WorldToCell(nextY)))
                        vel.y = 0;
                }
            }

            _rb.linearVelocity = vel;
        }
    }
}
