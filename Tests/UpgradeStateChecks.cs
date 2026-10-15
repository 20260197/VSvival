using System;
using CookieBreaker.Combat;
using CookieBreaker.Upgrades;

internal static class UpgradeStateChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static bool Close(double a, double b) => Math.Abs(a - b) < 0.0000001;

    private static void Main()
    {
        var coins = new CoinBalance(0);
        var upgrades = new UpgradeState(coins, 1, 0.4, 0.9);
        foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
        {
            Check(!upgrades.Offer(kind).CanPurchase(coins.Value), "No money must disable each offer");
            Check(!upgrades.TryPurchase(kind), "No money must reject every purchase");
            Check(upgrades.Offer(kind).Level == 0, "Rejected purchase changed its level");
        }
        coins.Credit(10);
        Check(upgrades.TryPurchase(UpgradeKind.Damage), "Exact 10 coins should buy damage");
        Check(coins.Value == 0 && upgrades.Damage == 2 && upgrades.Offer(UpgradeKind.Damage).Level == 1,
            "Damage purchase must spend once and apply +1");
        Check(upgrades.Offer(UpgradeKind.Damage).Cost == 12, "10 * 1.15 must round UP to 12");
        Check(!upgrades.TryPurchase(UpgradeKind.Damage) && upgrades.Damage == 2, "Repeated click must not overspend");

        coins.Credit(35);
        Check(upgrades.TryPurchase(UpgradeKind.AttackSpeed) && coins.Value == 20, "Attack speed base cost is 15");
        Check(Close(upgrades.FireInterval, 0.4 / 1.1), "Attack speed must multiply the rate by 1.10");
        Check(upgrades.TryPurchase(UpgradeKind.Production) && coins.Value == 0, "Production base cost is 20");
        Check(Close(upgrades.SpawnInterval, 0.9 / 1.15), "Production must multiply the rate by 1.15");
        Check(upgrades.Offer(UpgradeKind.AttackSpeed).Cost == 18 && upgrades.Offer(UpgradeKind.Production).Cost == 23,
            "Independent price progression is incorrect");

        foreach (UpgradeKind kind in new[] { UpgradeKind.AttackSpeed, UpgradeKind.Production })
        {
            coins.Credit(100000);
            int count = 0;
            while (upgrades.TryPurchase(kind)) Check(++count < 100, "Speed cap failed to stop purchases");
            UpgradeOffer capped = upgrades.Offer(kind);
            int before = coins.Value;
            Check(capped.Limit == UpgradeLimit.EffectCap && !capped.CanPurchase(before), "Cap must disable offer");
            Check(!upgrades.TryPurchase(kind) && coins.Value == before, "Capped purchase must not spend coins");
            Check(capped.Level == (kind == UpgradeKind.AttackSpeed ? 15 : 11), "Unexpected cap level");
        }
        Check(Close(upgrades.FireInterval, 0.1) && Close(upgrades.SpawnInterval, 0.2), "Intervals must stop exactly at caps");

        var rich = new CoinBalance(int.MaxValue);
        var high = new UpgradeState(rich, 1, 0.4, 0.9);
        int previousCost = 0;
        int purchases = 0;
        while (high.Offer(UpgradeKind.Damage).Cost.HasValue)
        {
            int cost = high.Offer(UpgradeKind.Damage).Cost.Value;
            Check(cost >= previousCost && cost > 0, "High-level cost wrapped or decreased");
            previousCost = cost;
            Check(high.TryPurchase(UpgradeKind.Damage), "Representable affordable high-level cost rejected");
            rich.Credit(int.MaxValue - rich.Value);
            Check(++purchases < 1000, "Cost limit never reached");
        }
        Check(high.Offer(UpgradeKind.Damage).Limit == UpgradeLimit.CostLimit, "Overflowing cost needs explicit limit");
        Check(!high.TryPurchase(UpgradeKind.Damage) && rich.Value == int.MaxValue, "Unrepresentable cost spent coins");
        Check(UpgradeState.CalculateCost(20, int.MaxValue) == null, "Extreme level cost must not overflow");
        Check(UpgradeState.CalculateCost(int.MaxValue, 0) == int.MaxValue &&
            UpgradeState.CalculateCost(int.MaxValue, 1) == null, "Int32 boundary cost handling failed");
        var maximumDamage = new UpgradeState(rich, int.MaxValue, 0.4, 0.9);
        Check(!maximumDamage.TryPurchase(UpgradeKind.Damage) && rich.Value == int.MaxValue, "Max damage must not overflow or charge");
        var fastBaseline = new UpgradeState(rich, 1, 0.1, 0.1);
        Check(Close(fastBaseline.SpawnInterval, 0.1) && !fastBaseline.TryPurchase(UpgradeKind.Production),
            "A custom faster M1 baseline must not be slowed or sold an ineffective upgrade");
        Throws(() => UpgradeState.CalculateCost(10, -1));
        Throws(() => UpgradeState.CalculateCost(0, 1));
        Throws(() => new UpgradeState(rich, 0, 0.4, 0.9));
        Throws(() => new UpgradeState(rich, 1, double.NaN, 0.9));
        Throws(() => new UpgradeState(rich, 1, 0.4, double.PositiveInfinity));
        Throws(() => high.TryPurchase((UpgradeKind)99));
        Console.WriteLine("PASS: M2 exact/insufficient/repeated purchases, effects, independent costs, caps, high-level overflow and invalid inputs.");
    }

    private static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Invalid input should throw ArgumentOutOfRangeException");
    }
}
