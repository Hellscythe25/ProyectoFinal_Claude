using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TTTXO.Game.Bootstrap
{
    /// <summary>
    /// Runtime bridge for the RPS scene's <see cref="UIDocument"/>. UIDocument is
    /// <c>[ExecuteAlways]</c> and its panelSettings setter re-enters panel creation and
    /// <c>OnValidate</c>, which can drop the assignment before the scene is saved (the scene YAML
    /// ends up with <c>m_PanelSettings: {{fileID: 0}}</c>). That happened even in batch mode during
    /// scene setup, so the RPS scene must not trust its serialized UIDocument references:
    ///
    /// - At runtime the document is (re)wired here in <c>Awake</c> from the serialized refs below
    ///   (first choice, survives in builds) or, failing that, straight from the asset paths in the
    ///   Editor (last-resort heal, see <see cref="Editor.RpsSceneSetup"/> for the paths).
    /// - The plain MonoBehaviour serializes its two object refs normally (no ExecuteAlways quirk),
    ///   so they stick in the scene YAML even where the UIDocument's own do not.
    ///
    /// In an Editor session this always converges to a working document regardless of what the
    /// setup tool managed to persist. See Docs/ERRORES_Conocidos.md (RPS scene PanelSettings).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SceneUiBootstrap : MonoBehaviour
    {
        private const string PanelSettingsPath = "Assets/UI/Settings/MainPanelSettings.asset";
        private const string RootUxmlPath = "Assets/UI/Screens/Root.uxml";

        [SerializeField]
        private PanelSettings panelSettings;

        [SerializeField]
        private VisualTreeAsset visualTreeAsset;

        /// <summary>
        /// Direct assignment from the scene setup tool. Public so the Setup writes the plain Unity
        /// reference fields exactly like MainSceneSetup writes the UIDocument properties - the
        /// SerializedObject route was proven to be reverted by the editor, this one persists.
        /// </summary>
        public void Configure(PanelSettings settings, VisualTreeAsset tree)
        {
            panelSettings = settings;
            visualTreeAsset = tree;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var document = GetComponent<UIDocument>();

            if (document.panelSettings == null)
            {
                document.panelSettings = ResolvePanelSettings();
            }

            if (document.visualTreeAsset == null)
            {
                document.visualTreeAsset = ResolveVisualTreeAsset();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            ReconcileEditMode();
        }

        /// <summary>
        /// Edit-time reconciliation: the batch scene setup could not persist the UIDocument's
        /// <c>m_PanelSettings</c> (the ExecuteAlways quirk), so the Inspector shows "Panel Settings:
        /// None" until the bridge re-applies it. This assigns the document from the bridge's own
        /// serialized refs whenever they are set and the document's are not, and marks everything
        /// dirty so a plain Ctrl+S in the Editor persists the references into the scene YAML.
        /// Idempotent by construction - assignments only happen while a ref is null, so running it
        /// or reloading the scene repeatedly cannot loop or corrupt anything.
        /// </summary>
        private void ReconcileEditMode()
        {
            if (panelSettings == null || visualTreeAsset == null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            if (document == null)
            {
                return;
            }

            bool changed = false;

            if (document.panelSettings == null)
            {
                document.panelSettings = panelSettings;
                changed = true;
            }

            if (document.visualTreeAsset == null)
            {
                document.visualTreeAsset = visualTreeAsset;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(document);
                EditorUtility.SetDirty(this);
            }
        }
#endif

        private PanelSettings ResolvePanelSettings()
        {
            if (panelSettings != null)
            {
                return panelSettings;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
#else
            return null;
#endif
        }

        private VisualTreeAsset ResolveVisualTreeAsset()
        {
            if (visualTreeAsset != null)
            {
                return visualTreeAsset;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RootUxmlPath);
#else
            return null;
#endif
        }
    }
}