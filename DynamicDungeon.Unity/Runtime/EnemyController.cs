using UnityEngine;

namespace DynamicDungeon.Unity
{
    // Attach to your Enemy prefab.
    // Requires: Rigidbody2D, CircleCollider2D
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyController : MonoBehaviour
    {
        public float Speed = 1.5f;

        private Transform _player;
        private Rigidbody2D _rb;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            var dir = ((Vector2)_player.position - _rb.position).normalized;
            _rb.linearVelocity = dir * Speed;
        }
    }
}
