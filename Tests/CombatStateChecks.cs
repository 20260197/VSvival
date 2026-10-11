using System;
using CookieBreaker.Combat;

public static class CombatStateChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        var random = new Random(731);
        var coins = new CoinBalance();
        for (int cookie = 0; cookie < 200; cookie++)
        {
            var health = new HealthState(3);
            while (!health.IsDead)
                if (health.ApplyDamage(random.Next(1, 7))) coins.Credit(1);
            // Simulate additional colliding projectiles in the death frame.
            for (int extraHit = 0; extraHit < 8; extraHit++)
                if (health.ApplyDamage(int.MaxValue)) coins.Credit(1);
            Check(health.Current == 0, "Health must stop at zero.");
            Check(coins.Value == cookie + 1, "Each cookie must reward exactly one coin.");
        }
        Console.WriteLine("PASS: 200 cookie deaths with overkill and repeated hits yield exactly 200 coins.");

        var untouched = new HealthState(3);
        Check(!untouched.ApplyDamage(0) && !untouched.ApplyDamage(-5), "Invalid damage must not kill.");
        Check(untouched.Current == 3, "Invalid damage must not heal or injure.");
        Check(!untouched.ApplyDamage(1) && untouched.Current == 2, "A nonlethal hit must not reward.");
        Check(untouched.ApplyDamage(int.MaxValue) && untouched.Current == 0, "Large overkill must kill safely.");
        Console.WriteLine("PASS: zero/negative damage, nonlethal damage and maximum overkill.");

        var spending = new CoinBalance(10);
        Check(!spending.TrySpend(11) && !spending.TrySpend(0) && !spending.TrySpend(-1), "Invalid purchases must fail.");
        Check(spending.Value == 10, "Failed purchases must preserve the balance.");
        Check(spending.TrySpend(10) && spending.Value == 0, "An affordable purchase must spend exactly its cost.");
        Check(!spending.TrySpend(1) && spending.Value == 0, "Coins must not go negative.");
        spending.Credit(1);
        Check(spending.Value == 1, "Rewards must remain usable after spending.");
        Console.WriteLine("PASS: purchase extension preserves nonnegative currency and failed-purchase balance.");

        bool invalidHealthRejected = false, invalidCoinsRejected = false, overflowRejected = false;
        try { new HealthState(0); } catch (ArgumentOutOfRangeException) { invalidHealthRejected = true; }
        try { new CoinBalance(-1); } catch (ArgumentOutOfRangeException) { invalidCoinsRejected = true; }
        var full = new CoinBalance(int.MaxValue);
        try { full.Credit(1); } catch (OverflowException) { overflowRejected = true; }
        Check(invalidHealthRejected && invalidCoinsRejected && overflowRejected && full.Value == int.MaxValue,
            "Invalid initialization and currency overflow must not corrupt state.");
        Console.WriteLine("PASS: invalid initialization and currency overflow rejected.");
    }
}
