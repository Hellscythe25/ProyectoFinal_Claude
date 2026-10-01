using System.IO;
using System.Linq;
using TTTXO.Game.Bootstrap;
using TTTXO.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TTTXO.Game.Editor
{
    /// <summary>
    /// Creates Assets/Scenes/Rps.unity: a second boot of the same app shell as Main, starting at the
    /// RPS menu instead of Home. The root GameObject "AppRoot" holds UIDocument (Root.uxml +
    /// MainPanelSettings), GameManager, ScreenRouter, AppBootstrap and the runtime SceneUiBootstrap
    /// bridge that re-applies the document references at play time (see SceneUiBootstrap for the
    /// stale-reference quirk). The router's StartScreen serialized field is switched to RpsMenu so the
    /// boot sequence lands on the RPS screens.
    ///
    /// Re-running the menu item is safe: existing assets/objects are reused and re-wired; a leftover
    /// pre-refactor "RpsRoot" (whose standalone controller was deleted) is cleaned up.
    /// </summary>
    public static class RpsSceneSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Rps.unity";
        private const string PanelSettingsPath = "Assets/UI/Settings/MainPanelSettings.asset";
        private const string RootUxmlPath = "Assets/UI/Screens/Root.uxml";
        private const string AppRootObjectName = "AppRoot";
        private const string LegacyRootObjectName = "RpsRoot";

        [MenuItem("TTTXO/Setup/Create RPS Scene")]
        public static void CreateRpsScene()
        {
            EnsureFolder(ScenesFolder);

            if (!TryLoadAssets(out VisualTreeAsset rootUxml, out PanelSettings panelSettings))
            {
                return;
            }

            var scene = OpenOrCreateScene();
            RemoveLegacyRoot();

            var appRoot = GameObject.Find(AppRootObjectName) ?? new GameObject(AppRootObjectName);

            var document = GetOrAddComponent<UIDocument>(appRoot);
            WireDocument(document, rootUxml, panelSettings);

            // Plain component that holds the two document refs reliably (UIDocument itself is
            // ExecuteAlways and can drop them before the scene saves); the runtime bridge applies
            // them on Awake. Configure writes the fields directly, not through SerializedObject -
            // the route proven to be reverted in batch mode.
            var bridge = GetOrAddComponent<SceneUiBootstrap>(appRoot);
            bridge.Configure(panelSettings, rootUxml);
            EditorUtility.SetDirty(bridge);

            GetOrAddComponent<GameManager>(appRoot);
            var router = GetOrAddComponent<ScreenRouter>(appRoot);
            // Boot straight into the RPS menu instead of the TTT home screen.
            var routerSerialized = new SerializedObject(router);
            routerSerialized.FindProperty("_startScreen").enumValueIndex = (int)ScreenId.RpsMenu;
            routerSerialized.ApplyModifiedPropertiesWithoutUndo();
            GetOrAddComponent<AppBootstrap>(appRoot);

            // A scene needs at least one camera for the game view to render.
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.043f, 0.059f, 0.078f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("RpsSceneSetup: scene created. Open Assets/Scenes/Rps.unity and press Play.");
        }

        [MenuItem("TTTXO/Setup/Fix RPS Scene")]
        public static void FixRpsScene()
        {
            if (!SceneExists())
            {
                Debug.LogError("RpsSceneSetup: Assets/Scenes/Rps.unity not found. Run 'TTTXO/Setup/Create RPS Scene' first.");
                return;
            }

            if (!TryLoadAssets(out VisualTreeAsset rootUxml, out PanelSettings panelSettings))
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool changed = false;

            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.GetComponent<UIDocument>() == null)
                {
                    continue;
                }

                var document = go.GetComponent<UIDocument>();
                if (WireDocument(document, rootUxml, panelSettings))
                {
                    changed = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log(changed
                ? "RpsSceneSetup: references fixed. Press Play."
                : "RpsSceneSetup: nothing to fix. Press Play.");
        }

        private static bool TryLoadAssets(out VisualTreeAsset rootUxml, out PanelSettings panelSettings)
        {
            rootUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RootUxmlPath);
            panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (rootUxml == null)
            {
                Debug.LogError($"RpsSceneSetup: could not load '{RootUxmlPath}'.");
                return false;
            }

            if (panelSettings == null)
            {
                Debug.LogError($"RpsSceneSetup: could not load '{PanelSettingsPath}'. Run 'TTTXO/Setup/Create Main Scene' first.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Assigns the PanelSettings and the UXML to a UIDocument. Deliberately the same direct
        /// property writes MainSceneSetup uses (which do persist): the SerializedObject rewrite was
        /// proven to be reverted in batch mode, and the runtime SceneUiBootstrap submits the true
        /// fallback if even these do not stick.
        /// </summary>
        private static bool WireDocument(UIDocument document, VisualTreeAsset rootUxml, PanelSettings panelSettings)
        {
            document.visualTreeAsset = rootUxml;
            document.panelSettings = panelSettings;
            EditorUtility.SetDirty(document);

            var check = new SerializedObject(document);
            return check.FindProperty("m_PanelSettings").objectReferenceValue != null
                   && check.FindProperty("sourceAsset").objectReferenceValue != null;
        }

        /// <summary>
        /// Pre-refactor scenes own an "RpsRoot" whose RpsStandaloneController was deleted: leaving it
        /// adds a missing-script component to the scene and it fights for screen space with AppRoot.
        /// </summary>
        private static void RemoveLegacyRoot()
        {
            var legacy = GameObject.Find(LegacyRootObjectName);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }
        }

        private static bool SceneExists()
        {
            return File.Exists(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
                   || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        }

        private static Scene OpenOrCreateScene()
        {
            if (SceneExists())
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(s => s.path == scenePath);

            if (existing != null)
            {
                existing.enabled = true;
            }
            else
            {
                // Append after the Main scene so Main keeps build index 0.
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}