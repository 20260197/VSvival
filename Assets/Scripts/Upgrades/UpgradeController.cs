using System;
using CookieBreaker.Combat;
using UnityEngine;

namespace CookieBreaker.Upgrades
{
    public sealed class UpgradeController : MonoBehaviour
    {
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private AutomaticTurret turret;
        [SerializeField] private CookieSpawner spawner;
        [SerializeField, Min(0.1f)] private float minimumFireInterval = 0.1f;
        [SerializeField, Min(0.1f)] private float minimumSpawnInterval = 0.2f;
        private UpgradeState state;
        private bool purchasing;
        public event Action Changed;
        public event Action Purchased;
        internal UpgradeState State => state;

        public void Configure(CoinWallet coinWallet, AutomaticTurret automaticTurret, CookieSpawner cookieSpawner)
        {
            wallet = coinWallet;
            turret = automaticTurret;
            spawner = cookieSpawner;
        }

        private void Awake()
        {
            if (EnsureReady()) return;
            Debug.LogError("UpgradeController needs the M1 wallet, turret and spawner references.", this);
            enabled = false;
        }

        public bool EnsureReady()
        {
            if (wallet == null || turret == null || spawner == null) return false;
            if (state == null)
                state = new UpgradeState(wallet.Balance, turret.Damage, turret.FireInterval, spawner.SpawnInterval,
                    Mathf.Max(0.1f, minimumFireInterval), Mathf.Max(0.1f, minimumSpawnInterval));
            return true;
        }

        public bool TryPurchase(UpgradeKind kind)
        {
            if (purchasing || !isActiveAndEnabled || !EnsureReady()) return false;
            purchasing = true;
            try
            {
                if (!state.TryPurchase(kind)) return false;
                ApplyEffectsAndNotify();
                Purchased?.Invoke();
                return true;
            }
            finally { purchasing = false; }
        }

        public void RestoreProgress(int coins, int damageLevel, int speedLevel, int productionLevel)
        {
            if (purchasing || !EnsureReady()) throw new InvalidOperationException("Cannot restore upgrades during a purchase or without M1 references.");
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            // Validate every level before changing any state, including the shared wallet.
            // A faster starting production rate must not invalidate already purchased levels.
            // Costs remain bounded; the current rate is still clamped to its normal cap.
            state.RestoreLevels(damageLevel, speedLevel, productionLevel, allowCappedProductionLevels: true);
            wallet.Balance.Restore(coins);
            ApplyEffectsAndNotify();
        }

        public void ClearTransientCombat()
        {
            turret.ClearActiveProjectiles();
            spawner.ClearActiveCookies();
        }

        private void ApplyEffectsAndNotify()
        {
            turret.Damage = state.Damage;
            turret.FireInterval = (float)state.FireInterval;
            spawner.SpawnInterval = (float)state.SpawnInterval;
            wallet.NotifyBalanceChanged();
            Changed?.Invoke();
        }
    }
}
