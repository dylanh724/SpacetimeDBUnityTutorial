using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// Binds style and click events to the Spacetime Publisher Window
    public partial class PublisherWindow : EditorWindow
    {
        /// <summary>
        /// Since we have FocusOut events, this will sometimes trigger
        /// awkwardly if you jump from input to a file picker button
        /// </summary>
        private bool _isFilePicking;

        #region UI Visual Elements
        private Button topBannerBtn;
        
        private GroupBox setupGroupBox;
        private Button setDirectoryBtn; // "Browse"
        private TextField serverModulePathTxt;
        
        private GroupBox nameGroupBox;
        private TextField nameTxt; // Always has a val (fallback system)

        private GroupBox publishGroupBox;
        private ProgressBar installProgressBar;
        private Label publishStatusLabel;
        private Button publishBtn;
        
        private Foldout publishResultFoldout;
        private TextField publishResultHostTxt; // readonly
        private TextField publishResultDbAddressTxt; // readonly
        private Toggle publishResultIsOptimizedBuildToggle; // Set readonly via hacky workaround (SetEnabled @ ResetUi)
        private Button installWasmOptBtn; // Only shows after a publish where wasm-opt was !found
        #endregion // UI Visual Elements
        
        
        #region Init
        /// Show the publisher window via top Menu item
        [MenuItem("Window/SpacetimeDB/Publisher #&p")] // (SHIFT+ALT+P)
        public static void ShowPublisherWindow()
        {
            PublisherWindow window = GetWindow<PublisherWindow>();
            window.titleContent = new GUIContent("Publisher");
        }

        /// Add style to the UI window; subscribe to click actions.
        /// High-level event chain handler.
        /// (!) Persistent vals will NOT load immediately here; await them at setOnActionEvents
        public void CreateGUI()
        {
            // Init styles, bind fields to ui, sub to events
            initVisualTreeStyles();
            setUiElements();
            sanityCheckUiElements();

            // Fields set from here
            resetUi();
            setOnActionEvents(); // @ PublisherWindowCallbacks.cs
        }

        private void initVisualTreeStyles()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PathToUxml);
            visualTree.CloneTree(rootVisualElement);

            // Apply style via USS
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PathToUss);
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void setUiElements()
        {
            topBannerBtn = rootVisualElement.Q<Button>("TopBannerBtn");
            
            setupGroupBox = rootVisualElement.Q<GroupBox>("PathGroupBox");
            setDirectoryBtn = rootVisualElement.Q<Button>("PathSetDirectoryBtn");
            serverModulePathTxt = rootVisualElement.Q<TextField>("PathTxt");
            
            nameGroupBox = rootVisualElement.Q<GroupBox>("NameGroupBox");
            nameTxt = rootVisualElement.Q<TextField>("NameTxt");

            publishGroupBox = rootVisualElement.Q<GroupBox>("PublishGroupBox");
            installProgressBar = rootVisualElement.Q<ProgressBar>("InstallProgressBar");
            publishStatusLabel = rootVisualElement.Q<Label>("PublishStatusLabel");
            publishBtn = rootVisualElement.Q<Button>("PublishBtn");
            
            publishResultFoldout = rootVisualElement.Q<Foldout>("PublishResultFoldout");
            publishResultHostTxt = rootVisualElement.Q<TextField>("PublishResultHostTxt");
            publishResultDbAddressTxt = rootVisualElement.Q<TextField>("PublishResultDbAddressTxt");
            publishResultIsOptimizedBuildToggle = rootVisualElement.Q<Toggle>("PublishResultIsOptimizedBuildToggle");
            installWasmOptBtn = rootVisualElement.Q<Button>("InstallWasmOptBtn");
        }

        /// Changing implicit names can easily cause unexpected nulls
        private void sanityCheckUiElements()
        {
            Assert.IsNotNull(topBannerBtn, $"Expected `{nameof(topBannerBtn)}`");
            
            Assert.IsNotNull(setupGroupBox, $"Expected `{nameof(setupGroupBox)}`");
            Assert.IsNotNull(setDirectoryBtn, $"Expected `{nameof(setDirectoryBtn)}`");
            Assert.IsNotNull(serverModulePathTxt, $"Expected `{nameof(serverModulePathTxt)}`");
            
            Assert.IsNotNull(nameGroupBox, $"Expected `{nameof(nameGroupBox)}`");
            Assert.IsNotNull(nameTxt, $"Expected `{nameof(nameTxt)}`");
            
            Assert.IsNotNull(publishGroupBox, $"Expected `{nameof(publishGroupBox)}`");
            Assert.IsNotNull(installProgressBar, $"Expected `{nameof(installProgressBar)}`");
            Assert.IsNotNull(publishStatusLabel, $"Expected `{nameof(publishStatusLabel)}`");
            Assert.IsNotNull(publishBtn, $"Expected `{nameof(publishBtn)}`");
            
            Assert.IsNotNull(publishResultFoldout, $"Expected `{nameof(publishResultFoldout)}`");
            Assert.IsNotNull(publishResultHostTxt, $"Expected `{nameof(publishResultHostTxt)}`");
            Assert.IsNotNull(publishResultDbAddressTxt, $"Expected `{nameof(publishResultDbAddressTxt)}`");
            Assert.IsNotNull(publishResultIsOptimizedBuildToggle, $"Expected `{nameof(publishResultIsOptimizedBuildToggle)}`");
            Assert.IsNotNull(installWasmOptBtn, $"Expected `{nameof(installWasmOptBtn)}`");
        }
        #endregion // Init
    }
}