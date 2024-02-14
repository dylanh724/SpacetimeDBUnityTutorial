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
        private GroupBox setupGroupBox;
        private Button setDirectoryBtn; // "Browse"
        private TextField serverModulePathTxt;
        
        private GroupBox nameGroupBox;
        private TextField nameTxt;

        private GroupBox publishGroupBox;
        private ProgressBar installProgressBar;
        private Label publishStatusLabel;
        private Button publishBtn;
        
        private Action publishBtnClickAction;

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
        public void CreateGUI()
        {
            // Init styles, bind fields to ui, sub to events
            initVisualTreeStyles();
            setUiElements();
            sanityCheckUiElements();

            // Fields set from here
            resetUi();
            setDynamicUi();
            setOnActionEvents();
        }

        /// Dynamically sets a dashified-project-name placeholder, if empty
        private void suggestModuleNameIfEmpty()
        {
            // Set the server module name placeholder text dynamically, based on the project name
            // Replace non-alphanumeric chars with dashes
            bool hasName = !string.IsNullOrEmpty(nameTxt.value);
            if (hasName)
                return; // Keep whatever the user customized
            
            // Prefix "unity-", dashify the name, replace "client" with "server (if found).
            string unityProjectName = $"unity-{Application.productName.ToLowerInvariant()}";
            string projectNameDashed = System.Text.RegularExpressions.Regex
                .Replace(unityProjectName, @"[^a-z0-9]", "-")
                .Replace("client", "server");
            
            nameTxt.value = projectNameDashed;
        }

        /// Dynamically set/reveal UI based on persistence
        private void setDynamicUi()
        {
            suggestModuleNameIfEmpty();
            
            // ServerModulePathTxt persists: If previously entered, show the publish group
            bool hasPathSet = !string.IsNullOrEmpty(serverModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync();
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
            setupGroupBox = rootVisualElement.Q<GroupBox>("PathGroupBox");
            setDirectoryBtn = rootVisualElement.Q<Button>("PathSetDirectoryBtn");
            serverModulePathTxt = rootVisualElement.Q<TextField>("PathTxt");
            
            nameGroupBox = rootVisualElement.Q<GroupBox>("NameGroupBox");
            nameTxt = rootVisualElement.Q<TextField>("NameTxt");

            publishGroupBox = rootVisualElement.Q<GroupBox>("PublishGroupBox");
            installProgressBar = rootVisualElement.Q<ProgressBar>("InstallProgressBar");
            publishStatusLabel = rootVisualElement.Q<Label>("PublishStatusLabel");
            publishBtn = rootVisualElement.Q<Button>("PublishBtn");
        }

        /// Changing implicit names can easily cause unexpected nulls
        private void sanityCheckUiElements()
        {
            Assert.IsNotNull(setupGroupBox, $"Expected `{nameof(setupGroupBox)}`");
            Assert.IsNotNull(setDirectoryBtn, $"Expected `{nameof(setDirectoryBtn)}`");
            Assert.IsNotNull(serverModulePathTxt, $"Expected `{nameof(serverModulePathTxt)}`");
            
            Assert.IsNotNull(nameGroupBox, $"Expected `{nameof(nameGroupBox)}`");
            Assert.IsNotNull(nameTxt, $"Expected `{nameof(nameTxt)}`");
            
            Assert.IsNotNull(publishGroupBox, $"Expected `{nameof(publishGroupBox)}`");
            Assert.IsNotNull(installProgressBar, $"Expected `{nameof(installProgressBar)}`");
            Assert.IsNotNull(publishStatusLabel, $"Expected `{nameof(publishStatusLabel)}`");
            Assert.IsNotNull(publishBtn, $"Expected `{nameof(publishBtn)}`");
        }

        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            serverModulePathTxt.RegisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            setDirectoryBtn.clicked += onSetDirectoryBtnClick;
            nameTxt.RegisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked += onPublishBtnClickAsync;
        }
        
        /// Curried to an async Task, wrapped this way so
        /// we can unsubscribe and for better err handling 
        private async void onPublishBtnClickAsync()
        {
            try
            {
                await startPublishChain();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }
        #endregion // Init
        
        
        #region Direct UI Interaction Callbacks
        /// Toggle next section if !null
        private void onServerModulePathTxtFocusOut(FocusOutEvent evt)
        {
            bool hasPathSet = !string.IsNullOrEmpty(serverModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync();
            else
                publishGroupBox.style.display = DisplayStyle.None;
        }
        
        /// Explicitly declared and curried so we can unsubscribe
        private void onNameTxtFocusOut(FocusOutEvent evt) =>
            suggestModuleNameIfEmpty();
        
        /// Show folder dialog -> Set path label
        private void onSetDirectoryBtnClick()
        {
            // Show folder dialog
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Server Module Dir", 
                Application.dataPath, 
                "");
            
            // Cancelled?
            if (string.IsNullOrEmpty(selectedPath))
                return;
            
            // Set path label to selected path to reflect UI
            serverModulePathTxt.value = selectedPath;

            // Reveal the next group
            revealPublisherGroupUiAsync();
        }
        #endregion // Direct UI Interaction Callbacks
        
        
        #region Action Utils    
        /// - Set to the initial state as if no inputs were set.
        /// - This exists so we can show all ui elements simultaneously in the
        ///   ui builder for convenience.
        private void resetUi()
        {
            publishGroupBox.style.display = DisplayStyle.None;
            installProgressBar.style.display = DisplayStyle.None;
            publishStatusLabel.style.display = DisplayStyle.None;
        }
        
        /// This will reveal the group and initially check for the spacetime cli tool
        private async void revealPublisherGroupUiAsync()
        {
            // Show and enable group, but disable the publish btn
            // to check/install Spacetime CLI tool
            publishGroupBox.SetEnabled(true);
            publishBtn.SetEnabled(false);
            publishStatusLabel.style.display = DisplayStyle.Flex;
            publishGroupBox.style.display = DisplayStyle.Flex;
            
            // Check/install for spacetime cli tool async =>
            try
            {
                await ensureSpacetimeCliInstalledAsync();
                publishStatusLabel.text = GetStyledStr(
                    StringStyle.Success, 
                    "Ready");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
            finally
            {
                // Reenable, no matter what, so we can try again if we want to
                publishGroupBox.SetEnabled(true);
            }
        }
                                    
        /// Init -> prereqs => publish => done
        /// High-level event chain handler.
        private async Task startPublishChain()
        {
            setPublishStartUi();

            try
            {
                await ensureSpacetimeCliInstalledAsync();
                await publish();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }

            setPublishDoneUi(); // TODO: Pass err, if any
        }
        
        private async Task publish()
        {
            // TODO            
        }

        /// Show progress bar, clamped to 5~100, updating every 1s
        /// Stops when reached 100, or if style display is hidden
        private async Task startProgressBarAsync(
            string title = "Running CLI...",
            int initVal = 5, 
            int valIncreasePerSec = 5,
            bool autoHideOnComplete = true)
        {
            installProgressBar.title = title;
            installProgressBar.value = Mathf.Clamp(initVal, 5, 100);
            installProgressBar.style.display = DisplayStyle.Flex;
            
            while (installProgressBar.value < 100 && 
                   installProgressBar.style.display == DisplayStyle.Flex)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                installProgressBar.value += valIncreasePerSec;
            }
            
            if (autoHideOnComplete)
                installProgressBar.style.display = DisplayStyle.None;
        }
        
        /// Check for install => Install if !found -> Throw if err
        private async Task ensureSpacetimeCliInstalledAsync()
        {
            _ = startProgressBarAsync(title: "Checking...", autoHideOnComplete: false);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Checking for Spacetime CLI tool");
            
            // Check if Spacetime CLI is installed => install, if !found
            bool isSpacetimeCliInstalled;

            try
            {
                isSpacetimeCliInstalled = await SpacetimeCli.CheckIsSpacetimeCliInstalledAsync();
            }
            catch (Exception e)
            {
                updateStatus(StringStyle.Error, e.Message);
                installProgressBar.style.display = DisplayStyle.None;
                throw;
            }

            if (isSpacetimeCliInstalled)
            {
                installProgressBar.style.display = DisplayStyle.None;
                return;
            }
            
            // Command !found: Update status => Install now
            installProgressBar.title = "Installing...";
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Installing Spacetime CLI");

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
            finally
            {
                installProgressBar.style.display = DisplayStyle.None;
            }
            
            bool hasSpacetimeCli = !installResult.HasErr;
            if (hasSpacetimeCli)
                return;
            
            // Critical error: Spacetime CLI !installed and failed install attempt
            updateStatus(StringStyle.Error, "Failed to install Spacetime CLI: See logs");
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
        #endregion // Action Utils
        
        
        #region Cleanup
        /// This should parity the opposite of setOnActionEvents()
        private void unsetOnActionEvents()
        {
            serverModulePathTxt.UnregisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            setDirectoryBtn.clicked -= onSetDirectoryBtnClick;
            nameTxt.UnregisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked -= onPublishBtnClickAsync;
        }

        private void OnDestroy() => unsetOnActionEvents();
        #endregion // Cleanup
    }
}