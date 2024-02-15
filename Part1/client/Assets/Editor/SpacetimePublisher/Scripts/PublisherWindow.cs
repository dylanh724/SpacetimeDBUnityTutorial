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
        /// <summary>
        /// Since we have FocusOut events, this will sometimes trigger
        /// awkwardly if you jump from input to a file picker button
        /// </summary>
        bool isFilePicking;
        
        #region UI Visual Elements
        private Button topBannerBtn;
        
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
        private Foldout publishResultFoldout;
        private TextField publishResultHostTxt;
        private TextField publishResultDbAddressTxt;
        private Toggle publishResultIsOptimizedBuildToggle;
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
        }

        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            topBannerBtn.clicked += onTopBannerBtnClick;
            serverModulePathTxt.RegisterValueChangedCallback(onServerModulePathTxtInitChanged); // For init only
            serverModulePathTxt.RegisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            setDirectoryBtn.clicked += onSetDirectoryBtnClick;
            nameTxt.RegisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked += onPublishBtnClickAsync;
        }
        #endregion // Init
        
        
        #region Direct UI Interaction Callbacks
        /// (1) Suggest module name, if empty
        /// (2) Reveal publisher group
        /// (3) Ensure spacetimeDB CLI is installed async
        private void onDirPathSet()
        {
            suggestModuleNameIfEmpty();
            
            // ServerModulePathTxt persists: If previously entered, show the publish group
            bool hasPathSet = !string.IsNullOrEmpty(serverModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync(); // +Ensures SpacetimeDB CLI is installed async
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
                Debug.LogError($"CliError: {e}");
                throw;
            }
        }

        /// Used for init only, for when the persistent ViewDataKey
        /// val is loaded from EditorPrefs 
        private void onServerModulePathTxtInitChanged(ChangeEvent<string> evt)
        {
            onDirPathSet();
            serverModulePathTxt.UnregisterValueChangedCallback(onServerModulePathTxtInitChanged);
        }
        
        /// Toggle next section if !null
        private void onServerModulePathTxtFocusOut(FocusOutEvent evt)
        {
            // Prevent inadvertent UI showing too early, frozen on modal file picking
            if (isFilePicking)
                return;
            
            bool hasPathSet = !string.IsNullOrEmpty(serverModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync();
            else
                publishGroupBox.style.display = DisplayStyle.None;
        }
        
        /// Explicitly declared and curried so we can unsubscribe
        private void onNameTxtFocusOut(FocusOutEvent evt) =>
            suggestModuleNameIfEmpty();
        
        /// Open link to SpacetimeDB Module docs
        private void onTopBannerBtnClick() =>
            Application.OpenURL(TOP_BANNER_CLICK_LINK);
        
        /// Show folder dialog -> Set path label
        private void onSetDirectoryBtnClick()
        {
            // Show folder panel (modal FolderPicker dialog)
            isFilePicking = true;
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Server Module Dir", 
                Application.dataPath, 
                "");
            isFilePicking = false;
            
            // Cancelled?
            if (string.IsNullOrEmpty(selectedPath))
                return;
            
            // Set path label to selected path to reflect UI
            serverModulePathTxt.value = selectedPath;

            // Reveal the next group
            onDirPathSet();
        }
        #endregion // Direct UI Interaction Callbacks
        
        
        #region Action 
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
        
        /// - Set to the initial state as if no inputs were set.
        /// - This exists so we can show all ui elements simultaneously in the
        ///   ui builder for convenience.
        private void resetUi()
        {
            publishGroupBox.style.display = DisplayStyle.None;
            installProgressBar.style.display = DisplayStyle.None;
            publishStatusLabel.style.display = DisplayStyle.None;
            publishResultFoldout.style.display = DisplayStyle.None;
            publishResultFoldout.value = false;
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
                setReadyStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"CliError: {e}");
                throw;
            }
            finally
            {
                // Reenable, no matter what, so we can try again if we want to
                publishGroupBox.SetEnabled(true);
            }
        }

        /// Sets status label to "Ready" and enables Publisher btn
        private void setReadyStatus()
        {
            publishBtn.SetEnabled(true);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Success, 
                "Ready");
        }
                                    
        /// Init -> prereqs => publish => done
        /// High-level event chain handler.
        private async Task startPublishChain()
        {
            setPublishStartUi();
            await ensureSpacetimeCliInstalledAsync(); // Sanity check
            
            PublishServerModuleResult publishResult = await publish();
            onPublishDone(publishResult);
        }

        private void onPublishDone(PublishServerModuleResult publishResult)
        {
            if (publishResult.HasPublishErr)
            {
                updateStatus(StringStyle.Error, publishResult.StyledFriendlyErrorMessage 
                    ?? publishResult.CliError);
                return;
            }
            
            // Success - reset UI back to normal
            setReadyStatus();
            setPublishResultGroupUi(publishResult);
        }

        private async Task<PublishServerModuleResult> publish()
        {
            _ = startProgressBarAsync(
                title: "Publishing...",
                initVal: 2,
                valIncreasePerSec: 2,
                autoHideOnComplete: false);
            
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Publishing Module to SpacetimeDB");

            PublishConfig publishConfig = new(nameTxt.value, serverModulePathTxt.value);
            
            PublishServerModuleResult publishResult;
            try
            {
                publishResult = await SpacetimeCli.PublishServerModuleAsync(publishConfig);
            }
            catch (Exception e)
            {
                Debug.LogError($"CliError: {e}");
                throw;
            }
            finally
            {
                installProgressBar.style.display = DisplayStyle.None;
            }

            return publishResult;
        }

        private void setPublishResultGroupUi(PublishServerModuleResult publishResult)
        {
            // Load the result data
            publishResultHostTxt.value = publishResult.UploadedToUrlAndPort;
            publishResultDbAddressTxt.value = publishResult.DatabaseAddressHash;
            publishResultIsOptimizedBuildToggle.value = !publishResult.CouldNotFindWasmOpt;
            
            // Show the result group and expand the foldout
            publishResultFoldout.style.display = DisplayStyle.Flex;
            publishResultFoldout.value = true;
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
                "Checking for SpacetimeDB CLI");
            
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
                "Installing SpacetimeDB CLI");

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
            
            bool hasSpacetimeCli = !installResult.HasCliErr;
            if (hasSpacetimeCli)
                return;
            
            // Critical error: Spacetime CLI !installed and failed install attempt
            updateStatus(StringStyle.Error, "Failed to install Spacetime CLI: See logs");
        }

        /// Show a styled friendly string to UI. Errs will enable publish btn.
        private void updateStatus(StringStyle style, string friendlyStr)
        {
            publishStatusLabel.text = GetStyledStr(style, friendlyStr);

            if (style != StringStyle.Error)
                return;
            
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
        #endregion // Action Utils
        
        
        #region Cleanup
        /// This should parity the opposite of setOnActionEvents()
        private void unsetOnActionEvents()
        {
            topBannerBtn.clicked -= onTopBannerBtnClick;
            serverModulePathTxt.UnregisterValueChangedCallback(onServerModulePathTxtInitChanged); // For init only
            serverModulePathTxt.UnregisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            setDirectoryBtn.clicked -= onSetDirectoryBtnClick;
            nameTxt.UnregisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked -= onPublishBtnClickAsync;
        }

        private void OnDisable() => unsetOnActionEvents();
        #endregion // Cleanup
    }
}