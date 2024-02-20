using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// While `PublisherWindowActions` is for indirect UI interaction,
    /// Visual Element callbacks directly triggered from the UI via a user,
    /// subscribed to @ PublisherWindowInit.setOnActionEvents.
    /// OnButtonClick, FocusOut, OnChanged, etc.
    /// Set @ setOnActionEvents(), unset at unsetActionEvents().
    /// These actions trigger the middleware between the UI and CLI.
    public partial class PublisherWindow
    {
        #region Init from PublisherWindowInit.cs CreateGUI()
        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            topBannerBtn.clicked += onTopBannerBtnClick;
            
            serverSelectedDropdown.RegisterValueChangedCallback(onServerSelectedDropdownChangedAsync); // Show if !null
            serverAddNewShowUiBtn.clicked += onServerAddNewShowUiBtnClick; // Toggle reveals the "new server" groupbox UI
            serverNicknameTxt.RegisterValueChangedCallback(onServerNicknameTxtChanged); // Replace spaces with dashes
            serverNicknameTxt.RegisterCallback<FocusOutEvent>(onServerNicknameFocusOut);
            serverHostTxt.RegisterCallback<FocusOutEvent>(onServerHostTxtFocusOut); // If valid, enable Add New Server btn
            serverAddBtn.clicked += onServerAddBtnClickAsync; // Add new newServer
            
            identitySelectedDropdown.RegisterValueChangedCallback(onIdentitySelectedDropdownChangedAsync); // Show if !null
            identityAddNewShowUiBtn.clicked += onIdentityAddNewShowUiBtnClick; // Toggle reveals the "new identity" groupbox UI
            identityNicknameTxt.RegisterValueChangedCallback(onIdentityNicknameTxtChanged); // Replace spaces with dashes
            identityNicknameTxt.RegisterCallback<FocusOutEvent>(onIdentityNicknameFocusOut);
            identityEmailTxt.RegisterValueChangedCallback(onIdentityEmailTxtChanged); // Normalize email chars
            identityEmailTxt.RegisterCallback<FocusOutEvent>(onIdentityEmailTxtFocusOut); // If valid, enable Add New Identity btn
            identityAddBtn.clicked += onIdentityAddBtnClickAsync; // Add new newIdentity
            
            publishModulePathTxt.RegisterValueChangedCallback(onPublishModulePathTxtInitChanged); // For init only
            publishModulePathTxt.RegisterCallback<FocusOutEvent>(onPublishModulePathTxtFocusOut); // If !empty, Reveal next UI grou
            publishPathSetDirectoryBtn.clicked += OnPublishPathSetDirectoryBtnClick; // Show folder dialog -> Set path label
            publishModuleNameTxt.RegisterCallback<FocusOutEvent>(onPublishModuleNameTxtFocusOut); // Suggest module name if empty
            publishModuleNameTxt.RegisterValueChangedCallback(onPublishModuleNameTxtChanged); // Replace spaces with dashes
            publishBtn.clicked += onPublishBtnClickAsync; // Start publish chain
            
            // Show [Install Package] btn if !optimized
            publishResultIsOptimizedBuildToggle.RegisterValueChangedCallback(
                onPublishResultIsOptimizedBuildToggleChanged);
            installWasmOptBtn.clicked += onInstallWasmOptBtnClick; // Curry to an async Task => install `wasm-opt` npm pkg
        }
        
        /// Cleanup: This should parity the opposite of setOnActionEvents()
        private void unsetOnActionEvents()
        {
            topBannerBtn.clicked -= onTopBannerBtnClick;
            
            serverSelectedDropdown.UnregisterValueChangedCallback(onServerSelectedDropdownChangedAsync); // Show if !null
            serverAddNewShowUiBtn.clicked -= onServerAddNewShowUiBtnClick; // Toggle reveals the "new server" groupbox UI
            serverNicknameTxt.UnregisterValueChangedCallback(onServerNicknameTxtChanged); // Replace spaces with dashes
            serverNicknameTxt.UnregisterCallback<FocusOutEvent>(onServerNicknameFocusOut);
            serverHostTxt.UnregisterCallback<FocusOutEvent>(onServerHostTxtFocusOut); // If valid, enable Add New Server btn
            serverAddBtn.clicked -= onServerAddBtnClickAsync; // Add new newServer
             
            identitySelectedDropdown.RegisterValueChangedCallback(onIdentitySelectedDropdownChangedAsync);
            identityNicknameTxt.UnregisterValueChangedCallback(onIdentityNicknameTxtChanged);
            identityNicknameTxt.UnregisterCallback<FocusOutEvent>(onIdentityNicknameFocusOut);
            identityEmailTxt.UnregisterValueChangedCallback(onIdentityEmailTxtChanged);
            identityEmailTxt.UnregisterCallback<FocusOutEvent>(onIdentityEmailTxtFocusOut);
            identityAddBtn.clicked -= onIdentityAddBtnClickAsync;
            
            publishModulePathTxt.UnregisterValueChangedCallback(onPublishModulePathTxtInitChanged); // For init only; likely already unsub'd itself
            publishModulePathTxt.UnregisterCallback<FocusOutEvent>(onPublishModulePathTxtFocusOut);
            publishPathSetDirectoryBtn.clicked -= OnPublishPathSetDirectoryBtnClick;
            publishModuleNameTxt.UnregisterCallback<FocusOutEvent>(onPublishModuleNameTxtFocusOut);
            publishModuleNameTxt.UnregisterValueChangedCallback(onPublishModuleNameTxtChanged);
            publishBtn.clicked -= onPublishBtnClickAsync;
            
            publishResultIsOptimizedBuildToggle.UnregisterValueChangedCallback(
                onPublishResultIsOptimizedBuildToggleChanged);
            installWasmOptBtn.clicked -= onInstallWasmOptBtnClick;
        }

        /// Cleanup when the UI is out-of-scope
        private void OnDisable() => unsetOnActionEvents();
        #endregion // Init from PublisherWindowInit.cs CreateGUI()
        
        
        #region Direct UI Callbacks
        /// Open link to SpacetimeDB Module docs
        private void onTopBannerBtnClick() =>
            Application.OpenURL(TOP_BANNER_CLICK_LINK);
        
        /// Normalize with no spacing
        private void onIdentityNicknameTxtChanged(ChangeEvent<string> evt) =>
            identityNicknameTxt.SetValueWithoutNotify(replaceSpacesWithDashes(evt.newValue));

        private void onServerNicknameTxtChanged(ChangeEvent<string> evt) =>
            serverNicknameTxt.SetValueWithoutNotify(replaceSpacesWithDashes(evt.newValue));

        /// Change spaces to dashes
        private void onPublishModuleNameTxtChanged(ChangeEvent<string> evt) =>
            publishModuleNameTxt.SetValueWithoutNotify(replaceSpacesWithDashes(evt.newValue));
        
        /// Normalize with email formatting
        private void onIdentityEmailTxtChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrWhiteSpace(evt.newValue))
                return;
            
            bool isEmailFormat = tryFormatAsEmail(evt.newValue, out string email);
            if (isEmailFormat)
                identityEmailTxt.SetValueWithoutNotify(email);
            else
                identityEmailTxt.SetValueWithoutNotify(evt.previousValue); // Revert non-email attempt
        }
        
        private async void onServerSelectedDropdownChangedAsync(ChangeEvent<string> evt)
        {
            bool selectedAnything = serverSelectedDropdown.index >= 0;
            
            // The old val could've beeen a placeholder "<color=yellow>Searching ...</color>" val
            bool oldValIsPlaceholderStr = selectedAnything && evt.previousValue.Contains("<"); 
            bool isHidden = serverSelectedDropdown.style.display == DisplayStyle.None;
            
            // We have "some" server loaded by runtime code; show this dropdown
            if (!selectedAnything || oldValIsPlaceholderStr)
                return;
            
            if (isHidden)
                serverSelectedDropdown.style.display = DisplayStyle.Flex;
            
            // We changed from a known server to another known one.
            // We should change the CLI default.
            string nickname = evt.newValue;
            await SpacetimeDbCli.SetDefaultServerAsync(nickname);
        }
        
        /// This is hidden, by default, until a first newIdentity is added
        private async void onIdentitySelectedDropdownChangedAsync(ChangeEvent<string> evt)
        {
            bool selectedAnything = identitySelectedDropdown.index >= 0;
            bool isHidden = identitySelectedDropdown.style.display == DisplayStyle.None;
            
            // We have "some" newIdentity loaded by runtime code; show this dropdown
            if (!selectedAnything)
                return;
            
            if (isHidden)
                identitySelectedDropdown.style.display = DisplayStyle.Flex;
            
            // We changed from a known identity to another known one.
            // We should change the CLI default.
            await setDefaultIdentityAsync(evt.newValue);
        }
        
        /// Used for init only, for when the persistent ViewDataKey
        /// val is loaded from EditorPrefs 
        private void onPublishModulePathTxtInitChanged(ChangeEvent<string> evt)
        {
            onDirPathSet();
            revealPublishResultCacheIfHostExists(openFoldout: null);
            publishModulePathTxt.UnregisterValueChangedCallback(onPublishModulePathTxtInitChanged);
        }
        
        /// Toggle newIdentity btn enabled based on email + nickname being valid
        private void onIdentityNicknameFocusOut(FocusOutEvent evt) =>
            checkIdentityReqsToggleIdentityBtn();
        
        /// Toggle newServer btn enabled based on email + nickname being valid
        private void onServerNicknameFocusOut(FocusOutEvent evt) =>
            checkServerReqsToggleServerBtn();
        
        /// Toggle newIdentity btn enabled based on nickname + email being valid
        private void onIdentityEmailTxtFocusOut(FocusOutEvent evt) =>
            checkIdentityReqsToggleIdentityBtn();
        
        /// Toggle newServer btn enabled based on nickname + host being valid
        private void onServerHostTxtFocusOut(FocusOutEvent evt) =>
            checkServerReqsToggleServerBtn();
        
        /// Toggle next section if !null
        private void onPublishModulePathTxtFocusOut(FocusOutEvent evt)
        {
            // Prevent inadvertent UI showing too early, frozen on modal file picking
            if (_isFilePicking)
                return;
            
            bool hasPathSet = !string.IsNullOrEmpty(publishModulePathTxt.value);
            if (hasPathSet)
            {
                // Since we just changed the path, wipe old publish info cache
                resetPublishedInfoCache();
                
                // Normalize, then reveal the next UI group
                publishModulePathTxt.value = superTrim(publishModulePathTxt.value);
                revealPublisherGroupUiAsync();
            }
            else
                publishGroupBox.style.display = DisplayStyle.None;
        }
        
        /// Explicitly declared and curried so we can unsubscribe
        /// There will *always* be a value for nameTxt
        private void onPublishModuleNameTxtFocusOut(FocusOutEvent evt) =>
            suggestModuleNameIfEmpty();

        /// Curry to an async Task to install `wasm-opt` npm pkg
        private async void onInstallWasmOptBtnClick()
        {
            // Disable btn + show installing status
            installWasmOptBtn.SetEnabled(false);
            installWasmOptBtn.text = GetStyledStr(StringStyle.Action, "Installing ...");

            try
            {
                await installWasmOptPackageViaNpmAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e.Message}");
                installWasmOptBtn.text = GetStyledStr(StringStyle.Error, $"<b>Error:</b> {e.Message}");
                throw;
            }
        }
        
        /// Toggles the "new server" group UI
        private void onServerAddNewShowUiBtnClick()
        {
            bool isHidden = serverNewGroupBox.style.display == DisplayStyle.None;
            if (isHidden)
            {
                // Show
                serverNewGroupBox.style.display = DisplayStyle.Flex;
                serverAddNewShowUiBtn.text = GetStyledStr(StringStyle.Success, "-"); // Show opposite, styled
            }
            else
            {
                // Hide
                serverNewGroupBox.style.display = DisplayStyle.None;
                serverAddNewShowUiBtn.text = "+"; // Show opposite
            }
        }
        
        /// Toggles the "new identity" group UI
        private void onIdentityAddNewShowUiBtnClick()
        {
            bool isHidden = identityNewGroupBox.style.display == DisplayStyle.None;
            if (isHidden)
            {
                // Show
                identityNewGroupBox.style.display = DisplayStyle.Flex;
                identityAddNewShowUiBtn.text = GetStyledStr(StringStyle.Success, "-"); // Show opposite, styled
            }
            else
            {
                // Hide
                identityNewGroupBox.style.display = DisplayStyle.None;
                identityAddNewShowUiBtn.text = "+"; // Show opposite
            }
        }
        
        /// Show folder dialog -> Set path label
        private void OnPublishPathSetDirectoryBtnClick()
        {
            string pathBefore = publishModulePathTxt.value;
            // Show folder panel (modal FolderPicker dialog)
            _isFilePicking = true;
            
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Server Module Dir", 
                Application.dataPath, 
                "");
            
            _isFilePicking = false;
            
            // Cancelled or same path?
            bool pathChanged = selectedPath == pathBefore;
            if (string.IsNullOrEmpty(selectedPath) || pathChanged)
                return;
            
            // Path changed: set path val + reveal next UI group
            publishModulePathTxt.value = selectedPath;
            onDirPathSet();
        }
        
        /// Show [Install Package] btn if !optimized
        private void onPublishResultIsOptimizedBuildToggleChanged(ChangeEvent<bool> evt)
        {
            bool isOptimized = evt.newValue;
            installWasmOptBtn.style.display = isOptimized 
                ? DisplayStyle.None 
                : DisplayStyle.Flex;
        }

        /// AKA AddIdentityBtnClick
        private async void onIdentityAddBtnClickAsync()
        {
            string nickname = identityNicknameTxt.value;
            string email = identityEmailTxt.value;
            
            // Sanity check
            if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(email))
                return;
            
            // UI: Disable btn + show installing status to id label
            identityAddBtn.SetEnabled(false);
            identityStatusLabel.text = GetStyledStr(StringStyle.Action, $"Adding {nickname} ...");
            identityStatusLabel.style.display = DisplayStyle.Flex;
            publishStatusLabel.style.display = DisplayStyle.None;
            publishResultFoldout.style.display = DisplayStyle.None;
            
            try
            {
                await addIdentityAsync(nickname, email);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// AKA AddServerBtnClick
        private async void onServerAddBtnClickAsync()
        {
            string nickname = serverNicknameTxt.value;
            string host = serverHostTxt.value;
            
            // Sanity check
            if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(host))
                return;
            
            // UI: Disable btn + show installing status to id label
            serverAddBtn.SetEnabled(false);
            serverStatusLabel.text = GetStyledStr(StringStyle.Action, $"Adding {nickname} ...");
            serverStatusLabel.style.display = DisplayStyle.Flex;
            
            // Hide the other sections (while clearing out their labels), since we rely on servers
            identityStatusLabel.style.display = DisplayStyle.None;
            identityFoldout.style.display = DisplayStyle.None;
            publishFoldout.style.display = DisplayStyle.None;
            publishStatusLabel.style.display = DisplayStyle.None;
            publishResultFoldout.style.display = DisplayStyle.None;
            
            try
            {
                await addServerAsync(nickname, host);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// Curried to an async Task, wrapped this way so
        /// we can unsubscribe and for better err handling 
        private async void onPublishBtnClickAsync()
        {
            try
            {
                setPublishStartUi();
                PublishServerModuleResult publishResult = await publish();
                onPublishDone(publishResult);
            }
            catch (Exception e)
            {
                Debug.LogError($"CliError: {e}");
                throw;
            }
        }
        #endregion // Direct UI Callbacks

        private void onAddServerFail(SpacetimeServer server, AddServerResult addServerResult)
        {
            serverAddBtn.SetEnabled(true);
            serverStatusLabel.text = GetStyledStr(StringStyle.Error, 
                $"<b>Failed:</b> Couldn't add `{server.Nickname}` server</b>\n" +
                addServerResult.StyledFriendlyErrorMessage);
                
            serverStatusLabel.style.display = DisplayStyle.Flex;
        }

        private void onAddIdentityFail(SpacetimeIdentity identity, AddIdentityResult addIdentityResult)
        {
            identityAddBtn.SetEnabled(true);
            identityStatusLabel.text = GetStyledStr(StringStyle.Error, 
                $"<b>Failed:</b> Couldn't add identity `{identity.Nickname}`\n" +
                addIdentityResult.StyledFriendlyErrorMessage);
                
            identityStatusLabel.style.display = DisplayStyle.Flex;
        }
        
        /// Success: Add to dropdown + set default + show. Hide the [+] add group.
        /// Don't worry about caching choices; we'll get the new choices via CLI each load
        private void onAddServerSuccess(SpacetimeServer server)
        {
            Debug.Log($"Add new server success: {server.Nickname}");
            onGetSetServersSuccessEnsureDefault(new List<SpacetimeServer> { server });
        }

        /// Success: Add to dropdown + set default + show. Hide the [+] add group.
        /// Don't worry about caching choices; we'll get the new choices via CLI each load
        private void onAddIdentitySuccess(SpacetimeIdentity identity)
        {
            Debug.Log($"Add new identity success: {identity.Nickname}");
            onGetSetIdentitiesSuccessEnsureDefault(new List<SpacetimeIdentity> { identity });
        }

        /// Success: We still want to show the install button, but tweak it.
        /// It'll hide next time we publish
        private void onInstallWasmOptPackageViaNpmSuccess()
        {
            
            installWasmOptBtn.text = GetStyledStr(StringStyle.Success, "Installed");
            publishResultIsOptimizedBuildToggle.value = true;
        }

        private void onInstallWasmOptPackageViaNpmFail(SpacetimeCliResult cliResult)
        {
            installWasmOptBtn.SetEnabled(false);
        }
    }
}
