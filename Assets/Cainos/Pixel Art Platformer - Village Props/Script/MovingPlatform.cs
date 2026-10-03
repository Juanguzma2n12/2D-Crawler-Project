using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtPlatformer_VillageProps
{
    //moving platform
    //used on a moving platform so that objects fallen on this platform will perfectly follow to it
    //otherwise due to physical simulation precision problem object will not follow the moving platform as expected

    public class MovingPlatform : MonoBehaviour
    {
        public float velocityInheritPercent = 0.8f;

        //colliders currently on the platform. a body can have several (e.g. a character's body and its ground sensor),
        //so the body itself is moved, once, instead of each collider's own transform
        private List<Collider2D> onPlatformColliders;
        private HashSet<Transform> movedThisStep;

        private Vector3 prevPos;
        private Vector2 velocity;

        private void Start()
        {
            onPlatformColliders = new List<Collider2D>();
            movedThisStep = new HashSet<Transform>();
            prevPos = transform.position;
        }

        private void FixedUpdate()
        {
            velocity = (transform.position - prevPos) / Time.fixedDeltaTime;
            prevPos = transform.position;

            movedThisStep.Clear();
            foreach (Collider2D c in onPlatformColliders)
            {
                if (c == null || c.attachedRigidbody == null) continue;

                Transform t = c.attachedRigidbody.transform;
                if (movedThisStep.Add(t)) t.Translate(velocity * Time.fixedDeltaTime);
            }
        }


        private void OnTriggerEnter2D(Collider2D collision)
        {
            Rigidbody2D rb = collision.attachedRigidbody;
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                if (onPlatformColliders.Contains(collision)) return;

                bool bodyAlreadyOn = IsBodyOnPlatform(rb);
                onPlatformColliders.Add(collision);
                if (!bodyAlreadyOn) rb.linearVelocity -= velocity * velocityInheritPercent;
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            Rigidbody2D rb = collision.attachedRigidbody;
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                if (onPlatformColliders.Remove(collision) == false) return;

                if (!IsBodyOnPlatform(rb)) rb.linearVelocity += velocity * velocityInheritPercent;
            }
        }

        private bool IsBodyOnPlatform(Rigidbody2D rb)
        {
            foreach (Collider2D c in onPlatformColliders)
            {
                if (c != null && c.attachedRigidbody == rb) return true;
            }
            return false;
        }
    }
}
