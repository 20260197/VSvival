using System;
using UnityEngine;

namespace CookieBreaker.Combat
{
    public sealed class CoinWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingCoins = 0;
        private CoinBalance balance;
        public event Action<int> Changed;
        public int Coins => Balance.Value;
        internal CoinBalance Balance => balance ?? (balance = new CoinBalance(Mathf.Max(0, startingCoins)));

        // Upgrade purchases notify after their gameplay effects have been applied.
        internal void NotifyBalanceChanged() => Changed?.Invoke(Coins);

        private void Awake() { _ = Balance; }

        public void Credit(int amount)
        {
            Balance.Credit(amount);
            NotifyBalanceChanged();
        }

        public bool TrySpend(int amount)
        {
            if (!Balance.TrySpend(amount)) return false;
            NotifyBalanceChanged();
            return true;
        }
    }
}
