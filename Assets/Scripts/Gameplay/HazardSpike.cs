using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class HazardSpike : MonoBehaviour
    {
        [Header("Hazard Settings")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float knockbackForce = 5f;
        [SerializeField] private Vector2 knockbackDirection = Vector2.up;

        [Header("Events")]
        public UnityEvent onHazardTriggered;

        public int Damage => damage;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                Vector2 dir = knockbackDirection.normalized;
                rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
            }

            onHazardTriggered?.Invoke();
        }
    }
}

