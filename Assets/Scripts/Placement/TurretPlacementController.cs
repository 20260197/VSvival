using System;
using System.Collections.Generic;
using System.Linq;
using CookieBreaker.Combat;
using CookieBreaker.Upgrades;
using UnityEngine;

namespace CookieBreaker.Placement
{
    [DefaultExecutionOrder(-120)]
    public sealed class TurretPlacementController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private UpgradeController upgrades;
        [SerializeField] private AutomaticTurret originalTurret;
        [SerializeField] private Transform turretContainer;
        [SerializeField] private Transform blockedIndicator;
        [SerializeField, Range(1, 16)] private int columns = 8, rows = 9;
        [SerializeField, Min(0.1f)] private float maximumCellSize = 0.6f;
        [SerializeField] private Rect fieldViewport = new Rect(0.08f, 0.24f, 0.84f, 0.60f);
        [SerializeField] private Vector2 blockedCenter = new Vector2(0.5f, 0.55f);
        [SerializeField, Min(0)] private float blockedRadiusCells = 1.6f;
        [SerializeField] private TurretDefinition[] definitions = TurretCatalog.Defaults();
        private readonly Dictionary<string, AutomaticTurret> live = new Dictionary<string, AutomaticTurret>();
        private GridPlacementState state;
        private Vector3 origin;
        private float cellSize;
        private bool originalWasActive;
        private bool convertedOriginal;
        private bool hasGeometry;
        public event Action Changed;
        public event Action TransactionCommitted;
        public FieldMode Mode { get; private set; }
        public string Message { get; private set; } = "";
        public PlacedTurretData SaleTarget { get; private set; }
        public bool IsRestored { get; private set; }
        public int Count => state?.Count ?? 0;
        public int? NextCost => state?.NextCost;
        public bool CanBuy => state != null && state.CanBuy;
        public IReadOnlyList<TurretDefinition> Definitions => definitions;
        public int Damage => originalTurret.Damage;
        public float FireInterval => originalTurret.FireInterval;
        public int SaleRefund => SaleTarget == null ? 0 : state.Refund(SaleTarget);

        public void Configure(Camera camera, CoinWallet coins, UpgradeController controller, AutomaticTurret original, Transform container, Transform indicator)
        { worldCamera = camera; wallet = coins; upgrades = controller; originalTurret = original; turretContainer = container; blockedIndicator = indicator; }

        private void Awake()
        {
            if (worldCamera == null || wallet == null || upgrades == null || originalTurret == null || turretContainer == null ||
                !worldCamera.orthographic || !upgrades.EnsureReady())
            { Debug.LogError("M4 needs the existing M1/M2 references and an orthographic camera.", this); enabled = false; return; }
            var grid = new GridRules(columns, rows, columns * blockedCenter.x, rows * blockedCenter.y, blockedRadiusCells);
            state = new GridPlacementState(wallet.Balance, grid, definitions.Single(x => x.id == TurretCatalog.Catapult));
            originalWasActive = originalTurret.gameObject.activeSelf;
            originalTurret.gameObject.SetActive(false);
            convertedOriginal = true;
        }
        private void OnEnable() { if (upgrades != null) upgrades.Changed += ApplyUpgrades; }
        private void OnDisable() { if (upgrades != null) upgrades.Changed -= ApplyUpgrades; }
        private void Start() { if (state != null && !IsRestored) RestoreRecords(null); }
        private void OnDestroy()
        {
            ClearLive();
            if (convertedOriginal && originalTurret != null) originalTurret.gameObject.SetActive(originalWasActive);
        }
        private void Update() { if (state != null) UpdateGeometry(); }

        public void RestoreRecords(PlacedTurretData[] saved)
        {
            if (state == null) throw new InvalidOperationException("M4 grid is not initialized.");
            ClearLive();
            int skipped = state.Restore(saved);
            UpdateGeometry();
            foreach (PlacedTurretData record in state.Snapshot()) live.Add(record.instanceId, CreateTurret(record));
            IsRestored = true;
            SaleTarget = null;
            SetMode(FieldMode.Normal, "");
            if (skipped > 0) Debug.LogWarning($"M4 skipped {skipped} invalid, duplicate, blocked or unimplemented turret records.", this);
        }
        public PlacedTurretData[] CaptureRecords() => state?.Snapshot();
        public void EnterModal() { SaleTarget = null; SetMode(FieldMode.Modal, ""); }
        public void BeginPlacement()
        {
            if (Mode != FieldMode.Modal || !CanBuy) return;
            SetMode(FieldMode.Placement, "Tap an empty field cell to place a Catapult.");
        }
        public void BeginSelling()
        {
            if (Mode != FieldMode.Normal) return;
            SetMode(FieldMode.Selling, "Tap a turret to sell it.");
        }
        public void Cancel()
        {
            FieldMode next = Mode == FieldMode.ConfirmationDialog ? FieldMode.Selling : FieldMode.Normal;
            SaleTarget = null;
            SetMode(next, next == FieldMode.Selling ? "Tap a turret to sell it." : "");
        }

