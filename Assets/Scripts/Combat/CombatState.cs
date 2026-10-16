using System;

namespace CookieBreaker.Combat
{
    // These state objects do not require a running Unity player.
    public sealed class HealthState
    {
        public int Current { get; private set; }
        public bool IsDead => Current == 0;

        public HealthState(int maximum)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
            Current = maximum;
        }

        // True only for the first transition from alive to dead.
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDead) return false;
            Current = Math.Max(0, Current - amount);
            return IsDead;
        }
    }

    public sealed class CoinBalance
    {
        public int Value { get; private set; }

        public CoinBalance(int initial = 0)
        {
            if (initial < 0) throw new ArgumentOutOfRangeException(nameof(initial));
            Value = initial;
        }

        public void Credit(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Value = checked(Value + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Value) return false;
            Value -= amount;
            return true;
        }

        public void Restore(int coins)
        {
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            Value = coins;
        }
    }
}
