using System;
using System.Linq;
using CookieBreaker.Combat;
using CookieBreaker.Persistence;
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
    public static class M3AutoSaveSetup
    {
        private const string RootName = "Cookie Breaker M3";

        [MenuItem("Cookie Breaker/M3/Add Auto Save and Settings to Open Main Scene")]
        public static void AddAutoSave()
        {
            if (Application.unityVersion != "6000.5.1f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Unity 6000.5.1f1 in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != CookieBreakerProjectSetup.MainScenePath)
                throw new InvalidOperationException("Open CookieBreakerMain and make it active first.");
            GameObject[] roots = scene.GetRootGameObjects();
            AutoSaveService[] existing = UnityEngine.Object.FindObjectsByType<AutoSaveService>(FindObjectsInactive.Include);
            GameObject rootNamed = roots.FirstOrDefault(root => root.name == RootName);
            if (existing.Length == 1 && rootNamed != null && existing[0].gameObject == rootNamed &&
                rootNamed.GetComponentInChildren<SettingsPanel>(true) != null)
            {
                Selection.activeGameObject = rootNamed;
                Debug.Log("M3 is already installed. Existing hierarchy selected; no duplicate or overwrite performed.");
                return;
            }
            if (existing.Length != 0 || rootNamed != null)
                throw new InvalidOperationException("Inspect the existing M3/save objects first. No existing objects were changed.");
            CoinWallet wallet = RequireOne<CoinWallet>(roots);
            UpgradeController upgrades = RequireOne<UpgradeController>(roots);
            EventSystem[] inputs = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (inputs.Length != 1 || inputs[0].gameObject.scene != scene || !inputs[0].isActiveAndEnabled ||
                inputs[0].GetComponents<BaseInputModule>().Length != 1 ||
                inputs[0].GetComponent<InputSystemUIInputModule>() == null ||
                !inputs[0].GetComponent<InputSystemUIInputModule>().isActiveAndEnabled)
                throw new InvalidOperationException("Keep the working M2 EventSystem with one InputSystemUIInputModule. Run the M2 setup first if it is missing.");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) throw new InvalidOperationException("Built-in UI font unavailable.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Cookie Breaker M3 auto save and settings");
            GameObject root = null;
            try
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                AutoSaveService saves = root.AddComponent<AutoSaveService>();
                saves.Configure(wallet, upgrades);
                GameObject canvasObject = Child("Settings Canvas", root.transform,
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 40;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0;
                GameObject safe = Child("Safe Area", canvasObject.transform, typeof(RectTransform), typeof(SafeAreaPanel));
                Rect(safe, Vector2.zero, Vector2.one);
                Button open = Button(safe.transform, "Settings", font, new Vector2(0.80f, 0.92f), new Vector2(0.98f, 0.98f));

                GameObject settings = Window("Settings Window", safe.transform);
                Label(settings.transform, "Settings", font, new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.84f), 50);
                Text status = Label(settings.transform, "Automatic saving enabled", font,
                    new Vector2(0.08f, 0.53f), new Vector2(0.92f, 0.70f), 32);
                Button reset = Button(settings.transform, "Reset Game", font, new Vector2(0.2f, 0.40f), new Vector2(0.8f, 0.49f));
                Button close = Button(settings.transform, "Close", font, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.34f));

                GameObject confirmation = Window("Reset Confirmation", safe.transform);
                Label(confirmation.transform, "Reset all progress?\nCoins and upgrades will be lost.", font,
                    new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.78f), 42);
                Button cancel = Button(confirmation.transform, "Cancel", font, new Vector2(0.1f, 0.37f), new Vector2(0.47f, 0.48f));
                Button confirm = Button(confirmation.transform, "Reset", font, new Vector2(0.53f, 0.37f), new Vector2(0.9f, 0.48f));
                confirmation.SetActive(false);
                settings.SetActive(false);
                safe.AddComponent<SettingsPanel>().Configure(saves, settings, confirmation, open, close, reset, cancel, confirm, status);
                root.SetActive(true);
                Undo.RegisterCreatedObjectUndo(root, "Add M3 auto save and settings");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);
                Debug.Log("M3 added without saving the scene or modifying M1/M2/settings. Save and reopen CookieBreakerMain, then test Play. Re-running this menu creates no duplicates.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static T RequireOne<T>(GameObject[] roots) where T : Behaviour
        {
            T[] found = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1 || !found[0].isActiveAndEnabled)
                throw new InvalidOperationException($"Exactly one active {typeof(T).Name} is needed. Install/inspect M1 and M2 first.");
            return found[0];
        }

        private static GameObject Child(string name, Transform parent, params Type[] types)
        {
            var child = new GameObject(name, types);
            child.transform.SetParent(parent, false);
            child.layer = LayerMask.NameToLayer("UI");
            return child;
        }
        private static void Rect(GameObject target, Vector2 min, Vector2 max)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static GameObject Window(string name, Transform parent)
        {
            GameObject result = Child(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Rect(result, Vector2.zero, Vector2.one);
            result.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.08f, 0.96f);
            return result;
        }
        private static Text Label(Transform parent, string content, Font font, Vector2 min, Vector2 max, int size)
        {
            GameObject result = Child(content.Split('\n')[0], parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Rect(result, min, max);
            Text label = result.GetComponent<Text>();
            label.font = font; label.text = content; label.fontSize = size; label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 22; label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }
        private static Button Button(Transform parent, string title, Font font, Vector2 min, Vector2 max)
        {
            GameObject result = Child(title + " Button", parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Rect(result, min, max);
            Image image = result.GetComponent<Image>();
            image.color = new Color(0.22f, 0.43f, 0.48f);
            Button button = result.GetComponent<Button>();
            button.targetGraphic = image;
            Label(result.transform, title, font, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), 36);
            return button;
        }
    }
}
