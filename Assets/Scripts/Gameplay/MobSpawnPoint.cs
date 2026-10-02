using UnityEngine;

namespace Platformer.Gameplay
{
    public class MobSpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public GameObject mobPrefab;
        public bool spawnOnStart = true;
        
        [Header("Patrol Area")]
        public float patrolRange = 3f;
        public Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.75f);

        private void Start()
        {
            if (spawnOnStart && mobPrefab != null)
            {
                Instantiate(mobPrefab, transform.position, Quaternion.identity, transform);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            
            // Draw horizontal patrol range
            Vector3 left = transform.position + Vector3.left * patrolRange;
            Vector3 right = transform.position + Vector3.right * patrolRange;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawWireCube(left, new Vector3(0.1f, 0.4f, 0.1f));
            Gizmos.DrawWireCube(right, new Vector3(0.1f, 0.4f, 0.1f));
        }
    }
}
