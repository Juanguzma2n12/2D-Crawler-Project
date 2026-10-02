using UnityEngine;

namespace Platformer.Gameplay
{
    // Put this on the Main Camera. Smoothly follows the target (the Bandit by default).
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Leave empty to follow the first Bandit found in the scene.")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);

        [Header("Smoothing")]
        [Tooltip("Roughly how many seconds the camera takes to catch up. 0 = locked to target.")]
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 velocity;

        private void Start()
        {
            if (target == null)
            {
                var bandit = FindAnyObjectByType<Bandit>();
                if (bandit != null) target = bandit.transform;
            }

            if (target == null)
            {
                Debug.LogWarning("CameraFollow2D: no target assigned and no Bandit found in the scene.", this);
                return;
            }

            // Start centered on the target instead of sliding over from wherever the camera was placed
            transform.position = GetDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref velocity, smoothTime);
        }

        private Vector3 GetDesiredPosition()
        {
            // Keep the camera's own z so it stays in front of the 2D scene
            return new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        }
    }
}
