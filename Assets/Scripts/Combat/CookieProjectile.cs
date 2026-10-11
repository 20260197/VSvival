using UnityEngine;

namespace CookieBreaker.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class CookieProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 10;
        [SerializeField, Min(0.1f)] private float lifetime = 3;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private Rigidbody2D body;
        private Vector2 direction;
        private int damage;
        private float remaining;
        private bool spent;

        public void Initialize(Vector2 shotDirection, int shotDamage)
        {
            body = GetComponent<Rigidbody2D>();
            direction = shotDirection.normalized;
            damage = Mathf.Max(1, shotDamage);
            remaining = Mathf.Max(0.1f, lifetime);
            spent = false;
        }

        private void FixedUpdate()
        {
            if (spent) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0 || body == null) { Retire(); return; }
            float distance = Mathf.Max(0.1f, speed) * Time.fixedDeltaTime;
            ContactFilter2D filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            int count = body.Cast(direction, filter, hits, distance);
            CookieHealth nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                CookieHealth cookie = hits[i].collider.GetComponentInParent<CookieHealth>();
                if (cookie == null || !cookie.IsAlive || hits[i].distance >= nearestDistance) continue;
                nearest = cookie;
                nearestDistance = hits[i].distance;
            }
            // Sweep before moving so a small cookie cannot be skipped between physics steps.
            if (nearest != null) { Hit(nearest); return; }
            body.MovePosition(body.position + direction * distance);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            CookieHealth cookie = other.GetComponentInParent<CookieHealth>();
            if (cookie != null && cookie.IsAlive) Hit(cookie);
        }

        private void Hit(CookieHealth cookie)
        {
            if (spent) return;
            spent = true;
            cookie.ReceiveDamage(damage);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void Retire()
        {
            spent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
