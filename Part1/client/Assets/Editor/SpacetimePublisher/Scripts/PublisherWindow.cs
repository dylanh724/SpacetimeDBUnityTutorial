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

        private Foldout identityFoldout;
        private DropdownField identitySelectedDropdown;
        private TextField identityNicknameTxt;
        private TextField identityEmailTxt;

        private DropdownField publishDropdown;
        private GroupBox publishPathGroupBox;
        private Button publishPathSetDirectoryBtn; // "Browse"
        private TextField publishModulePathTxt;
        
        private TextField publishModuleNameTxt; // Always has a val (fallback system)

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

        /// All VisualElement field names should match their #identity in camelCase
        private void setUiElements()
        {
            topBannerBtn = rootVisualElement.Q<Button>(nameof(topBannerBtn));
            
            identityFoldout = rootVisualElement.Q<Foldout>(nameof(identityFoldout));
            identitySelectedDropdown = rootVisualElement.Q<DropdownField>(nameof(identitySelectedDropdown));
            identityNicknameTxt = rootVisualElement.Q<TextField>(nameof(identityNicknameTxt));
            identityEmailTxt = rootVisualElement.Q<TextField>(nameof(identityEmailTxt));
            
            publishDropdown = rootVisualElement.Q<DropdownField>(nameof(publishDropdown));
            publishModuleNameTxt = rootVisualElement.Q<TextField>(nameof(publishModuleNameTxt));
            publishPathGroupBox = rootVisualElement.Q<GroupBox>(nameof(publishPathGroupBox));
            publishPathSetDirectoryBtn = rootVisualElement.Q<Button>(nameof(publishPathSetDirectoryBtn));
            publishModulePathTxt = rootVisualElement.Q<TextField>(nameof(publishModulePathTxt));

            publishGroupBox = rootVisualElement.Q<GroupBox>(nameof(publishGroupBox));
            installProgressBar = rootVisualElement.Q<ProgressBar>(nameof(installProgressBar));
            publishStatusLabel = rootVisualElement.Q<Label>(nameof(publishStatusLabel));
            publishBtn = rootVisualElement.Q<Button>(nameof(publishBtn));
            
            publishResultFoldout = rootVisualElement.Q<Foldout>(nameof(publishResultFoldout));
            publishResultHostTxt = rootVisualElement.Q<TextField>(nameof(publishResultHostTxt));
            publishResultDbAddressTxt = rootVisualElement.Q<TextField>(nameof(publishResultDbAddressTxt));
            publishResultIsOptimizedBuildToggle = rootVisualElement.Q<Toggle>(nameof(publishResultIsOptimizedBuildToggle));
            installWasmOptBtn = rootVisualElement.Q<Button>(nameof(installWasmOptBtn));
        }

        /// Changing implicit names can easily cause unexpected nulls
        /// All VisualElement field names should match their #identity in camelCase
        private void sanityCheckUiElements()
        {
            Assert.IsNotNull(topBannerBtn, $"Expected `#{nameof(topBannerBtn)}`");
            
            Assert.IsNotNull(identityFoldout, $"Expected `#{nameof(identityFoldout)}`");
            Assert.IsNotNull(identitySelectedDropdown, $"Expected `#{nameof(identitySelectedDropdown)}`");
            Assert.IsNotNull(identityNicknameTxt, $"Expected `#{nameof(identityNicknameTxt)}`");
            Assert.IsNotNull(identityEmailTxt, $"Expected `#{nameof(identityEmailTxt)}`");
            
            Assert.IsNotNull(publishPathGroupBox, $"Expected `#{nameof(publishPathGroupBox)}`");
            Assert.IsNotNull(publishPathSetDirectoryBtn, $"Expected `#{nameof(publishPathSetDirectoryBtn)}`");
            Assert.IsNotNull(publishModulePathTxt, $"Expected `#{nameof(publishModulePathTxt)}`");
            
            Assert.IsNotNull(publishModuleNameTxt, $"Expected `#{nameof(publishModuleNameTxt)}`");
            
            Assert.IsNotNull(publishGroupBox, $"Expected `#{nameof(publishGroupBox)}`");
            Assert.IsNotNull(installProgressBar, $"Expected `#{nameof(installProgressBar)}`");
            Assert.IsNotNull(publishStatusLabel, $"Expected `#{nameof(publishStatusLabel)}`");
            Assert.IsNotNull(publishBtn, $"Expected `#{nameof(publishBtn)}`");
            
            Assert.IsNotNull(publishResultFoldout, $"Expected `#{nameof(publishResultFoldout)}`");
            Assert.IsNotNull(publishResultHostTxt, $"Expected `#{nameof(publishResultHostTxt)}`");
            Assert.IsNotNull(publishResultDbAddressTxt, $"Expected `#{nameof(publishResultDbAddressTxt)}`");
            Assert.IsNotNull(publishResultIsOptimizedBuildToggle, $"Expected `#{nameof(publishResultIsOptimizedBuildToggle)}`");
            Assert.IsNotNull(installWasmOptBtn, $"Expected `#{nameof(installWasmOptBtn)}`");
        }
        #endregion // Init
    }
}