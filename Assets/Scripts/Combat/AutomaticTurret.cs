using System.Collections.Generic;
using UnityEngine;

namespace CookieBreaker.Combat
{
    public sealed class AutomaticTurret : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CookieSpawner spawner;
        [SerializeField] private Transform barrelPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private CookieProjectile projectileTemplate;
        [SerializeField] private Transform projectileContainer;
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.5f, 0.18f);
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0.1f)] private float fireInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float attackRange = 8;
        [SerializeField, Range(1, 64)] private int maximumProjectiles = 24;
        private readonly List<CookieProjectile> projectiles = new List<CookieProjectile>();
        private float remaining;
        private bool placedOnGrid;
        [SerializeField] private CookieHealth currentTarget;
        public CookieHealth CurrentTarget => currentTarget;
        public CookieSpawner Spawner => spawner;
        private void OnDisable() { currentTarget = null; }
        public void PlaceAt(Vector3 position) { placedOnGrid = true; transform.position = position; }
        public int Damage { get => damage; set => damage = Mathf.Max(1, value); }
        public float FireInterval
        {
            get => Mathf.Max(0.1f, fireInterval);
            set
            {
                float previous = FireInterval;
                fireInterval = Mathf.Max(0.1f, value);
                remaining *= fireInterval / previous;
            }
        }

        public void Configure(Camera camera, CookieSpawner cookieSpawner, Transform pivot, Transform firePoint,
            CookieProjectile template, Transform container)
        {
            worldCamera = camera;
            spawner = cookieSpawner;
            barrelPivot = pivot;
            muzzle = firePoint;
            projectileTemplate = template;
            projectileContainer = container;
        }

        public void ClearActiveProjectiles()
        {
            foreach (CookieProjectile projectile in projectiles)
            {
                if (projectile == null) continue;
                projectile.gameObject.SetActive(false);
                Destroy(projectile.gameObject);
            }
            projectiles.Clear();
            remaining = 0;
        }

        private void Start()
        {
            if (worldCamera != null && spawner != null && barrelPivot != null && muzzle != null &&
                projectileTemplate != null && projectileContainer != null && !projectileTemplate.gameObject.activeSelf) return;
            Debug.LogError("AutomaticTurret has missing camera, spawner, barrel, muzzle or inactive projectile template references.", this);
            enabled = false;
        }

        private void Update()
        {
            if (!placedOnGrid) transform.position = CookieSpawner.ViewportPosition(worldCamera, viewportPosition);
            projectiles.RemoveAll(projectile => projectile == null || !projectile.isActiveAndEnabled);
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            currentTarget = spawner.FindNearest(transform.position, Mathf.Max(0.1f, attackRange));
            CookieHealth target = currentTarget;
            if (target == null) return;
            Vector2 direction = target.transform.position - transform.position;
            barrelPivot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
            if (remaining > 0 || projectiles.Count >= Mathf.Clamp(maximumProjectiles, 1, 64)) return;
            Vector2 shotDirection = target.transform.position - muzzle.position;
            if (shotDirection.sqrMagnitude < 0.0001f) return;
            CookieProjectile projectile = Instantiate(projectileTemplate, muzzle.position, Quaternion.identity, projectileContainer);
            projectile.Initialize(shotDirection.normalized, Mathf.Max(1, damage));
            projectile.gameObject.SetActive(true);
            projectiles.Add(projectile);
            remaining = FireInterval;
        }
    }
}
