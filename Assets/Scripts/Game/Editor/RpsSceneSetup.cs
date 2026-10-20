using System.Collections.Generic;
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
    /// Creates (or repairs) Assets/Scenes/Rps.unity, the game's only scene. The root GameObject
    /// "AppRoot" holds UIDocument (Root.uxml + MainPanelSettings), GameManager, ScreenRouter,
    /// AppBootstrap and the runtime SceneUiBootstrap bridge that re-applies the document references
    /// at play time (see SceneUiBootstrap for the stale-reference quirk). The router's StartScreen
    /// serialized field is set to RpsMenu so the boot sequence lands on the menu.
    ///
    /// Re-running the menu item is safe: existing assets/objects are reused and re-wired. It also
    /// removes the leftover world-space Animator/SpriteRenderer preview objects from the old art
    /// experiments, which are not used by the UI-frame animations.
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
            RemovePreviewObjects(scene);

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
            ConfigureRouter(router);
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
            bool changed = RemovePreviewObjects(scene);

            foreach (var go in scene.GetRootGameObjects())
            {
                var router = go.GetComponent<ScreenRouter>();
                if (router != null)
                {
                    ConfigureRouter(router);
                    changed = true;
                }

                var document = go.GetComponent<UIDocument>();
                if (document == null)
                {
                    continue;
                }

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
                Debug.LogError($"RpsSceneSetup: could not load '{PanelSettingsPath}'. Check the UI settings asset.");
                return false;
            }

            return true;
        }

        private static void ConfigureRouter(ScreenRouter router)
        {
            var routerSerialized = new SerializedObject(router);
            routerSerialized.FindProperty("_startScreen").enumValueIndex = (int)ScreenId.RpsMenu;
            routerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Removes the old world-space animation previews. The game renders the fighters through
        /// UI Toolkit frames, so these Animator/SpriteRenderer objects are unused duplicates.
        /// </summary>
        private static bool RemovePreviewObjects(Scene scene)
        {
            var toRemove = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = transform.gameObject;
                    if (go.name == AppRootObjectName || go.name == "Main Camera")
                    {
                        continue;
                    }

                    if (go.GetComponent<Animator>() != null || go.GetComponent<SpriteRenderer>() != null)
                    {
                        toRemove.Add(go);
                    }
                }
            }

            foreach (var go in toRemove)
            {
                Object.DestroyImmediate(go);
            }

            return toRemove.Count > 0;
        }

        /// <summary>
        /// Assigns the PanelSettings and the UXML to a UIDocument. Deliberately the same direct
        /// property writes the old scene setup used (which do persist): the SerializedObject rewrite
        /// was proven to be reverted in batch mode, and the runtime SceneUiBootstrap submits the true
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
            // Rps is the only scene in this build. Replacing the list also drops the deleted Main
            // scene reference instead of leaving a missing entry at build index 0.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
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