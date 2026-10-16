using System;
using CookieBreaker.Combat;
using CookieBreaker.Persistence;
using CookieBreaker.Upgrades;
using UnityEngine;
using UnityEngine.UI;

namespace CookieBreaker.Placement
{
    [Serializable]
    public sealed class PlacementUiBindings
    {
        public GameObject pageOne, pageTwo, cancelBar, listWindow, infoWindow, upgradeWindow, saleWindow;
        public Button install, support, upgrade, next, previous, sell, info, cancel, listClose, infoClose, upgradeClose, saleYes, saleNo;
        public Text header, saleText, infoText;
        public Button[] buyButtons;
        public Text[] rowLabels;
    }

    public sealed class PlacementHud : MonoBehaviour
    {
        private enum Popup { None, List, Info, Upgrades }
        [SerializeField] private TurretPlacementController placement;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private UpgradeController upgrades;
        [SerializeField] private UpgradePanel upgradePanel;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private Text originalTitle;
        [SerializeField] private PlacementUiBindings ui;
        private Canvas upgradeCanvas;
        private bool oldUpgradeActive, oldTitleEnabled, pageTwo;
        private int oldSort;
        private Popup popup;

        public void Configure(TurretPlacementController controller, CoinWallet coins, UpgradeController upgradeController,
            UpgradePanel panel, SettingsPanel settingsPanel, Text title, PlacementUiBindings bindings)
        { placement = controller; wallet = coins; upgrades = upgradeController; upgradePanel = panel; settings = settingsPanel; originalTitle = title; ui = bindings; }

        private void OnEnable()
        {
            if (placement == null || wallet == null || upgrades == null || upgradePanel == null || settings == null || ui == null)
            { Debug.LogError("M4 HUD has missing references.", this); enabled = false; return; }
            upgradeCanvas = upgradePanel.GetComponentInParent<Canvas>();
            oldUpgradeActive = upgradePanel.gameObject.activeSelf;
            oldSort = upgradeCanvas.sortingOrder;
            oldTitleEnabled = originalTitle != null && originalTitle.enabled;
            ui.install.onClick.AddListener(OpenList); ui.upgrade.onClick.AddListener(OpenUpgrades);
            ui.next.onClick.AddListener(Next); ui.previous.onClick.AddListener(Previous);
            ui.sell.onClick.AddListener(placement.BeginSelling); ui.info.onClick.AddListener(OpenInfo);
            ui.cancel.onClick.AddListener(placement.Cancel); ui.saleNo.onClick.AddListener(placement.Cancel);
            ui.saleYes.onClick.AddListener(placement.ConfirmSale);
            ui.listClose.onClick.AddListener(ClosePopup); ui.infoClose.onClick.AddListener(ClosePopup); ui.upgradeClose.onClick.AddListener(ClosePopup);
            for (int i = 0; i < ui.buyButtons.Length; i++)
                if (placement.Definitions[i].id == TurretCatalog.Catapult) ui.buyButtons[i].onClick.AddListener(placement.BeginPlacement);
            ui.support.interactable = false;
            placement.Changed += Refresh; wallet.Changed += CoinsChanged; upgrades.Changed += Refresh;
            settings.VisibilityChanged += SettingsChanged;
            settings.ResetCompleted += ResetUi;
            Refresh();
        }
        private void OnDisable()
        {
            if (ui == null || placement == null) return;
            ui.install.onClick.RemoveListener(OpenList); ui.upgrade.onClick.RemoveListener(OpenUpgrades);
            ui.next.onClick.RemoveListener(Next); ui.previous.onClick.RemoveListener(Previous);
            ui.sell.onClick.RemoveListener(placement.BeginSelling); ui.info.onClick.RemoveListener(OpenInfo);
            ui.cancel.onClick.RemoveListener(placement.Cancel); ui.saleNo.onClick.RemoveListener(placement.Cancel);
            ui.saleYes.onClick.RemoveListener(placement.ConfirmSale);
            ui.listClose.onClick.RemoveListener(ClosePopup); ui.infoClose.onClick.RemoveListener(ClosePopup); ui.upgradeClose.onClick.RemoveListener(ClosePopup);
            foreach (Button button in ui.buyButtons) button.onClick.RemoveListener(placement.BeginPlacement);
            placement.Changed -= Refresh;
            if (wallet != null) wallet.Changed -= CoinsChanged;
            if (upgrades != null) upgrades.Changed -= Refresh;
            if (settings != null) { settings.VisibilityChanged -= SettingsChanged; settings.ResetCompleted -= ResetUi; }
            if (upgradePanel != null) upgradePanel.gameObject.SetActive(oldUpgradeActive);
            if (upgradeCanvas != null) upgradeCanvas.sortingOrder = oldSort;
            if (originalTitle != null) originalTitle.enabled = oldTitleEnabled;
        }
        private void OpenList() { if (placement.Mode != FieldMode.Normal) return; popup = Popup.List; placement.EnterModal(); }
        private void OpenInfo() { if (placement.Mode != FieldMode.Normal) return; popup = Popup.Info; placement.EnterModal(); }
        private void OpenUpgrades() { if (placement.Mode != FieldMode.Normal) return; popup = Popup.Upgrades; placement.EnterModal(); }
        private void ClosePopup() { popup = Popup.None; placement.Cancel(); }
        private void Next() { if (placement.Mode == FieldMode.Normal) { pageTwo = true; Refresh(); } }
        private void Previous() { if (placement.Mode == FieldMode.Normal) { pageTwo = false; Refresh(); } }
        private void CoinsChanged(int coins) { Refresh(); }
        private void SettingsChanged(bool open) { popup = Popup.None; if (open) placement.EnterModal(); else placement.Cancel(); Refresh(); }
        private void ResetUi() { popup = Popup.None; pageTwo = false; placement.Cancel(); }

