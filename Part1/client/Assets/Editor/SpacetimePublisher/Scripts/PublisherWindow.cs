using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace SpacetimeDB.Editor
{
    /// Binds style and click events to the Spacetime Publisher Window
    public class PublisherWindow : EditorWindow
    {
        private Label publishStatusLabel;
        private Button publishBtn;


        #region Init
        /// Show the publisher window via top Menu item
        [MenuItem("Window/Spacetime/Publisher #&p")] // (SHIFT+ALT+P)
        public static void ShowPublisherWindow()
        {
            PublisherWindow window = GetWindow<PublisherWindow>();
            window.titleContent = new GUIContent("Publisher");
        }

        /// Add style to the UI window; subscribe to click actions
        public void CreateGUI()
        {
            initVisualTreeStyles();
            setUiElements();
            sanityCheckUiElements();
            setOnActionEvents();
        }

        private void initVisualTreeStyles()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PublisherMeta.PathToUxml);
            visualTree.CloneTree(rootVisualElement);

            // Apply style via USS
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PublisherMeta.PathToUss);
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void setUiElements()
        {
            // Set
            publishStatusLabel = rootVisualElement.Q<Label>("PublishStatusLabel");
            publishBtn = rootVisualElement.Q<Button>("PublishBtn");
        }

        /// Changing implicit names can easily cause unexpected nulls
        private void sanityCheckUiElements()
        {
            // Sanity check - 
            Assert.IsNotNull(publishStatusLabel, $"Expected `{nameof(publishStatusLabel)}`");
            Assert.IsNotNull(publishBtn, $"Expected `{nameof(publishBtn)}`");
        }

        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            publishBtn.clicked += () => 
                _ = OnPublishBtnClick();
        }
        #endregion // Init


        #region User Input Interactions
        private async Task OnPublishBtnClick()
        {
            bool isSpacetimeCliInstalled = await SpacetimeCli.CheckIsSpacetimeCliInstalled();
            if (!isSpacetimeCliInstalled)
            {
                publishStatusLabel.text = "Installing Spacetime CLI...";
                SpacetimeCliResult installResult = await SpacetimeCli.InstallSpacetimeCli();
                throw new NotImplementedException("TODO: UI Result handling");
            }
        }
        #endregion // User Input Interactions
        }
}