using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        
        /// This is hidden, by default, until a first newIdentity is added
        private async void onIdentitySelectedDropdownChangedAsync(ChangeEvent<string> evt)
        {
            bool selectedAnything = identitySelectedDropdown.index >= 0;
            bool isHidden = identitySelectedDropdown.style.display == DisplayStyle.None;
            
            // We have "some" newIdentity loaded by runtime code; show this dropdown
            if (selectedAnything)
            {
                if (isHidden)
                    identitySelectedDropdown.style.display = DisplayStyle.Flex;
                
                // We changed from a known identity to another known one.
                // We should change the CLI default.
                await setDefaultIdentityAsync(evt.newValue);
            }
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
        
        /// Toggle newIdentity btn enabled based on email + nickname being valid
        private void onIdentityEmailTxtFocusOut(FocusOutEvent evt) =>
            checkIdentityReqsToggleIdentityBtn();
        
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
        private async void onInstallWasmOptBtnClick() =>
            await installWasmOptPackageViaNpmAsync();
        
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

        /// AKA AddNewIdentityBtnClick
        private async void onIdentityAddBtnClickAsync()
        {
            try
            {
                await addNewIdentity(identityNicknameTxt.value, identityEmailTxt.value);
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
    }
}
