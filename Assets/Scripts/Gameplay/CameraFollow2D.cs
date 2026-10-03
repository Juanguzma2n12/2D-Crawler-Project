using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platformer.Gameplay
{
    // Smoothly follows the target (the Bandit by default).
    // Adds itself to the Main Camera automatically when a scene with a Bandit loads, so no setup is needed.
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Leave empty to follow the first Bandit found in the scene.")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);

        [Header("Smoothing")]
        [Tooltip("Roughly how many seconds the camera takes to catch up. 0 = locked to target.")]
        [SerializeField] private float smoothTime = 0.15f;
        [Tooltip("If the target jumps further than this in one frame (teleport, respawn), the camera cuts instead of sliding.")]
        [SerializeField] private float snapDistance = 12f;

        private Vector3 velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToMainCamera();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToMainCamera();
        }

        private static void AttachToMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<CameraFollow2D>() != null) return;
            if (FindAnyObjectByType<Bandit>() == null) return;

            cam.gameObject.AddComponent<CameraFollow2D>();
        }

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

            Vector3 desired = GetDesiredPosition();
            if ((desired - transform.position).sqrMagnitude > snapDistance * snapDistance)
            {
                transform.position = desired;
                velocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        private Vector3 GetDesiredPosition()
        {
            // Keep the camera's own z so it stays in front of the 2D scene
            return new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        }
    }
}
