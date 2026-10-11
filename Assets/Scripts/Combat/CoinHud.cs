using UnityEngine;
using UnityEngine.UI;

namespace CookieBreaker.Combat
{
    public sealed class CoinHud : MonoBehaviour
    {
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private Text coinLabel;
        [SerializeField] private RectTransform originalTitle;
        private Vector2 titleAnchorMin, titleAnchorMax, titleOffsetMin, titleOffsetMax;
        private bool titleMoved;

        public void Configure(CoinWallet coinWallet, Text label, RectTransform title)
        {
            wallet = coinWallet;
            coinLabel = label;
            originalTitle = title;
        }

        private void OnEnable()
        {
            if (wallet == null || coinLabel == null)
            {
                Debug.LogError("CoinHud needs wallet and text references.", this);
                enabled = false;
                return;
            }
            wallet.Changed += Refresh;
            Refresh(wallet.Coins);
            if (originalTitle == null) return;
            titleAnchorMin = originalTitle.anchorMin;
            titleAnchorMax = originalTitle.anchorMax;
            titleOffsetMin = originalTitle.offsetMin;
            titleOffsetMax = originalTitle.offsetMax;
            originalTitle.anchorMin = new Vector2(0.12f, 0.82f);
            originalTitle.anchorMax = new Vector2(0.88f, 0.90f);
            originalTitle.offsetMin = originalTitle.offsetMax = Vector2.zero;
            titleMoved = true;
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.Changed -= Refresh;
            if (!titleMoved || originalTitle == null) return;
            originalTitle.anchorMin = titleAnchorMin;
            originalTitle.anchorMax = titleAnchorMax;
            originalTitle.offsetMin = titleOffsetMin;
            originalTitle.offsetMax = titleOffsetMax;
            titleMoved = false;
        }

        private void Refresh(int coins) { coinLabel.text = "Coins: " + coins; }
    }
}
