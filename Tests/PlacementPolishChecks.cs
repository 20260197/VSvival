using System;
using System.Linq;
using CookieBreaker.Combat;
using CookieBreaker.Placement;
using CookieBreaker.Upgrades;

internal static class PlacementPolishChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static PlacedTurretData Record(string id, int x, int y, int paid = 25) => new PlacedTurretData
        { instanceId = id, typeId = TurretCatalog.Catapult, gridX = x, gridY = y, paidCost = paid };
    private static void Main()
    {
        var gesture = new PlacementGesture();
        Check(!gesture.Release(1, true), "Release without down committed");
        Check(gesture.Begin(1) && !gesture.Begin(2) && gesture.Owns(1), "Second touch stole the gesture");
        Check(!gesture.Release(2, true) && gesture.Owns(1), "Other pointer released the captured gesture");
        Check(!gesture.Release(1, false) && !gesture.Owns(1), "UI/outside release committed or stayed active");
        Check(gesture.Begin(1), "A canceled release blocked the next gesture"); gesture.Cancel();
        Check(!gesture.Release(1, true), "Release after cancel/focus loss committed");
        Check(gesture.Begin(-1) && gesture.Release(-1, true) && !gesture.Release(-1, true), "Mouse release did not commit exactly once");

        double sumSquared = 0, sumX = 0, sumY = 0;
        const int count = 10000;
        for (int i = 0; i < count; i++)
        {
            double angle = i * 0.618033988749895 % 1;
            Check(CircleSpawnRules.Sample(angle, (i + 0.5) / count, 1.2, 0.18, out double x, out double y), "Valid circle sample rejected");
            Check(CircleSpawnRules.Contains(x, y, 1.2, 0.18), "Cookie radius crossed visible circle");
            sumSquared += x * x + y * y; sumX += x; sumY += y;
        }
        Check(Math.Abs(sumSquared / count - 1.02 * 1.02 / 2) < 1e-9, "Samples are center-biased instead of area-uniform");
        Check(Math.Abs(sumX / count) < 0.002 && Math.Abs(sumY / count) < 0.002, "Samples cluster in one direction");
        Check(!CircleSpawnRules.Sample(0.5, 0.5, 0.1, 0.18, out _, out _) &&
            !CircleSpawnRules.Sample(double.NaN, 0.5, 1, 0.1, out _, out _), "Invalid circle sample accepted");
        Check(!CircleSpawnRules.Contains(1.1, 0, 1.2, 0.18), "Center-only inclusion ignored cookie extent");

        var grid = new GridRules(8, 9, 4, 4.95, 2, 0.75);
        for (int y = 0; y < grid.Rows; y++) for (int x = 0; x < grid.Columns; x++)
            if (grid.Allows(new GridCell(x, y)))
                Check(Math.Sqrt(Math.Pow(x + 0.5 - 4, 2) + Math.Pow(y + 0.5 - 4.95, 2)) > 2.75, "Turret footprint crosses cookie circle");
        var coins = new CoinBalance(123);
        var layout = new GridPlacementState(coins, grid, TurretCatalog.Defaults()[0]);
        var old = new[] { Record("move", 2, 3, 29), Record("keep", 2, 2, 0), Record("move2", 5, 3, 34) };
        Check(layout.Restore(old, true) == 0 && layout.RelocatedCount == 2 && layout.Count == 3 && coins.Value == 123,
            "Circle expansion lost a valid old turret or charged coins");
        Check(layout.Find(new GridCell(2, 2)).instanceId == "keep", "Relocation stole another valid saved cell");
        PlacedTurretData[] moved = layout.Snapshot();
        Check(moved.All(r => grid.Allows(new GridCell(r.gridX, r.gridY))) && moved.Single(r => r.instanceId == "move").paidCost == 29,
            "Relocation failed to preserve identity/metadata or avoid the circle");
        layout.Restore(moved, true);
        Check(layout.RelocatedCount == 0 && layout.Count == 3, "Repeated load relocated or duplicated towers again");
        var tiny = new GridPlacementState(coins, new GridRules(3, 1, 1.5, 0.5, 0.25), TurretCatalog.Defaults()[0]);
        tiny.Restore(new[] { Record("previous", 0, 0) });
        try { tiny.Restore(new[] { Record("left", 0, 0), Record("right", 2, 0), Record("blocked", 1, 0) }, true); throw new Exception("Full grid relocation silently dropped towers"); }
        catch (InvalidOperationException) { }
        Check(tiny.Count == 1 && tiny.Find(new GridCell(0, 0)).instanceId == "previous" && coins.Value == 123, "Failed relocation partially committed");

        var saleWallet = new CoinBalance();
        var sales = new GridPlacementState(saleWallet, grid, TurretCatalog.Defaults()[0]);
        sales.Restore(new[] { Record("a", 0, 0, 999), Record("b", 1, 0, 1), Record("c", 2, 0, 0) });
        Check(sales.Refund(Record("a", 0, 0)) == 17 && sales.CountOfType("railgun") == 0, "Refund used purchase history or another type count");
        Check(sales.TrySell("b", out _, 17) && saleWallet.Value == 17 && sales.Refund(Record("a", 0, 0)) == 14, "Quantity change did not change the next quote");
        Check(!sales.TrySell("a", out _, 17) && saleWallet.Value == 17 && sales.Count == 2, "Stale confirmation amount was paid");
        Check(sales.TrySell("a", out _, 14) && saleWallet.Value == 31 && sales.Refund(Record("c", 2, 0)) == 12,
            "Reviewed quote was not paid exactly once");
        Check(sales.TrySell("c", out _, 12) && saleWallet.Value == 43 && !sales.TrySell("c", out _, 12), "Last/repeated sale duplicated coins");

        var upgrades = new UpgradeState(coins, 1, 0.4, 0.25);
        upgrades.RestoreLevels(0, 0, 11, allowCappedProductionLevels: true);
        Check(upgrades.Offer(UpgradeKind.Production).Level == 11 && upgrades.SpawnInterval == 0.2 && coins.Value == 123,
            "Faster production invalidated saved levels or removed the existing rate cap");
        Console.WriteLine("PASS: M4.1 pointer ownership/release/cancel, 10000 uniform circle samples with cookie extent, turret clearance, atomic legacy relocation, quantity-based sale/requote and preserved production levels.");
    }
}
