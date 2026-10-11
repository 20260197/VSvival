using System;
using System.Linq;
using CookieBreaker.Combat;
using CookieBreaker.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CookieBreaker.Editor
{
    public static class M2UpgradeSetup
    {
        private const string RootName = "Cookie Breaker M2";

        [MenuItem("Cookie Breaker/M2/Add Upgrades to Open Main Scene")]
        public static void AddUpgrades()
        {
            if (Application.unityVersion != "6000.5.1f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Unity 6000.5.1f1 in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != CookieBreakerProjectSetup.MainScenePath)
                throw new InvalidOperationException("Open CookieBreakerMain and make it the active scene first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Any(root => root.name == RootName || root.GetComponentInChildren<UpgradeController>(true) != null))
                throw new InvalidOperationException("M2 upgrades already exist. Inspect them instead of adding a duplicate.");
            CoinWallet wallet = RequireOne<CoinWallet>(roots);
            AutomaticTurret turret = RequireOne<AutomaticTurret>(roots);
            CookieSpawner spawner = RequireOne<CookieSpawner>(roots);

            // An additive scene must not accidentally introduce a second active EventSystem.
            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystems.Length > 1 || (eventSystems.Length == 1 &&
                (!eventSystems[0].isActiveAndEnabled || eventSystems[0].gameObject.scene != scene ||
                 eventSystems[0].GetComponents<BaseInputModule>().Length != 1 ||
                 eventSystems[0].GetComponent<InputSystemUIInputModule>() == null ||
                 !eventSystems[0].GetComponent<InputSystemUIInputModule>().isActiveAndEnabled)))
                throw new InvalidOperationException("Resolve existing EventSystems first: use one active InputSystemUIInputModule in CookieBreakerMain. Existing input objects were not changed.");
            if (eventSystems.Length == 1)
            {
                InputSystemUIInputModule existing = eventSystems[0].GetComponent<InputSystemUIInputModule>();
                if (existing.point == null || existing.point.action == null ||
                    existing.leftClick == null || existing.leftClick.action == null)
                    throw new InvalidOperationException("The existing UI input module needs Point and Left Click actions. Its configuration was not changed.");
            }
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) throw new InvalidOperationException("Unity's built-in UI font is unavailable.");
            // Reuse imported, persistent action references. Runtime-created references would
            // not reliably survive saving/reopening the scene or inclusion in an APK.
            const string inputPath = "Assets/InputSystem_Actions.inputactions";
            InputActionAsset inputAsset = null;
            InputActionReference[] actions = null;
            if (eventSystems.Length == 0)
            {
                inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
                actions = AssetDatabase.LoadAllAssetsAtPath(inputPath).OfType<InputActionReference>()
                    .Where(reference => reference.action != null && reference.action.actionMap.name == "UI").ToArray();
                if (inputAsset == null) throw new InvalidOperationException("The existing InputSystem_Actions asset must be imported first.");
                foreach (string name in new[] { "Point", "Click", "Navigate", "Submit", "Cancel", "RightClick",
                    "MiddleClick", "ScrollWheel", "TrackedDevicePosition", "TrackedDeviceOrientation" })
                    RequireAction(actions, name);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Cookie Breaker M2 upgrades");
            GameObject root = null;
            try
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                UpgradeController controller = root.AddComponent<UpgradeController>();
                controller.Configure(wallet, turret, spawner);
                GameObject canvasObject = Child("Upgrade Canvas", root.transform,
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0;
                GameObject safe = Child("Safe Area", canvasObject.transform, typeof(RectTransform), typeof(SafeAreaPanel));
                SetRect(safe.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

                Button damage = CreateButton(safe.transform, "Damage", 0.03f, 0.32f, font, out Text damageText);
                Button speed = CreateButton(safe.transform, "Attack Speed", 0.355f, 0.645f, font, out Text speedText);
                Button production = CreateButton(safe.transform, "Production", 0.68f, 0.97f, font, out Text productionText);
                safe.AddComponent<UpgradePanel>().Configure(controller, wallet, damage, damageText,
                    speed, speedText, production, productionText);

                if (eventSystems.Length == 0)
                {
                    GameObject input = Child("Upgrade EventSystem", root.transform, typeof(EventSystem), typeof(InputSystemUIInputModule));
                    InputSystemUIInputModule module = input.GetComponent<InputSystemUIInputModule>();
                    module.actionsAsset = inputAsset;
                    module.point = RequireAction(actions, "Point");
                    module.leftClick = RequireAction(actions, "Click");
                    module.move = RequireAction(actions, "Navigate");
                    module.submit = RequireAction(actions, "Submit");
                    module.cancel = RequireAction(actions, "Cancel");
                    module.rightClick = RequireAction(actions, "RightClick");
                    module.middleClick = RequireAction(actions, "MiddleClick");
                    module.scrollWheel = RequireAction(actions, "ScrollWheel");
                    module.trackedDevicePosition = RequireAction(actions, "TrackedDevicePosition");
                    module.trackedDeviceOrientation = RequireAction(actions, "TrackedDeviceOrientation");
                }
                root.SetActive(true);
                Undo.RegisterCreatedObjectUndo(root, "Add M2 upgrades");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);
                Debug.Log("M2 upgrade objects added. Save CookieBreakerMain and test Play/touch input. Existing M1 hierarchy and Player Settings were preserved. Undo removes this addition.");
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
            T[] matches = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1 || !matches[0].isActiveAndEnabled)
                throw new InvalidOperationException($"M2 needs exactly one active M1 {typeof(T).Name}. Existing objects were not changed.");
            return matches[0];
        }

        private static InputActionReference RequireAction(InputActionReference[] actions, string name)
        {
            InputActionReference reference = actions.SingleOrDefault(action => action.action.name == name);
            if (reference == null || !EditorUtility.IsPersistent(reference))
                throw new InvalidOperationException($"The existing input asset needs an imported UI/{name} action reference. No new input asset was created.");
            return reference;
        }

        private static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            child.layer = LayerMask.NameToLayer("UI");
            return child;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(Transform parent, string title, float left, float right, Font font, out Text label)
        {
            GameObject target = Child(title + " Upgrade", parent,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            SetRect(target.GetComponent<RectTransform>(), new Vector2(left, 0.015f), new Vector2(right, 0.105f));
            Image image = target.GetComponent<Image>();
            image.color = Color.white;
            Button button = target.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.42f, 0.48f);
            colors.highlightedColor = new Color(0.25f, 0.55f, 0.6f);
            colors.pressedColor = new Color(0.12f, 0.3f, 0.35f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.23f, 0.23f, 0.23f);
            button.colors = colors;
            button.interactable = false;
            GameObject textObject = Child("Label", target.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            SetRect(textObject.GetComponent<RectTransform>(), new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
            label = textObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 32;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = title + "\nLv 0\nPress Play";
            return button;
        }
    }
}
