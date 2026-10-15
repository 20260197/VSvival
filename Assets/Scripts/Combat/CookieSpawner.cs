using System.Collections.Generic;
using UnityEngine;

namespace CookieBreaker.Combat
{
    public sealed class CookieSpawner : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private CookieHealth cookieTemplate;
        [SerializeField] private Transform cookieContainer;
        [SerializeField, Min(0.1f)] private float spawnInterval = 0.9f;
        [SerializeField, Range(1, 100)] private int maximumCookies = 24;
        [SerializeField] private Rect viewportArea = new Rect(0.14f, 0.36f, 0.72f, 0.42f);
        [SerializeField, Min(0)] private float minimumSpacing = 0.72f;
        private readonly List<CookieHealth> cookies = new List<CookieHealth>();
        private float remaining;
        public IReadOnlyList<CookieHealth> ActiveCookies => cookies;
        public float SpawnInterval
        {
            get => Mathf.Max(0.1f, spawnInterval);
            set
            {
                float previous = SpawnInterval;
                spawnInterval = Mathf.Max(0.1f, value);
                remaining *= spawnInterval / previous;
            }
        }

        public void Configure(Camera camera, CoinWallet coinWallet, CookieHealth template, Transform container)
        {
            worldCamera = camera;
            wallet = coinWallet;
            cookieTemplate = template;
            cookieContainer = container;
        }

        public void ClearActiveCookies()
        {
            foreach (CookieHealth cookie in cookies)
            {
                if (cookie == null) continue;
                cookie.gameObject.SetActive(false);
                Destroy(cookie.gameObject);
            }
            cookies.Clear();
            remaining = 0;
        }

        private void Start()
        {
            if (worldCamera != null && wallet != null && cookieTemplate != null &&
                cookieContainer != null && !cookieTemplate.gameObject.activeSelf) return;
            Debug.LogError("CookieSpawner needs a camera, wallet, inactive cookie template and container.", this);
            enabled = false;
        }

        private void Update()
        {
            cookies.RemoveAll(cookie => cookie == null || !cookie.IsAlive);
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = SpawnInterval;
            if (cookies.Count >= Mathf.Clamp(maximumCookies, 1, 100)) return;

            // A bounded retry keeps random placement from piling cookies on each other.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 position = ViewportPosition(worldCamera, new Vector2(
                    Random.Range(viewportArea.xMin, viewportArea.xMax),
                    Random.Range(viewportArea.yMin, viewportArea.yMax)));
                bool overlaps = false;
                foreach (CookieHealth cookie in cookies)
                {
                    if (((Vector2)cookie.transform.position - position).sqrMagnitude >= minimumSpacing * minimumSpacing) continue;
                    overlaps = true;
                    break;
                }
                if (overlaps) continue;
                CookieHealth spawned = Instantiate(cookieTemplate, position, Quaternion.identity, cookieContainer);
                spawned.Initialize(wallet);
                spawned.gameObject.SetActive(true);
                cookies.Add(spawned);
                return;
            }
        }

        public CookieHealth FindNearest(Vector2 origin, float range)
        {
            CookieHealth nearest = null;
            float bestDistance = range * range;
            foreach (CookieHealth cookie in cookies)
            {
                if (cookie == null || !cookie.IsAlive) continue;
                float distance = ((Vector2)cookie.transform.position - origin).sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                nearest = cookie;
            }
            return nearest;
        }

        internal static Vector3 ViewportPosition(Camera camera, Vector2 viewport)
        {
            float depth = Vector3.Dot(-camera.transform.position, camera.transform.forward);
            Vector3 world = camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
            world.z = 0;
            return world;
        }
    }
}
