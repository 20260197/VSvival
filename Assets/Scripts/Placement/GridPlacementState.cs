using System;
using System.Collections.Generic;
using System.Linq;
using CookieBreaker.Combat;

namespace CookieBreaker.Placement
{
    public enum FieldMode { Normal, Placement, Selling, ConfirmationDialog, Modal }

    public readonly struct GridCell : IEquatable<GridCell>
    {
        public int X { get; }
        public int Y { get; }
        public GridCell(int x, int y) { X = x; Y = y; }
        public bool Equals(GridCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is GridCell cell && Equals(cell);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
    }

    public sealed class GridRules
    {
        public int Columns { get; }
        public int Rows { get; }
        public double BlockedX { get; }
        public double BlockedY { get; }
        public double BlockedRadius { get; }
        public double TurretClearance { get; }
        public GridRules(int columns, int rows, double blockedX, double blockedY, double blockedRadius, double turretClearance = 0)
        {
            if (columns < 1 || columns > 16 || rows < 1 || rows > 16 ||
                !Finite(blockedX) || !Finite(blockedY) || !Finite(blockedRadius) || blockedRadius < 0 || !Finite(turretClearance) || turretClearance < 0)
                throw new ArgumentOutOfRangeException(nameof(columns), "Invalid grid settings.");
            Columns = columns; Rows = rows; BlockedX = blockedX; BlockedY = blockedY; BlockedRadius = blockedRadius;
            TurretClearance = turretClearance;
        }
        public bool Allows(GridCell cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= Columns || cell.Y >= Rows) return false;
            double x = cell.X + 0.5 - BlockedX, y = cell.Y + 0.5 - BlockedY;
            double limit = BlockedRadius + TurretClearance;
            return BlockedRadius == 0 || x * x + y * y > limit * limit;
        }
        public bool TryCell(double worldX, double worldY, double originX, double originY, double size, out GridCell cell)
        {
            cell = default;
            if (!Finite(worldX) || !Finite(worldY) || !Finite(originX) || !Finite(originY) || !Finite(size) || size <= 0) return false;
            double x = Math.Floor((worldX - originX) / size), y = Math.Floor((worldY - originY) / size);
            if (x < 0 || y < 0 || x >= Columns || y >= Rows) return false;
            cell = new GridCell((int)x, (int)y);
            return true;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }

    [Serializable]
    public sealed class PlacedTurretData
    {
        public string instanceId;
        public string typeId;
        public int gridX;
        public int gridY;
        public int paidCost;
        public PlacedTurretData Copy() => (PlacedTurretData)MemberwiseClone();
    }

    [Serializable]
    public sealed class TurretDefinition
    {
        public string id;
        public string displayName;
        public bool unlocked;
        public int baseCost;
        public double costGrowth = 1.15;
        public double refundFraction = 0.5;
        // Zero means unknown/TBD for future weapons, not a working zero-damage weapon.
        public int displayBaseDamage;
        public float displayBaseInterval;
    }

    public static class TurretCatalog
    {
        public const string Catapult = "catapult";
        public static TurretDefinition[] Defaults()
        {
            string[] ids = { Catapult, "small_cannon", "rapid_cannon", "gatling", "missile", "multi_missile", "railgun", "gravity" };
            string[] names = { "Catapult", "Small Cannon", "Rapid Cannon", "Gatling", "Missile Launcher", "Multi Missile", "Railgun", "Gravity Cannon" };
            return ids.Select((id, i) => new TurretDefinition { id = id, displayName = names[i], unlocked = i == 0,
                baseCost = i == 0 ? 25 : 0, displayBaseDamage = i == 0 ? 1 : 0, displayBaseInterval = i == 0 ? 0.4f : 0 }).ToArray();
        }
    }

    // Authoritative economy/occupancy model. No Unity, input, scene or disk dependencies.
    public sealed class GridPlacementState
    {
        private readonly CoinBalance wallet;
        private readonly TurretDefinition catapult;
        private readonly Dictionary<GridCell, PlacedTurretData> records = new Dictionary<GridCell, PlacedTurretData>();
        public GridRules Grid { get; }
        public int Count => records.Count;
        public int RelocatedCount { get; private set; }
        public int CountOfType(string typeId) => records.Values.Count(x => x.typeId == typeId);
        public GridPlacementState(CoinBalance wallet, GridRules grid, TurretDefinition definition)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            catapult = definition ?? throw new ArgumentNullException(nameof(definition));
            if (catapult.id != TurretCatalog.Catapult || catapult.baseCost < 1 || double.IsNaN(catapult.costGrowth) ||
                double.IsInfinity(catapult.costGrowth) || catapult.costGrowth < 1 || double.IsNaN(catapult.refundFraction) ||
                catapult.refundFraction < 0 || catapult.refundFraction > 1)
                throw new ArgumentException("Invalid catapult price/refund configuration.");
        }
        public int? NextCost
        {
            get
            {
                return CostForCount(CountOfType(TurretCatalog.Catapult));
            }
        }
        public bool CanBuy => catapult.unlocked && NextCost.HasValue && wallet.Value >= NextCost.Value;
        public PlacedTurretData Find(GridCell cell) => records.TryGetValue(cell, out PlacedTurretData data) ? data.Copy() : null;
        public PlacedTurretData[] Snapshot() => records.Values.OrderBy(x => x.gridY).ThenBy(x => x.gridX).Select(x => x.Copy()).ToArray();

        public bool TryPlace(string typeId, GridCell cell, out PlacedTurretData placed)
        {
            placed = null;
            if (typeId != TurretCatalog.Catapult || !CanBuy || !Grid.Allows(cell) || records.ContainsKey(cell)) return false;
            int cost = NextCost.Value;
            var data = new PlacedTurretData { instanceId = Guid.NewGuid().ToString("N"), typeId = typeId,
                gridX = cell.X, gridY = cell.Y, paidCost = cost };
            if (!wallet.TrySpend(cost)) return false;
            records.Add(cell, data);
            placed = data.Copy();
            return true;
        }
        private int? CostForCount(int count)
        {
            double cost = Math.Ceiling(catapult.baseCost * Math.Pow(catapult.costGrowth, Math.Max(0, count - 1)));
            return double.IsNaN(cost) || double.IsInfinity(cost) || cost < 1 || cost > int.MaxValue ? (int?)null : (int)cost;
        }
        public int? Refund(PlacedTurretData record)
        {
            if (record == null || record.typeId != TurretCatalog.Catapult || double.IsNaN(catapult.refundFraction) ||
                catapult.refundFraction < 0 || catapult.refundFraction > 1) return null;
            int? current = CostForCount(CountOfType(record.typeId));
            return current.HasValue ? (int?)Math.Floor(current.Value * catapult.refundFraction) : null;
        }
        public bool TrySell(string instanceId, out PlacedTurretData sold, int? expectedRefund = null)
        {
            sold = records.Values.FirstOrDefault(x => x.instanceId == instanceId);
            if (sold == null) return false;
            int? quote = Refund(sold);
            if (!quote.HasValue || expectedRefund.HasValue && expectedRefund.Value != quote.Value) { sold = null; return false; }
            int refund = quote.Value;
            if (refund > int.MaxValue - wallet.Value) { sold = null; return false; }
            records.Remove(new GridCell(sold.gridX, sold.gridY));
            if (refund > 0) wallet.Credit(refund);
            sold = sold.Copy();
            return true;
        }
        public int Restore(PlacedTurretData[] saved, bool relocateBlocked = false)
        {
            PlacedTurretData[] previous = Snapshot();
            records.Clear();
            RelocatedCount = 0;
            if (saved == null)
            {
                for (int y = 0; y < Grid.Rows; y++)
                    for (int step = 0; step < Grid.Columns; step++)
                    {
                        int x = (Grid.Columns / 2 + step) % Grid.Columns;
                        if (!Grid.Allows(new GridCell(x, y))) continue;
                        records.Add(new GridCell(x, y), new PlacedTurretData { instanceId = "starter-catapult", typeId = TurretCatalog.Catapult,
                            gridX = x, gridY = y, paidCost = 0 });
                        return 0;
                    }
                return 0;
            }
            int skipped = 0;
            var ids = new HashSet<string>();
            var cells = new HashSet<GridCell>();
            var blocked = new List<PlacedTurretData>();
            foreach (PlacedTurretData data in saved)
            {
                if (data == null || data.typeId != TurretCatalog.Catapult || string.IsNullOrEmpty(data.instanceId) ||
                    data.instanceId.Length > 64 || data.paidCost < 0 || data.gridX < 0 || data.gridY < 0 ||
                    data.gridX >= Grid.Columns || data.gridY >= Grid.Rows ||
                    cells.Contains(new GridCell(data.gridX, data.gridY)) || ids.Contains(data.instanceId)) { skipped++; continue; }
                GridCell cell = new GridCell(data.gridX, data.gridY);
                if (!Grid.Allows(cell) && !relocateBlocked) { skipped++; continue; }
                cells.Add(cell); ids.Add(data.instanceId);
                if (Grid.Allows(cell)) records.Add(cell, data.Copy());
                else blocked.Add(data.Copy());
            }
            // Reserve every still-valid saved cell before relocating, so one tower cannot
            // steal another tower's valid position. Tie-break by row, then column.
            foreach (PlacedTurretData data in blocked)
            {
                GridCell? best = null;
                double bestDistance = double.PositiveInfinity;
                for (int y = 0; y < Grid.Rows; y++) for (int x = 0; x < Grid.Columns; x++)
                {
                    var cell = new GridCell(x, y);
                    double distance = (x - data.gridX) * (x - data.gridX) + (y - data.gridY) * (y - data.gridY);
                    if (!Grid.Allows(cell) || records.ContainsKey(cell) || distance >= bestDistance) continue;
                    best = cell; bestDistance = distance;
                }
                if (!best.HasValue)
                {
                    records.Clear(); foreach (PlacedTurretData old in previous) records.Add(new GridCell(old.gridX, old.gridY), old);
                    RelocatedCount = 0;
                    throw new InvalidOperationException("No free cell for the expanded cookie circle. Save must be preserved; enlarge the grid or reset explicitly.");
                }
                data.gridX = best.Value.X; data.gridY = best.Value.Y;
                records.Add(best.Value, data); RelocatedCount++;
            }
            return skipped;
        }
    }
}