        private void Refresh()
        {
            if (settings.IsOpen && placement.Mode != FieldMode.Modal) { placement.EnterModal(); return; }
            FieldMode mode = placement.Mode;
            if (mode != FieldMode.Modal) popup = Popup.None;
            bool modal = mode == FieldMode.Modal && !settings.IsOpen;
            ui.pageOne.SetActive(mode == FieldMode.Normal && !pageTwo);
            ui.pageTwo.SetActive(mode == FieldMode.Normal && pageTwo);
            ui.cancelBar.SetActive(mode == FieldMode.Placement || mode == FieldMode.Selling);
            ui.listWindow.SetActive(modal && popup == Popup.List);
            ui.infoWindow.SetActive(modal && popup == Popup.Info);
            ui.upgradeWindow.SetActive(modal && popup == Popup.Upgrades);
            ui.saleWindow.SetActive(mode == FieldMode.ConfirmationDialog);
            bool showUpgrades = modal && popup == Popup.Upgrades;
            upgradeCanvas.sortingOrder = showUpgrades ? 60 : oldSort;
            upgradePanel.gameObject.SetActive(showUpgrades);
            ui.header.gameObject.SetActive(mode == FieldMode.Placement || mode == FieldMode.Selling);
            if (originalTitle != null) originalTitle.enabled = oldTitleEnabled && mode == FieldMode.Normal;
            ui.header.text = placement.Message + (mode == FieldMode.Placement ? "\nRequired: " + Cost() + " coins" : "");
            ui.infoText.text = $"Turrets: {placement.Count}\nCoins: {wallet.Coins}\nCatapult damage: {placement.Damage}\nAttack period: {placement.FireInterval:0.00}s";
            ui.saleYes.interactable = placement.SaleRefund.HasValue;
            ui.saleText.text = "Sell Catapult for " + (placement.SaleRefund.HasValue ? placement.SaleRefund.Value.ToString() : "LIMIT") + " coins?\n" +
                (placement.Count == 1 ? "This is your last turret. Attacks will stop.\n" : "") + placement.Message;
            for (int i = 0; i < ui.rowLabels.Length; i++)
            {
                TurretDefinition definition = placement.Definitions[i];
                bool catapult = definition.id == TurretCatalog.Catapult;
                string damage = definition.displayBaseDamage > 0 ? definition.displayBaseDamage.ToString() : "TBD";
                string period = definition.displayBaseInterval > 0 ? definition.displayBaseInterval.ToString("0.00") + "s" : "TBD";
                string price = definition.baseCost > 0 ? definition.baseCost.ToString() : "TBD";
                ui.rowLabels[i].text = catapult ? $"{definition.displayName}  x{placement.Count}\nDMG {placement.Damage} | Period {placement.FireInterval:0.00}s | Next {Cost()} coins" :
                    $"{definition.displayName}  x0\nDMG {damage} | Period {period} | Price {price} (M5+)";
                ui.buyButtons[i].interactable = catapult && placement.CanBuy;
            }
        }
        private string Cost() => placement.NextCost.HasValue ? placement.NextCost.Value.ToString() : "LIMIT";
    }
}
