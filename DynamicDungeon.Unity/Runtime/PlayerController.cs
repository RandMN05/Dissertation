using UnityEngine;

namespace DynamicDungeon.Unity
{
    // Attach to your Player prefab.
    // Requires: Rigidbody2D, CircleCollider2D
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public float Speed = 3f;

        private Rigidbody2D _rb;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _rb.linearVelocity = new Vector2(h, v).normalized * Speed;
        }
    }
}
