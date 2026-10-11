using System;
using System.Linq;
using CookieBreaker.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CookieBreaker.Editor
{
    public static class M1CombatSetup
    {
        private const string RootName = "Cookie Breaker M1";

        [MenuItem("Cookie Breaker/M1/Add Combat to Open Main Scene")]
        public static void AddCombat()
        {
            if (Application.unityVersion != "6000.5.1f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Unity 6000.5.1f1 in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != CookieBreakerProjectSetup.MainScenePath)
                throw new InvalidOperationException("Open CookieBreakerMain and make it the active scene first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Any(root => root.name == RootName || root.GetComponentInChildren<CookieSpawner>(true) != null))
                throw new InvalidOperationException("M1 combat already exists. Inspect it instead of adding a duplicate. Undo the previous setup to recreate it.");
            Camera[] cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Where(camera => camera.isActiveAndEnabled && camera.CompareTag("MainCamera")).ToArray();
            if (cameras.Length != 1 || !cameras[0].orthographic)
                throw new InvalidOperationException("The main scene needs exactly one active orthographic MainCamera.");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) throw new InvalidOperationException("Unity's built-in UI font is unavailable.");
            Text originalTitle = roots.SelectMany(root => root.GetComponentsInChildren<Text>(true))
                .FirstOrDefault(text => text.gameObject.name == "Game Title" && text.text == "Cookie Breaker");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Cookie Breaker M1 combat");
            GameObject root = null;
            try
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                CoinWallet wallet = root.AddComponent<CoinWallet>();
                CookieSpawner spawner = root.AddComponent<CookieSpawner>();
                Transform cookies = Child("Active Cookies", root.transform).transform;
                Transform shots = Child("Active Projectiles", root.transform).transform;
                Transform templates = Child("Inactive Templates", root.transform).transform;

                GameObject cookie = Child("Cookie Template", templates);
                cookie.SetActive(false);
                cookie.transform.localScale = Vector3.one * 0.6f;
                AddGraphic(cookie, PlaceholderGraphic.Shape.Cookie, Color.white, 1);
                cookie.AddComponent<CircleCollider2D>().radius = 0.5f;
                CookieHealth cookieTemplate = cookie.AddComponent<CookieHealth>();

                GameObject shot = Child("Projectile Template", templates);
                shot.SetActive(false);
                shot.transform.localScale = Vector3.one * 0.14f;
                AddGraphic(shot, PlaceholderGraphic.Shape.Disc, new Color(1, 0.93f, 0.5f), 3);
                shot.AddComponent<CircleCollider2D>().isTrigger = true;
                Rigidbody2D body = shot.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                CookieProjectile projectileTemplate = shot.AddComponent<CookieProjectile>();
                spawner.Configure(cameras[0], wallet, cookieTemplate, cookies);

                GameObject turretObject = Child("Automatic Turret", root.transform);
                AutomaticTurret turret = turretObject.AddComponent<AutomaticTurret>();
                GameObject turretBase = Child("Base", turretObject.transform);
                turretBase.transform.localScale = Vector3.one * 0.7f;
                AddGraphic(turretBase, PlaceholderGraphic.Shape.Disc, new Color(0.25f, 0.7f, 0.8f), 2);
                Transform pivot = Child("Barrel Pivot", turretObject.transform).transform;
                GameObject barrel = Child("Barrel", pivot);
                barrel.transform.localPosition = new Vector3(0, 0.3f, 0);
                barrel.transform.localScale = new Vector3(0.18f, 0.6f, 1);
                AddGraphic(barrel, PlaceholderGraphic.Shape.Square, new Color(0.5f, 0.9f, 1), 3);
                Transform muzzle = Child("Muzzle", pivot).transform;
                muzzle.localPosition = new Vector3(0, 0.65f, 0);
                turret.Configure(cameras[0], spawner, pivot, muzzle, projectileTemplate, shots);

                GameObject canvasObject = Child("Coin Canvas", root.transform, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.layer = LayerMask.NameToLayer("UI");
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0;
                GameObject safePanel = Child("Safe Area", canvasObject.transform, typeof(RectTransform), typeof(SafeAreaPanel));
                safePanel.layer = canvasObject.layer;
                RectTransform safeRect = safePanel.GetComponent<RectTransform>();
                safeRect.anchorMin = Vector2.zero;
                safeRect.anchorMax = Vector2.one;
                safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
                GameObject label = Child("Coin Label", safePanel.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                label.layer = canvasObject.layer;
                RectTransform rect = label.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.08f, 0.92f);
                rect.anchorMax = new Vector2(0.92f, 0.98f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Text text = label.GetComponent<Text>();
                text.font = font;
                text.fontSize = 54;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = new Color(1, 0.9f, 0.65f);
                text.text = "Coins: 0";
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 24;
                text.resizeTextMaxSize = 54;
                text.raycastTarget = false;
                root.AddComponent<CoinHud>().Configure(wallet, text, originalTitle != null ? originalTitle.rectTransform : null);
                root.SetActive(true);
                Undo.RegisterCreatedObjectUndo(root, "Add M1 combat");

                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);
                Debug.Log("M1 objects added without saving or changing Player Settings. Save CookieBreakerMain, then press Play. Undo removes the added hierarchy.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void AddGraphic(GameObject target, PlaceholderGraphic.Shape shape, Color tint, int order)
        {
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.color = tint;
            renderer.sortingOrder = order;
            target.AddComponent<PlaceholderGraphic>().Configure(shape);
        }
    }
}
