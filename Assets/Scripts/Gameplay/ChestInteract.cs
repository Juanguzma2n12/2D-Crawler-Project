using Cainos.PixelArtPlatformer_VillageProps;
using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Gameplay
{
    // Stand next to the chest and press Up (W / Up arrow / stick up) to open it.
    [RequireComponent(typeof(Collider2D))]
    public class ChestInteract : MonoBehaviour
    {
        [SerializeField] private Chest chest;

        [Header("Events")]
        public UnityEvent onOpened;

        private Bandit banditInside;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            chest = GetComponentInChildren<Chest>();
        }

        private void Update()
        {
            if (banditInside == null || chest == null || chest.IsOpened) return;
            if (!banditInside.UpPressedThisFrame) return;

            chest.Open();
            onOpened?.Invoke();
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
