using System;
using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isActivated = false;
        
        [Header("Visuals & Feedback")]
        [SerializeField] private GameObject activeVisualGlow;
        [SerializeField] private ParticleSystem activateParticles;
        
        [Header("Events")]
        public UnityEvent onActivated;

        public bool IsActivated => isActivated;

        // The most recently activated checkpoint; KillZone respawns the player here
        public static Checkpoint LastActivated { get; private set; }

        public Vector3 RespawnPosition => transform.position + Vector3.up * 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastActivated = null;
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isActivated) return;

            // Trigger when player enters
            if (other.CompareTag("Player") || other.GetComponent<Rigidbody2D>() != null)
            {
                ActivateCheckpoint();
            }
        }

        public void ActivateCheckpoint()
        {
            if (isActivated) return;
            isActivated = true;
            LastActivated = this;
            UpdateVisuals();

            if (activateParticles != null)
            {
                activateParticles.Play();
            }

            onActivated?.Invoke();
        }

        private void UpdateVisuals()
        {
            if (activeVisualGlow != null)
            {
                activeVisualGlow.SetActive(isActivated);
            }
        }
    }
}
