using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Gameplay
{
    // Sends the Bandit back to the last activated Checkpoint (or its start position) on touch.
    // Use for bottomless pits, spike pits, lava, etc.
    [RequireComponent(typeof(Collider2D))]
    public class KillZone : MonoBehaviour
    {
        [Header("Events")]
        public UnityEvent onPlayerKilled;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;   // ignore the ground sensor, only the body counts

            Bandit bandit = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Bandit>() : null;
            if (bandit == null) return;

            Vector3 respawn = Checkpoint.LastActivated != null
                ? Checkpoint.LastActivated.RespawnPosition
                : bandit.SpawnPosition;

            bandit.TeleportTo(respawn);
            onPlayerKilled?.Invoke();
        }
    }
}
