using System;
using CookieBreaker.Combat;
using CookieBreaker.Upgrades;
using CookieBreaker.Placement;
using UnityEngine;

namespace CookieBreaker.Persistence
{
    [DefaultExecutionOrder(-100)]
    public sealed class AutoSaveService : MonoBehaviour
    {
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private UpgradeController upgrades;
        [SerializeField] private TurretPlacementController placement;
        private PlacedTurretData[] preservedTurrets;
        private bool preservedLayoutInitialized;
        public void ConfigurePlacement(TurretPlacementController controller) { placement = controller; }
        private SaveFileStore store;
        private bool initialized, writesAllowed, transitioning;
        private float elapsed;
        public event Action StatusChanged;
        public string Status { get; private set; } = "Not initialized";

        public void Configure(CoinWallet coinWallet, UpgradeController controller)
        { wallet = coinWallet; upgrades = controller; }

        private void Awake()
        {
            if (wallet == null || upgrades == null || !upgrades.EnsureReady())
            {
                Debug.LogError("AutoSaveService needs the existing M1 wallet and M2 controller.", this);
                enabled = false;
                return;
            }
            store = new SaveFileStore(Application.persistentDataPath, new JsonSaveCodec(), message => Debug.LogWarning(message, this));
            SaveLoadStatus result = store.Load(out SaveData data);
            writesAllowed = result != SaveLoadStatus.Invalid && result != SaveLoadStatus.UnsupportedVersion;
            if (data != null && writesAllowed)
            {
                try { Restore(data); }
                catch (Exception error)
                {
                    writesAllowed = false;
                    Debug.LogError("Save does not match current gameplay rules; preserved without overwriting. " + error, this);
                }
            }
            if (placement != null && !placement.IsRestored) placement.RestoreRecords(null);
            initialized = true;
            SetStatus(writesAllowed ? "Ready (" + result + ")" : "Save could not be loaded. Files preserved; reset required to save.");
            Debug.Log("Cookie Breaker save directory: " + Application.persistentDataPath, this);
        }

        private void OnEnable()
        { if (upgrades != null) upgrades.Purchased += SaveNow; if (placement != null) placement.TransactionCommitted += SaveNow; }
        private void OnDisable()
        { if (upgrades != null) upgrades.Purchased -= SaveNow; if (placement != null) placement.TransactionCommitted -= SaveNow; }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 30) return;
            elapsed = 0;
            SaveNow();
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnApplicationFocus(bool focused) { if (!focused) SaveNow(); }
        private void OnApplicationQuit() { SaveNow(); }

        public void SaveNow()
        {
            if (!initialized || !writesAllowed || transitioning) return;
            SaveData data = Capture();
            SetStatus(store.Save(data) ? "Progress saved automatically." : "Save failed. See Console; progress will be retried.");
        }

        public bool ResetGame()
        {
            if (!initialized || transitioning) return false;
            transitioning = true;
            try
            {
                if (!store.Reset()) { SetStatus("Reset failed. Current progress was kept. See Console."); return false; }
                upgrades.ClearTransientCombat();
                Restore(SaveData.Initial());
                writesAllowed = true;
                elapsed = 0;
                SetStatus("Game reset and initial progress saved.");
                return true;
            }
            finally { transitioning = false; }
        }

        private SaveData Capture() => new SaveData
        {
            version = SaveData.CurrentVersion,
            coins = wallet.Coins,
            damageLevel = upgrades.State.Offer(UpgradeKind.Damage).Level,
            attackSpeedLevel = upgrades.State.Offer(UpgradeKind.AttackSpeed).Level,
            productionLevel = upgrades.State.Offer(UpgradeKind.Production).Level,
            turrets = placement != null ? placement.CaptureRecords() : preservedTurrets,
            layoutInitialized = placement != null ? placement.IsRestored : preservedLayoutInitialized
        };
        private void Restore(SaveData data)
        {
            upgrades.RestoreProgress(data.coins, data.damageLevel, data.attackSpeedLevel, data.productionLevel);
            preservedLayoutInitialized = data.version >= 2 && data.layoutInitialized;
            preservedTurrets = preservedLayoutInitialized ? data.turrets ?? Array.Empty<PlacedTurretData>() : null;
            if (placement != null) placement.RestoreRecords(preservedTurrets);
        }
        private void SetStatus(string value) { Status = value; StatusChanged?.Invoke(); }
    }
}
