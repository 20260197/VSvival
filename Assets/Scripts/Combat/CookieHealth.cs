using UnityEngine;

namespace CookieBreaker.Combat
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CookieHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 3;
        private HealthState health;
        private CoinWallet wallet;
        public bool IsAlive => health != null && !health.IsDead && isActiveAndEnabled;
        public int CurrentHealth => health?.Current ?? maximumHealth;

        public void Initialize(CoinWallet coinWallet)
        {
            wallet = coinWallet;
            health = new HealthState(Mathf.Max(1, maximumHealth));
        }

        public void ReceiveDamage(int amount)
        {
            if (!IsAlive || !health.ApplyDamage(amount)) return;
            // Marked dead before any reward/callback: subsequent hits cannot pay again.
            wallet.Credit(1);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
