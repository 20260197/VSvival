using System;
using System.Linq;
using CookieBreaker.Combat;
using CookieBreaker.Persistence;
using CookieBreaker.Placement;
using CookieBreaker.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CookieBreaker.Editor
{
    public static class M4GridPlacementSetup
    {
        private const string RootName = "Cookie Breaker M4";
        [MenuItem("Cookie Breaker/M4/Add Grid Placement to Open Main Scene")]
        public static void AddPlacement()
        {
            if (Application.unityVersion != "6000.5.1f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Unity 6000.5.1f1 in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != CookieBreakerProjectSetup.MainScenePath) throw new InvalidOperationException("Open CookieBreakerMain first.");
            GameObject[] roots = scene.GetRootGameObjects();
            AutoSaveService saves = One<AutoSaveService>(roots);
            TurretPlacementController[] existing = UnityEngine.Object.FindObjectsByType<TurretPlacementController>(FindObjectsInactive.Include);
            GameObject oldRoot = roots.FirstOrDefault(x => x.name == RootName);
            var serializedSaves = new SerializedObject(saves);
            if (existing.Length == 1 && oldRoot != null && existing[0].gameObject == oldRoot &&
                oldRoot.GetComponentInChildren<PlacementHud>(true) != null &&
                serializedSaves.FindProperty("placement").objectReferenceValue == existing[0])
            { Selection.activeGameObject = oldRoot; Debug.Log("M4 already installed; selected without duplication or overwrite."); return; }
            if (existing.Length > 0 || oldRoot != null || serializedSaves.FindProperty("placement").objectReferenceValue != null)
                throw new InvalidOperationException("Inspect existing/partial M4 objects and save reference. Nothing was overwritten.");
            CoinWallet wallet = One<CoinWallet>(roots);
            UpgradeController upgrades = One<UpgradeController>(roots);
            AutomaticTurret turret = One<AutomaticTurret>(roots);
            UpgradePanel upgradePanel = One<UpgradePanel>(roots);
            SettingsPanel settings = One<SettingsPanel>(roots);
            Camera camera = One<Camera>(roots);
            if (!camera.orthographic || !camera.CompareTag("MainCamera")) throw new InvalidOperationException("Keep one orthographic MainCamera.");
            EventSystem[] inputs = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (inputs.Length != 1 || inputs[0].gameObject.scene != scene || !inputs[0].isActiveAndEnabled ||
                inputs[0].GetComponents<BaseInputModule>().Length != 1 || inputs[0].GetComponent<InputSystemUIInputModule>() == null ||
                !inputs[0].GetComponent<InputSystemUIInputModule>().isActiveAndEnabled)
                throw new InvalidOperationException("Keep the working M2/M3 InputSystem EventSystem; no new input system was added.");
            if (upgradePanel.GetComponentInParent<Canvas>() == null) throw new InvalidOperationException("Existing upgrade UI needs its own Canvas.");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) throw new InvalidOperationException("Built-in font unavailable.");
            Text title = roots.SelectMany(x => x.GetComponentsInChildren<Text>(true)).FirstOrDefault(x => x.name == "Game Title");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add M4 grid placement");
            GameObject root = null;
            try
            {
                root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene); root.SetActive(false);
                TurretPlacementController placement = root.AddComponent<TurretPlacementController>();
                Transform container = Child("Placed Turrets", root.transform).transform;
                GameObject blocked = Child("Central Exclusion Indicator", root.transform, typeof(SpriteRenderer), typeof(PlaceholderGraphic));
                blocked.GetComponent<SpriteRenderer>().color = new Color(1, 0.45f, 0.1f, 0.20f);
                blocked.GetComponent<SpriteRenderer>().sortingOrder = 0;
                blocked.GetComponent<PlaceholderGraphic>().Configure(PlaceholderGraphic.Shape.Disc);
                placement.Configure(camera, wallet, upgrades, turret, container, blocked.transform);
                GameObject canvasObject = Child("Placement Canvas", root.transform, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
                GameObject field = Image("Field Input", canvasObject.transform, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.84f), Color.clear);
                field.AddComponent<PlacementFieldInput>().Configure(placement);
                GameObject safe = Child("Safe Area", canvasObject.transform, typeof(RectTransform), typeof(SafeAreaPanel));
                SetRect(safe, Vector2.zero, Vector2.one);
                var ui = new PlacementUiBindings();
                ui.header = Label("Mode Guide", safe.transform, "", font, new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.90f), 32);
                ui.pageOne = Panel("Menu Page 1", safe.transform);
                ui.install = Button("Install Turret", ui.pageOne.transform, font, 0.02f, 0.25f);
                ui.support = Button("Support\n(M5+)", ui.pageOne.transform, font, 0.27f, 0.50f); ui.support.interactable = false;
                ui.upgrade = Button("Upgrades", ui.pageOne.transform, font, 0.52f, 0.79f);
                ui.next = Button(">", ui.pageOne.transform, font, 0.81f, 0.98f);
                ui.pageTwo = Panel("Menu Page 2", safe.transform);
                ui.previous = Button("<", ui.pageTwo.transform, font, 0.02f, 0.19f);
                ui.sell = Button("Sell Turret", ui.pageTwo.transform, font, 0.21f, 0.59f);
                ui.info = Button("Info", ui.pageTwo.transform, font, 0.61f, 0.98f);
                ui.cancelBar = Panel("Mode Cancel", safe.transform);
                ui.cancel = Button("Cancel", ui.cancelBar.transform, font, 0.04f, 0.96f);
                ui.listWindow = Window("Turret List", safe.transform);
                Label("Title", ui.listWindow.transform, "Install Turret", font, new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), 40);
                ui.buyButtons = new Button[8]; ui.rowLabels = new Text[8];
                for (int i = 0; i < 8; i++)
                {
                    float top = 0.88f - i * 0.098f, bottom = top - 0.092f;
                    ui.rowLabels[i] = Label("Turret Row " + i, ui.listWindow.transform, "", font,
                        new Vector2(0.03f, bottom), new Vector2(0.77f, top), 26);
                    ui.rowLabels[i].alignment = TextAnchor.MiddleLeft;
                    ui.buyButtons[i] = MakeButton(i == 0 ? "Buy" : "Locked", ui.listWindow.transform, font,
                        new Vector2(0.79f, bottom), new Vector2(0.97f, top), 26);
                    ui.buyButtons[i].interactable = i == 0;
                }
                ui.listClose = MakeButton("Close", ui.listWindow.transform, font, new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.075f), 30);
                ui.infoWindow = Window("Game Info", safe.transform);
                ui.infoText = Label("Info", ui.infoWindow.transform, "", font, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.8f), 36);
                ui.infoClose = MakeButton("Close", ui.infoWindow.transform, font, new Vector2(0.2f, 0.10f), new Vector2(0.8f, 0.23f), 32);
                // Full safe-area mask blocks field input; only the existing three M2 buttons are raised above it at runtime.
                ui.upgradeWindow = Image("Upgrade Modal", safe.transform, Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.08f, 0.96f));
                Label("Upgrades Title", ui.upgradeWindow.transform, "Upgrades\nDamage, attack speed and cookie production", font,
                    new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.80f), 38);
                ui.upgradeClose = MakeButton("Close", ui.upgradeWindow.transform, font, new Vector2(0.25f, 0.22f), new Vector2(0.75f, 0.32f), 32);
                ui.saleWindow = Window("Sale Confirmation", safe.transform);
                ui.saleText = Label("Sale Details", ui.saleWindow.transform, "", font, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.85f), 38);
                ui.saleNo = MakeButton("Cancel", ui.saleWindow.transform, font, new Vector2(0.06f, 0.12f), new Vector2(0.47f, 0.29f), 32);
                ui.saleYes = MakeButton("Sell", ui.saleWindow.transform, font, new Vector2(0.53f, 0.12f), new Vector2(0.94f, 0.29f), 32);
                safe.AddComponent<PlacementHud>().Configure(placement, wallet, upgrades, upgradePanel, settings, title, ui);
                Undo.RecordObject(saves, "Connect M4 layout saving"); saves.ConfigurePlacement(placement); EditorUtility.SetDirty(saves);
                root.SetActive(true); Undo.RegisterCreatedObjectUndo(root, "Add M4 placement");
                EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = root; Undo.CollapseUndoOperations(group);
                Debug.Log("M4 added. Existing turret/UI remain intact in Edit Mode; Play replaces the fixed attack with grid-owned clones. Save/reopen the scene and test. No Player Settings or assets changed.");
            }
            catch { Undo.RevertAllDownToGroup(group); if (root != null) UnityEngine.Object.DestroyImmediate(root); throw; }
        }
        private static T One<T>(GameObject[] roots) where T : Behaviour
        {
            T[] found = roots.SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1 || !found[0].isActiveAndEnabled) throw new InvalidOperationException($"Need exactly one active {typeof(T).Name}. Install/inspect M1-M3 first.");
            return found[0];
        }
        private static GameObject Child(string name, Transform parent, params Type[] types)
        { var child = new GameObject(name, types); child.transform.SetParent(parent, false); child.layer = LayerMask.NameToLayer("UI"); return child; }
        private static void SetRect(GameObject go, Vector2 min, Vector2 max)
        { RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static GameObject Panel(string name, Transform parent)
        { GameObject panel = Child(name, parent, typeof(RectTransform)); SetRect(panel, Vector2.zero, Vector2.one); return panel; }
        private static GameObject Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        { GameObject go = Child(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); SetRect(go, min, max); go.GetComponent<Image>().color = color; return go; }
        private static GameObject Window(string name, Transform parent) => Image(name, parent, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.91f), new Color(0.04f, 0.06f, 0.08f, 1));
        private static Text Label(string name, Transform parent, string content, Font font, Vector2 min, Vector2 max, int size)
        {
            GameObject go = Child(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); SetRect(go, min, max);
            Text text = go.GetComponent<Text>(); text.font = font; text.text = content; text.fontSize = size; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = size; text.raycastTarget = false; return text;
        }
        private static Button Button(string title, Transform parent, Font font, float left, float right) =>
            MakeButton(title, parent, font, new Vector2(left, 0.015f), new Vector2(right, 0.105f), 30);
        private static Button MakeButton(string title, Transform parent, Font font, Vector2 min, Vector2 max, int size)
        {
            GameObject go = Image(title + " Button", parent, min, max, new Color(0.20f, 0.43f, 0.48f));
            Button button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            Label("Label", go.transform, title, font, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f), size); return button;
        }
    }
}
