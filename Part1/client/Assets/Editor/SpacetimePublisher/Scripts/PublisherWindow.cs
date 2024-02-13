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
        private Button setDirectoryBtn;
        private Label serverModulePathTxt;

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
            revealUi();
        }

        /// There are some fields that persist, such as dir path
        /// We'll start revealing UI depending on what we have
        private void revealUi()
        {
            resetUi();
            // TODO: Check for ServerModulePathTxt null/empty.
            // TODO: If !empty, show publish + namebtn
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
            setDirectoryBtn = rootVisualElement.Q<Button>("SetDirectoryBtn");
            serverModulePathTxt = rootVisualElement.Q<Label>("ServerModulePathTxt");

            publishStatusLabel = rootVisualElement.Q<Label>("PublishStatusLabel");
            publishBtn = rootVisualElement.Q<Button>("PublishBtn");
        }

        /// Changing implicit names can easily cause unexpected nulls
        private void sanityCheckUiElements()
        {
            Assert.IsNotNull(setDirectoryBtn, $"Expected `{nameof(setDirectoryBtn)}`");
            Assert.IsNotNull(serverModulePathTxt, $"Expected `{nameof(serverModulePathTxt)}`");
            
            Assert.IsNotNull(publishStatusLabel, $"Expected `{nameof(publishStatusLabel)}`");
            Assert.IsNotNull(publishBtn, $"Expected `{nameof(publishBtn)}`");
        }

        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            publishBtn.clicked += () => 
                _ = OnPublishBtnClick();
            
            setDirectoryBtn.clicked += () =>
                _ = OnSetDirectoryBtnClick();
        }

        /// Resets the UI as if there was no persistence/input
        /// All you should see is the "Set Directory" section
        private void resetUi()
        {
            // Publish
            publishBtn.style.display = DisplayStyle.None;
            publishStatusLabel.style.display = DisplayStyle.None;
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
        
        private async Task OnSetDirectoryBtnClick()
        {
            // Show folder dialog
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Server Module Dir", 
                Application.dataPath, 
                "");
            
            // Cancelled?
            if (string.IsNullOrEmpty(selectedPath))
                return;
            
            // Set+Show path label, hide set dir btn
            serverModulePathTxt.text = selectedPath;
            serverModulePathTxt.style.display = DisplayStyle.Flex;
            setDirectoryBtn.style.display = DisplayStyle.None;

            // TODO: Show name btn, prefill with suggestion from project-name-with-dashes
            
            // Show btn
            publishBtn.style.display = DisplayStyle.Flex;
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
        /// - Throw an Exception
        /// - Enable Publish btn
        private void updateStatus(StringStyle style, string friendlyStr)
        {
            publishStatusLabel.text = GetStyledStr(style, friendlyStr);

            if (style != StringStyle.Error)
                return;
            
            // Error:
            publishBtn.SetEnabled(true);
            throw new Exception($"Error: {friendlyStr}");
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