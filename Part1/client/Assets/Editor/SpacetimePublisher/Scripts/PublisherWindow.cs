using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

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
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PathToUxml);
            visualTree.CloneTree(rootVisualElement);

            // Apply style via USS
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PathToUss);
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
        /// Init -> prereqs => publish => done
        private async Task OnPublishBtnClick()
        {
            setPublishStartUi();
            await ensureSpacetimeCliInstalledAsync();
            await publish();
            setPublishDoneUi(); // TODO: Pass err, if any
        }
        #endregion // User Input Interactions
        
        
        #region Utils
        private async Task publish()
        {
            
        }
        
        /// Check for install => Install if !found -> Throw if err
        private async Task ensureSpacetimeCliInstalledAsync()
        {
            // Check if Spacetime CLI is installed => install, if !found
            bool isSpacetimeCliInstalled;
            try
            {
                isSpacetimeCliInstalled = await SpacetimeCli.CheckIsSpacetimeCliInstalledAsync();
            }
            catch (Exception e)
            {
                updateStatus(StringStyle.Error, e.Message);
                throw;
            }
            
            if (!isSpacetimeCliInstalled)
            {
                // Command !found: Update status => Install now
                publishStatusLabel.text = GetStyledStr(
                    StringStyle.Action, 
                    "Installing Spacetime CLI...");

                SpacetimeCliResult installResult;
                try
                {
                    installResult = await SpacetimeCli.InstallSpacetimeCliAsync();
                }
                catch (Exception e)
                {
                    updateStatus(StringStyle.Error, e.Message);
                    throw;
                }
                
                bool hasSpacetimeCli = !installResult.HasErr;
                if (hasSpacetimeCli)
                    return;
                
                // Critical error: Spacetime CLI !installed and failed install attempt
                updateStatus(StringStyle.Error, "Failed to install Spacetime CLI: See logs");
            }
        }

        /// Show a styled friendly string to UI. Errs will:
        /// - LogError
        /// - Enable Publish btn
        private void updateStatus(StringStyle style, string friendlyStr)
        {
            publishStatusLabel.text = GetStyledStr(style, friendlyStr);

            if (style != StringStyle.Error)
                return;
            
            // Error:
            Debug.LogError($"Error: {friendlyStr}");
            publishBtn.SetEnabled(true);
        }
        
        private void setPublishStartUi()
        {
            // Set UI
            publishBtn.SetEnabled(false);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Connecting...");
            publishStatusLabel.style.display = DisplayStyle.Flex;
        }

        private void setPublishDoneUi()
        {
            publishBtn.SetEnabled(true);
            updateStatus(StringStyle.Success, "Published!");
        }
        #endregion // Utils
    }
}