using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CookieBreaker.Editor
{
    public static class CookieBreakerProjectSetup
    {
        internal const string MainScenePath = "Assets/Scenes/CookieBreakerMain.unity";

        [MenuItem("Cookie Breaker/Create Test Main Scene")]
        public static void CreateMainScene()
        {
            RequireProjectVersion();
            string absolutePath = Path.Combine(ProjectRoot, MainScenePath);
            if (File.Exists(absolutePath) || File.Exists(absolutePath + ".meta"))
                throw new System.InvalidOperationException("The main scene or its metadata already exists. Open and inspect it; this tool will not overwrite it.");

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                throw new System.InvalidOperationException("The existing Assets/Scenes folder is missing. Restore it before creating the test scene.");

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool saved = false;
            try
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 0, -10);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.065f, 0.035f, 1);
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

                var lightObject = new GameObject("Global Light 2D", typeof(Light2D));
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                lightObject.GetComponent<Light2D>().lightType = Light2D.LightType.Global;

                var canvasObject = new GameObject("Title Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvasObject.layer = LayerMask.NameToLayer("UI");
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0;

                var titleObject = new GameObject("Game Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                SceneManager.MoveGameObjectToScene(titleObject, scene);
                titleObject.layer = canvasObject.layer;
                titleObject.transform.SetParent(canvasObject.transform, false);
                RectTransform rect = titleObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.12f, 0.45f);
                rect.anchorMax = new Vector2(0.88f, 0.55f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Text title = titleObject.GetComponent<Text>();
                title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (title.font == null)
                    throw new System.InvalidOperationException("Unity's built-in title font could not be loaded.");
                title.text = "Cookie Breaker";
                title.color = new Color(1, 0.87f, 0.64f, 1);
                title.fontStyle = FontStyle.Bold;
                title.fontSize = 92;
                title.alignment = TextAnchor.MiddleCenter;
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 24;
                title.resizeTextMaxSize = 92;
                title.raycastTarget = false;

                if (!EditorSceneManager.SaveScene(scene, MainScenePath))
                    throw new System.InvalidOperationException("Unity could not save the test main scene.");

                saved = true;
                SceneManager.SetActiveScene(scene);
                Selection.activeGameObject = titleObject;
                Debug.Log("Test main scene saved by Unity. Inspect Game View, then use Cookie Breaker > Register Main Scene for Builds.");
            }
            finally
            {
                if (!saved)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previousActiveScene.IsValid())
                        SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        [MenuItem("Cookie Breaker/Register Main Scene for Builds")]
        public static void RegisterMainScene()
        {
            RequireProjectVersion();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
                throw new System.InvalidOperationException("Create and inspect the test main scene before registering it.");

            // Preserve the enabled states and ordering of all other existing scenes.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(entry => entry.path != MainScenePath))
                .ToArray();
            Debug.Log("Main scene registered first in the shared build scene list. Also inspect any active Build Profile scene overrides.");
        }

        internal static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static void RequireProjectVersion()
        {
            if (Application.unityVersion != "6000.5.1f1")
                throw new System.InvalidOperationException("Open this project with Unity 6000.5.1f1.");
        }
    }
}
