using System;
using UnityEngine;
using UnityEngine.UI;

namespace CookieBreaker.Persistence
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private AutoSaveService saves;
        [SerializeField] private GameObject settingsWindow, confirmationWindow;
        [SerializeField] private Button open, close, reset, cancel, confirm;
        [SerializeField] private Text status;
        public bool IsOpen => settingsWindow != null && settingsWindow.activeSelf;
        public event Action<bool> VisibilityChanged;

        public void Configure(AutoSaveService service, GameObject settings, GameObject confirmation,
            Button openButton, Button closeButton, Button resetButton, Button cancelButton, Button confirmButton, Text statusLabel)
        {
            saves = service; settingsWindow = settings; confirmationWindow = confirmation;
            open = openButton; close = closeButton; reset = resetButton; cancel = cancelButton; confirm = confirmButton; status = statusLabel;
        }
        private void OnEnable()
        {
            if (saves == null || settingsWindow == null || confirmationWindow == null || open == null ||
                close == null || reset == null || cancel == null || confirm == null || status == null)
            { Debug.LogError("SettingsPanel has missing references.", this); enabled = false; return; }
            open.onClick.AddListener(Open);
            close.onClick.AddListener(Close);
            reset.onClick.AddListener(RequestReset);
            cancel.onClick.AddListener(Cancel);
            confirm.onClick.AddListener(Confirm);
            saves.StatusChanged += Refresh;
            Close();
            Refresh();
        }
        private void OnDisable()
        {
            if (open != null) open.onClick.RemoveListener(Open);
            if (close != null) close.onClick.RemoveListener(Close);
            if (reset != null) reset.onClick.RemoveListener(RequestReset);
            if (cancel != null) cancel.onClick.RemoveListener(Cancel);
            if (confirm != null) confirm.onClick.RemoveListener(Confirm);
            if (saves != null) saves.StatusChanged -= Refresh;
        }
        private void Open() { settingsWindow.SetActive(true); confirmationWindow.SetActive(false); VisibilityChanged?.Invoke(true); Refresh(); }
        private void Close() { confirmationWindow.SetActive(false); settingsWindow.SetActive(false); VisibilityChanged?.Invoke(false); }
        private void RequestReset() { confirmationWindow.SetActive(true); }
        private void Cancel() { confirmationWindow.SetActive(false); }
        private void Confirm()
        {
            confirm.interactable = false;
            try { saves.ResetGame(); confirmationWindow.SetActive(false); }
            finally { confirm.interactable = true; Refresh(); }
        }
        private void Refresh() { status.text = saves.Status; }
    }
}
