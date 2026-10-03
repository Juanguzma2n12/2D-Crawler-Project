using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Gameplay
{
    // Stand inside the trigger and press Up (W / Up arrow / stick up) to travel to the destination.
    [RequireComponent(typeof(Collider2D))]
    public class Teleporter : MonoBehaviour
    {
        [SerializeField] private Transform destination;

        [Header("Events")]
        public UnityEvent onTeleported;

        private Bandit banditInside;

        public Transform Destination
        {
            get => destination;
            set => destination = value;
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            if (banditInside == null || destination == null) return;
            if (!banditInside.UpPressedThisFrame) return;

            banditInside.TeleportTo(destination.position);
            banditInside = null;
            onTeleported?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger || other.attachedRigidbody == null) return;

            Bandit bandit = other.attachedRigidbody.GetComponent<Bandit>();
            if (bandit != null) banditInside = bandit;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.isTrigger || other.attachedRigidbody == null) return;

            if (other.attachedRigidbody.GetComponent<Bandit>() == banditInside) banditInside = null;
        }
    }
}
