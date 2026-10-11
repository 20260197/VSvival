using CookieBreaker.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CookieBreaker.Upgrades
{
    public sealed class UpgradePanel : MonoBehaviour
    {
        [SerializeField] private UpgradeController controller;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private Button damageButton, speedButton, productionButton;
        [SerializeField] private Text damageLabel, speedLabel, productionLabel;

        public void Configure(UpgradeController upgrades, CoinWallet coinWallet,
            Button damage, Text damageText, Button speed, Text speedText, Button production, Text productionText)
        {
            controller = upgrades;
            wallet = coinWallet;
            damageButton = damage; damageLabel = damageText;
            speedButton = speed; speedLabel = speedText;
            productionButton = production; productionLabel = productionText;
        }

        private void OnEnable()
        {
            if (controller == null || wallet == null || damageButton == null || speedButton == null ||
                productionButton == null || damageLabel == null || speedLabel == null || productionLabel == null ||
                !controller.EnsureReady())
            {
                Debug.LogError("UpgradePanel needs its controller, wallet and all three button/label pairs.", this);
                enabled = false;
                return;
            }
            damageButton.onClick.AddListener(BuyDamage);
            speedButton.onClick.AddListener(BuySpeed);
            productionButton.onClick.AddListener(BuyProduction);
            wallet.Changed += CoinsChanged;
            controller.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.Changed -= CoinsChanged;
            if (controller != null) controller.Changed -= Refresh;
            if (damageButton != null) damageButton.onClick.RemoveListener(BuyDamage);
            if (speedButton != null) speedButton.onClick.RemoveListener(BuySpeed);
            if (productionButton != null) productionButton.onClick.RemoveListener(BuyProduction);
        }

        private void BuyDamage() => controller.TryPurchase(UpgradeKind.Damage);
        private void BuySpeed() => controller.TryPurchase(UpgradeKind.AttackSpeed);
        private void BuyProduction() => controller.TryPurchase(UpgradeKind.Production);
        private void CoinsChanged(int coins) => Refresh();

        private void Refresh()
        {
            UpgradeState state = controller.State;
            Show(UpgradeKind.Damage, damageButton, damageLabel, "Damage", "DMG " + state.Damage);
            Show(UpgradeKind.AttackSpeed, speedButton, speedLabel, "Attack Speed", $"{1 / state.FireInterval:0.00}/sec");
            Show(UpgradeKind.Production, productionButton, productionLabel, "Production", $"{1 / state.SpawnInterval:0.00}/sec");
        }

        private void Show(UpgradeKind kind, Button button, Text label, string title, string effect)
        {
            UpgradeOffer offer = controller.State.Offer(kind);
            button.interactable = controller.isActiveAndEnabled && offer.CanPurchase(wallet.Coins);
            string cost = offer.Limit == UpgradeLimit.EffectCap ? "MAX" :
                offer.Limit == UpgradeLimit.CostLimit ? "COST LIMIT" : offer.Cost.Value + " coins";
            label.text = $"{title}\nLv {offer.Level} | {effect}\n{cost}";
        }
    }
}
