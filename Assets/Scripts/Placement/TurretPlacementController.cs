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
        [SerializeField, Min(0)] private float blockedRadiusCells = 2.0f;
        [SerializeField, Min(0)] private float turretClearanceCells = 0.75f;
        [SerializeField] private TurretDefinition[] definitions = TurretCatalog.Defaults();
        private readonly Dictionary<string, AutomaticTurret> live = new Dictionary<string, AutomaticTurret>();
        private GridPlacementState state;
        private Vector3 origin;
        private float cellSize;
        private bool originalWasActive;
        private bool convertedOriginal;
        private bool hasGeometry;
        private int? saleQuote;
        private GameObject preview;
        private SpriteRenderer[] previewRenderers;
        private bool previewActive, previewOverField;
        private Vector2 previewScreen;
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
        public int? SaleRefund => SaleTarget == null ? null : saleQuote;

        public void Configure(Camera camera, CoinWallet coins, UpgradeController controller, AutomaticTurret original, Transform container, Transform indicator)
        { worldCamera = camera; wallet = coins; upgrades = controller; originalTurret = original; turretContainer = container; blockedIndicator = indicator; }

        private void Awake()
        {
            if (worldCamera == null || wallet == null || upgrades == null || originalTurret == null || turretContainer == null ||
                originalTurret.Spawner == null || !worldCamera.orthographic || !upgrades.EnsureReady())
            { Debug.LogError("M4 needs the existing M1/M2 references and an orthographic camera.", this); enabled = false; return; }
            var grid = new GridRules(columns, rows, columns * blockedCenter.x, rows * blockedCenter.y, blockedRadiusCells, turretClearanceCells);
            state = new GridPlacementState(wallet.Balance, grid, definitions.Single(x => x.id == TurretCatalog.Catapult));
            originalWasActive = originalTurret.gameObject.activeSelf;
            originalTurret.gameObject.SetActive(false);
            convertedOriginal = true;
        }
        private void OnEnable() { if (upgrades != null) upgrades.Changed += ApplyUpgrades; }
        private void OnDisable() { CancelPreview(); if (upgrades != null) upgrades.Changed -= ApplyUpgrades; }
        private void Start() { if (state != null && !IsRestored) RestoreRecords(null); }
        private void OnDestroy()
        {
            ClearLive();
            if (convertedOriginal && originalTurret != null) originalTurret.gameObject.SetActive(originalWasActive);
        }
        private void Update()
        {
            if (state == null) return;
            UpdateGeometry();
            if (previewActive) UpdatePreview(previewScreen, previewOverField);
        }

        public void RestoreRecords(PlacedTurretData[] saved)
        {
            if (state == null) throw new InvalidOperationException("M4 grid is not initialized.");
            CancelPreview(); ClearLive();
            int skipped = state.Restore(saved, relocateBlocked: true);
            UpdateGeometry();
            foreach (PlacedTurretData record in state.Snapshot()) live.Add(record.instanceId, CreateTurret(record));
            IsRestored = true;
            SaleTarget = null;
            SetMode(FieldMode.Normal, "");
            if (skipped > 0) Debug.LogWarning($"M4 skipped {skipped} invalid, duplicate, blocked or unimplemented turret records.", this);
            if (state.RelocatedCount > 0) Debug.LogWarning($"M4.1 moved {state.RelocatedCount} saved turrets to nearest free cells outside the expanded cookie area; IDs and economy were preserved.", this);
        }
        public PlacedTurretData[] CaptureRecords() => state?.Snapshot();
        public void EnterModal() { SaleTarget = null; SetMode(FieldMode.Modal, ""); }
        public void BeginPlacement()
        {
            if (Mode != FieldMode.Modal || !CanBuy) return;
            SetMode(FieldMode.Placement, "Hold and drag to preview. Release on the field to install.");
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
            // Click is only for sale selection; placement is committed exclusively on release.
            if (state == null || Mode != FieldMode.Selling) return;
            if (!TryScreenCell(screen, out GridCell cell, out Vector3 world))
            { SetMode(Mode, "Outside the placement field."); return; }
            PlacedTurretData target = state.Find(cell);
            if (target == null || (world - Center(cell)).sqrMagnitude > cellSize * cellSize * 0.45f * 0.45f)
            { SetMode(Mode, "No turret selected. Tap its base."); return; }
            SaleTarget = target; saleQuote = state.Refund(target);
            SetMode(FieldMode.ConfirmationDialog, "Confirm the sale.");
        }

        private bool TryScreenCell(Vector2 screen, out GridCell cell, out Vector3 world)
        {
            cell = default; world = default;
            UpdateGeometry();
            Ray ray = worldCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return state.Grid.TryCell(world.x, world.y, origin.x, origin.y, cellSize, out cell);
        }

        public bool BeginPreview(Vector2 screen, bool overField)
        {
            if (state == null || Mode != FieldMode.Placement || previewActive) return false;
            CreatePreview(); previewActive = true;
            UpdatePreview(screen, overField);
            return true;
        }
        public void UpdatePreview(Vector2 screen, bool overField)
        {
            if (!previewActive) return;
            previewScreen = screen; previewOverField = overField;
            if (!overField || !TryScreenCell(screen, out GridCell cell, out _)) { preview.SetActive(false); return; }
            preview.SetActive(true);
            preview.transform.position = Center(cell);
            preview.transform.localScale = Vector3.one * (cellSize / 0.9f);
            bool valid = state.Grid.Allows(cell) && state.Find(cell) == null && CanBuy;
            Color tint = valid ? new Color(0.25f, 1, 0.4f, 0.5f) : new Color(1, 0.2f, 0.2f, 0.5f);
            foreach (SpriteRenderer renderer in previewRenderers) renderer.color = tint;
        }
        public void EndPreview(Vector2 screen, bool overField)
        {
            if (!previewActive) return;
            GridCell cell = default;
            bool commit = overField && Mode == FieldMode.Placement && TryScreenCell(screen, out cell, out _);
            CancelPreview();
            if (commit) CommitPlacement(cell);
        }
        public void CancelPreview()
        {
            previewActive = false;
            if (preview != null) { preview.SetActive(false); Destroy(preview); }
            preview = null; previewRenderers = null;
        }
        private void CreatePreview()
        {
            preview = new GameObject("Catapult visual preview"); preview.SetActive(false);
            preview.transform.SetParent(transform, false);
            foreach (SpriteRenderer source in originalTurret.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var visual = new GameObject(source.name, typeof(SpriteRenderer)); visual.transform.SetParent(preview.transform, false);
                visual.transform.localPosition = originalTurret.transform.InverseTransformPoint(source.transform.position);
                visual.transform.localRotation = Quaternion.Inverse(originalTurret.transform.rotation) * source.transform.rotation;
                Vector3 scale = originalTurret.transform.lossyScale, worldScale = source.transform.lossyScale;
                visual.transform.localScale = new Vector3(worldScale.x / scale.x, worldScale.y / scale.y, worldScale.z / scale.z);
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>(); renderer.sprite = source.sprite;
                renderer.sharedMaterial = source.sharedMaterial; renderer.flipX = source.flipX; renderer.flipY = source.flipY; renderer.enabled = source.enabled;
                renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + 10;
                PlaceholderGraphic graphic = source.GetComponent<PlaceholderGraphic>();
                if (source.sprite == null && graphic != null) visual.AddComponent<PlaceholderGraphic>().Configure(graphic.ConfiguredShape);
            }
            previewRenderers = preview.GetComponentsInChildren<SpriteRenderer>(true);
        }
        private void CommitPlacement(GridCell cell)
        {
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
            int? latest = state.Refund(SaleTarget);
            if (latest != saleQuote)
            { saleQuote = latest; SetMode(Mode, "Price changed. Review the new amount and confirm again."); return; }
            if (!state.TrySell(SaleTarget.instanceId, out PlacedTurretData sold, expectedRefund: saleQuote))
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
            Vector3 center = origin + new Vector3((float)state.Grid.BlockedX * size, (float)state.Grid.BlockedY * size, 0);
            float radius = (float)state.Grid.BlockedRadius * size;
            originalTurret.Spawner.ConfigureCircularArea(center, radius);
            if (blockedIndicator != null)
            {
                blockedIndicator.gameObject.SetActive(state.Grid.BlockedRadius > 0);
                blockedIndicator.position = center;
                blockedIndicator.localScale = Vector3.one * (2 * radius);
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
        private void SetMode(FieldMode mode, string message)
        { if (mode != FieldMode.Placement) CancelPreview(); Mode = mode; Message = message; Changed?.Invoke(); }
    }
}
