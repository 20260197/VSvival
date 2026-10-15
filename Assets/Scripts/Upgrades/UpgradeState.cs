using System;
using CookieBreaker.Combat;

namespace CookieBreaker.Upgrades
{
    public enum UpgradeKind { Damage, AttackSpeed, Production }
    public enum UpgradeLimit { None, EffectCap, CostLimit }

    public readonly struct UpgradeOffer
    {
        public int Level { get; }
        public int? Cost { get; }
        public UpgradeLimit Limit { get; }
        public bool CanPurchase(int coins) => Limit == UpgradeLimit.None && Cost.HasValue && coins >= Cost.Value;

        internal UpgradeOffer(int level, int? cost, UpgradeLimit limit)
        {
            Level = level;
            Cost = cost;
            Limit = limit;
        }
    }

    // Pure session rules: no scene, UI or Unity dependencies.
    public sealed class UpgradeState
    {
        private readonly CoinBalance wallet;
        private readonly int baseDamage;
        private readonly double baseFireInterval, baseSpawnInterval, minimumFireInterval, minimumSpawnInterval;
        private readonly int[] levels = new int[3];

        public UpgradeState(CoinBalance wallet, int baseDamage, double baseFireInterval, double baseSpawnInterval,
            double minimumFireInterval = 0.1, double minimumSpawnInterval = 0.2)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            if (baseDamage < 1) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            ValidateInterval(baseFireInterval, nameof(baseFireInterval));
            ValidateInterval(baseSpawnInterval, nameof(baseSpawnInterval));
            ValidateInterval(minimumFireInterval, nameof(minimumFireInterval));
            ValidateInterval(minimumSpawnInterval, nameof(minimumSpawnInterval));
            this.baseDamage = baseDamage;
            this.baseFireInterval = baseFireInterval;
            this.baseSpawnInterval = baseSpawnInterval;
            // Respect a pre-existing faster custom M1 baseline instead of slowing it down.
            this.minimumFireInterval = Math.Min(baseFireInterval, minimumFireInterval);
            this.minimumSpawnInterval = Math.Min(baseSpawnInterval, minimumSpawnInterval);
        }

        public int Damage => (int)Math.Min(int.MaxValue, (long)baseDamage + levels[(int)UpgradeKind.Damage]);
        public double FireInterval => Interval(baseFireInterval, minimumFireInterval, 1.10, levels[1]);
        public double SpawnInterval => Interval(baseSpawnInterval, minimumSpawnInterval, 1.15, levels[2]);

        public UpgradeOffer Offer(UpgradeKind kind)
        {
            int index = Index(kind);
            int? cost = CalculateCost(BaseCost(kind), levels[index]);
            bool capped = kind == UpgradeKind.Damage ? Damage == int.MaxValue :
                kind == UpgradeKind.AttackSpeed ? FireInterval <= minimumFireInterval : SpawnInterval <= minimumSpawnInterval;
            return new UpgradeOffer(levels[index], cost,
                capped ? UpgradeLimit.EffectCap : !cost.HasValue ? UpgradeLimit.CostLimit : UpgradeLimit.None);
        }

        public bool TryPurchase(UpgradeKind kind)
        {
            UpgradeOffer offer = Offer(kind);
            if (!offer.CanPurchase(wallet.Value)) return false;
            int index = Index(kind);
            int nextLevel = checked(levels[index] + 1);
            if (!wallet.TrySpend(offer.Cost.Value)) return false;
            levels[index] = nextLevel;
            return true;
        }

        // Restore counts directly, never replay purchases or add effects to current values.
        public void RestoreLevels(int damage, int attackSpeed, int production)
        {
            int[] restored = { damage, attackSpeed, production };
            for (int index = 0; index < restored.Length; index++)
            {
                int level = restored[index];
                if (level < 0 || (level > 0 && !CalculateCost(BaseCost((UpgradeKind)index), level - 1).HasValue))
                    throw new ArgumentOutOfRangeException(nameof(restored), "Unreachable upgrade level.");
                if (index == 0 && (long)baseDamage + level > int.MaxValue ||
                    index == 1 && level > 0 && Interval(baseFireInterval, minimumFireInterval, 1.10, level - 1) <= minimumFireInterval ||
                    index == 2 && level > 0 && Interval(baseSpawnInterval, minimumSpawnInterval, 1.15, level - 1) <= minimumSpawnInterval)
                    throw new ArgumentOutOfRangeException(nameof(restored), "Upgrade level exceeds its gameplay cap.");
            }
            Array.Copy(restored, levels, levels.Length);
        }

        public static int? CalculateCost(int baseCost, int purchases)
        {
            if (baseCost < 1) throw new ArgumentOutOfRangeException(nameof(baseCost));
            if (purchases < 0) throw new ArgumentOutOfRangeException(nameof(purchases));
            double cost = Math.Ceiling(baseCost * Math.Pow(1.15, purchases));
            // Unrepresentable costs stop purchases; never wrap or clamp to a cheaper price.
            if (double.IsNaN(cost) || double.IsInfinity(cost) || cost > int.MaxValue) return null;
            return (int)cost;
        }

        private static int BaseCost(UpgradeKind kind) => kind == UpgradeKind.Damage ? 10 :
            kind == UpgradeKind.AttackSpeed ? 15 : 20;
        private static int Index(UpgradeKind kind)
        {
            int value = (int)kind;
            if (value < 0 || value >= 3) throw new ArgumentOutOfRangeException(nameof(kind));
            return value;
        }
        private static double Interval(double baseline, double minimum, double multiplier, int level) =>
            Math.Max(minimum, baseline / Math.Pow(multiplier, level));
        private static void ValidateInterval(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.1)
                throw new ArgumentOutOfRangeException(name, "Intervals must be finite and at least 0.1 seconds.");
        }
    }
}
