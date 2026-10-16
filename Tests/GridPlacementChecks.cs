using System;
using System.Linq;
using System.Text.Json;
using CookieBreaker.Combat;
using CookieBreaker.Persistence;
using CookieBreaker.Placement;

internal static class GridPlacementChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static PlacedTurretData Record(string id, int x, int y, string type = TurretCatalog.Catapult, int paid = 25) =>
        new PlacedTurretData { instanceId = id, typeId = type, gridX = x, gridY = y, paidCost = paid };

    private static void Main()
    {
        var grid = new GridRules(8, 9, 4, 4.95, 1.6);
        Check(grid.TryCell(-1.75, -2.75, -2, -3, 0.5, out GridCell cell) && cell.Equals(new GridCell(0, 0)), "World-to-grid floor failed");
        Check(grid.TryCell(-1.5, -2.5, -2, -3, 0.5, out cell) && cell.Equals(new GridCell(1, 1)), "Cell boundary mapping failed");
        Check(!grid.TryCell(2, 0, -2, -3, 0.5, out _) && !grid.TryCell(-2.00001, 0, -2, -3, 0.5, out _), "Outer edge was accepted");
        Check(!grid.TryCell(double.NaN, 0, 0, 0, 1, out _) && !grid.TryCell(0, 0, 0, 0, 0, out _), "Invalid geometry accepted");
        Check(!grid.Allows(new GridCell(3, 4)) && grid.Allows(new GridCell(0, 0)), "Circle exclusion failed");
        Check(!grid.Allows(new GridCell(-1, 0)) && !grid.Allows(new GridCell(int.MaxValue, 0)), "Invalid coordinate accepted");
        var wallet = new CoinBalance();
        var state = new GridPlacementState(wallet, grid, TurretCatalog.Defaults()[0]);
        state.Restore(null);
        Check(state.Count == 1 && state.Snapshot()[0].paidCost == 0 && state.NextCost == 25, "Default free starter changed price or cost coins");
        Check(!state.TryPlace(TurretCatalog.Catapult, new GridCell(0, 0), out _) && wallet.Value == 0, "Insufficient funds spent coins");
        wallet.Credit(100);
        Check(state.TryPlace(TurretCatalog.Catapult, new GridCell(0, 0), out PlacedTurretData bought) && wallet.Value == 75 && state.Count == 2,
            "Successful placement did not commit exactly 25 coins and one cell");
        Check(state.NextCost == 29, "Configurable active-count price growth failed");
        foreach (GridCell invalid in new[] { new GridCell(0, 0), new GridCell(3, 4), new GridCell(8, 0), new GridCell(-1, 2) })
            Check(!state.TryPlace(TurretCatalog.Catapult, invalid, out _) && wallet.Value == 75 && state.Count == 2, "Rejected placement mutated state");
        Check(!state.TryPlace("railgun", new GridCell(1, 0), out _) && wallet.Value == 75, "Locked weapon was implemented as a catapult");
        PlacedTurretData[] saved = state.Snapshot();
        Check(state.Restore(saved.Concat(saved).ToArray()) == 2 && state.Count == 2 && wallet.Value == 75, "Duplicate restore changed occupancy or coins");
        saved[0].gridX = 7;
        Check(state.Find(new GridCell(0, 0)) != null, "Exported records mutated authoritative occupancy");
        PlacedTurretData original = state.Find(new GridCell(0, 0));
        original.instanceId = "changed";
        Check(state.Find(new GridCell(0, 0)).instanceId == bought.instanceId, "Find leaked mutable state");
        var invalidRecords = new[] { Record("valid", 0, 0), Record("valid", 1, 0), Record("other", 0, 0),
            Record("blocked", 3, 4), Record("outside", -1, 0), Record("locked", 1, 0, "railgun"),
            Record("negative", 2, 0, paid: -1), Record(new string('a', 65), 2, 0), null };
        Check(state.Restore(invalidRecords) == 8 && state.Count == 1, "Malformed records were not isolated/skipped");
        Check(!state.TrySell("wrong-id", out _) && state.Count == 1 && wallet.Value == 75, "Wrong sale target removed a different turret");
        Check(state.TrySell("valid", out _) && wallet.Value == 87 && state.Count == 0, "Paid-price refund did not floor 25 * 0.5 to 12");
        Check(!state.TrySell("valid", out _) && wallet.Value == 87, "Repeated sale paid twice");
        state.Restore(Array.Empty<PlacedTurretData>());
        Check(state.Count == 0, "Saved empty layout silently recreated the starter");
        state.Restore(null);
        string starterId = state.Snapshot()[0].instanceId;
        Check(state.TrySell(starterId, out _) && wallet.Value == 87, "Free starter generated resale money");
        state.Restore(new[] { Record("paid", 0, 0, paid: 100) });
        wallet.Restore(int.MaxValue);
        Check(!state.TrySell("paid", out _) && state.Count == 1 && wallet.Value == int.MaxValue, "Sale overflow removed occupancy or coins");
        state.Restore(null);
        Check(state.Count == 1 && state.Snapshot()[0].paidCost == 0, "Reset did not restore the initial layout");
        Check(TurretCatalog.Defaults().Length == 8 && TurretCatalog.Defaults().Count(x => x.unlocked) == 1, "Catalog must contain eight entries, one implemented");

        const string m3Json = "{\"version\":1,\"coins\":123,\"damageLevel\":3,\"attackSpeedLevel\":2,\"productionLevel\":1}";
        var options = new JsonSerializerOptions { IncludeFields = true };
        SaveData legacy = JsonSerializer.Deserialize<SaveData>(m3Json, options);
        legacy.Validate();
        Check(legacy.Checksum() == "zPMG8mHMNKc4TqLaOaK2Uslh9b1YuPOpsnwJWCLx0Co=", "M3 checksum compatibility broke");
        Check(legacy.coins == 123 && legacy.damageLevel == 3 && legacy.turrets == null, "M3 migration discarded economy or invented records");
        var modern = new SaveData { version = SaveData.CurrentVersion, coins = legacy.coins, damageLevel = legacy.damageLevel,
            attackSpeedLevel = legacy.attackSpeedLevel, productionLevel = legacy.productionLevel, turrets = state.Snapshot(), layoutInitialized = true };
        string checksum = modern.Checksum();
        SaveData roundTrip = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(modern, options), options);
        roundTrip.Validate();
        Check(roundTrip.Checksum() == checksum && roundTrip.turrets[0].instanceId == starterId, "V2 layout JSON round trip failed");
        roundTrip.turrets[0].gridX++;
        Check(roundTrip.Checksum() != checksum, "Placement coordinates were not covered by checksum");
        modern.layoutInitialized = false;
        modern.turrets = null;
        string defaultHash = modern.Checksum();
        modern.turrets = Array.Empty<PlacedTurretData>();
        Check(defaultHash == modern.Checksum(), "Initial layout checksum depends on null-array serialization");
        modern.layoutInitialized = true;
        Check(defaultHash != modern.Checksum(), "Default and sold-out layouts hash identically");
        string emptyHash = modern.Checksum(); modern.turrets = null;
        Check(emptyHash == modern.Checksum(), "Initialized empty layout checksum depends on null-array serialization");
        Check(SaveData.Initial().IsInitial && SaveData.Initial().version == 2, "Reset marker is not v2 default state");
        Console.WriteLine("PASS: M4 grid boundaries/circle, occupancy, exact/failed transactions, refunds/overflow, locked catalog, malformed/duplicate/empty restores, M3 checksum and V2 layout round trip.");
    }
}