        public void HandleFieldClick(Vector2 screen)
        {
            if (state == null || (Mode != FieldMode.Placement && Mode != FieldMode.Selling)) return;
            UpdateGeometry();
            Ray ray = worldCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 world = ray.GetPoint(distance);
            if (!state.Grid.TryCell(world.x, world.y, origin.x, origin.y, cellSize, out GridCell cell))
            { SetMode(Mode, "Outside the placement field."); return; }
            if (Mode == FieldMode.Selling)
            {
                PlacedTurretData target = state.Find(cell);
                if (target == null || (world - Center(cell)).sqrMagnitude > cellSize * cellSize * 0.45f * 0.45f)
                { SetMode(Mode, "No turret selected. Tap its base."); return; }
                SaleTarget = target;
                SetMode(FieldMode.ConfirmationDialog, "Confirm the sale.");
                return;
            }
            if (!state.Grid.Allows(cell) || state.Find(cell) != null || !CanBuy)
            { SetMode(Mode, "Cell blocked/occupied, or not enough coins."); return; }
            // Build the inactive hierarchy before committing coins/occupancy.
            var provisional = new PlacedTurretData { gridX = cell.X, gridY = cell.Y };
            AutomaticTurret turret;
            try { turret = CreateTurret(provisional, false); }
            catch (Exception error) { Debug.LogException(error, this); return; }
            if (!state.TryPlace(TurretCatalog.Catapult, cell, out PlacedTurretData placed))
            { Destroy(turret.gameObject); return; }
            turret.name = "Catapult " + placed.instanceId;
            live.Add(placed.instanceId, turret);
            turret.gameObject.SetActive(true);
            wallet.NotifyBalanceChanged();
            SetMode(FieldMode.Normal, "");
            TransactionCommitted?.Invoke();
        }
        public void ConfirmSale()
        {
            if (Mode != FieldMode.ConfirmationDialog || SaleTarget == null) return;
            if (!state.TrySell(SaleTarget.instanceId, out PlacedTurretData sold))
            { SetMode(Mode, "Sale unavailable (target changed or coin limit reached)."); return; }
            if (live.TryGetValue(sold.instanceId, out AutomaticTurret turret))
            {
                RemoveTurret(turret);
                live.Remove(sold.instanceId);
            }
            SaleTarget = null;
            wallet.NotifyBalanceChanged();
            SetMode(FieldMode.Selling, "Tap a turret to sell it.");
            TransactionCommitted?.Invoke();
        }
        private AutomaticTurret CreateTurret(PlacedTurretData record, bool activate = true)
        {
            AutomaticTurret turret = Instantiate(originalTurret, turretContainer);
            turret.name = "Catapult " + record.instanceId;
            turret.transform.localScale = Vector3.one * (cellSize / 0.9f);
            turret.PlaceAt(Center(new GridCell(record.gridX, record.gridY)));
            turret.Damage = originalTurret.Damage;
            turret.FireInterval = originalTurret.FireInterval;
            turret.gameObject.SetActive(activate);
            return turret;
        }
        private void ApplyUpgrades()
        {
            foreach (AutomaticTurret turret in live.Values)
            { if (turret == null) continue; turret.Damage = originalTurret.Damage; turret.FireInterval = originalTurret.FireInterval; }
        }
        private void UpdateGeometry()
        {
            Vector3 min = CookieSpawner.ViewportPosition(worldCamera, fieldViewport.min);
            Vector3 max = CookieSpawner.ViewportPosition(worldCamera, fieldViewport.max);
            float size = Mathf.Max(0.01f, Mathf.Min(maximumCellSize, (max.x - min.x) / state.Grid.Columns, (max.y - min.y) / state.Grid.Rows));
            Vector3 nextOrigin = (min + max) * 0.5f - new Vector3(state.Grid.Columns * size, state.Grid.Rows * size, 0) * 0.5f;
            if (hasGeometry && size == cellSize && nextOrigin == origin) return;
            cellSize = size; origin = nextOrigin; hasGeometry = true;
            if (blockedIndicator != null)
            {
                blockedIndicator.gameObject.SetActive(state.Grid.BlockedRadius > 0);
                blockedIndicator.position = origin + new Vector3((float)state.Grid.BlockedX * size, (float)state.Grid.BlockedY * size, 0);
                blockedIndicator.localScale = Vector3.one * (float)(2 * state.Grid.BlockedRadius * size);
            }
            foreach (PlacedTurretData record in state.Snapshot())
                if (live.TryGetValue(record.instanceId, out AutomaticTurret turret) && turret != null)
                { turret.PlaceAt(Center(new GridCell(record.gridX, record.gridY))); turret.transform.localScale = Vector3.one * (cellSize / 0.9f); }
        }
        private Vector3 Center(GridCell cell) => origin + new Vector3((cell.X + 0.5f) * cellSize, (cell.Y + 0.5f) * cellSize, 0);
        private static void RemoveTurret(AutomaticTurret turret)
        {
            if (turret == null) return;
            turret.ClearActiveProjectiles();
            turret.gameObject.SetActive(false);
            Destroy(turret.gameObject);
        }
        private void ClearLive() { foreach (AutomaticTurret turret in live.Values) RemoveTurret(turret); live.Clear(); }
        private void SetMode(FieldMode mode, string message) { Mode = mode; Message = message; Changed?.Invoke(); }
    }
}
