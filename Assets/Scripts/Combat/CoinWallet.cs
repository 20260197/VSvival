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
        private CoinBalance Balance => balance ?? (balance = new CoinBalance(Mathf.Max(0, startingCoins)));

        private void Awake() { _ = Balance; }

        public void Credit(int amount)
        {
            Balance.Credit(amount);
            Changed?.Invoke(Coins);
        }

        // A small extension point for the next upgrade milestone.
        public bool TrySpend(int amount)
        {
            if (!Balance.TrySpend(amount)) return false;
            Changed?.Invoke(Coins);
            return true;
        }
    }
}
