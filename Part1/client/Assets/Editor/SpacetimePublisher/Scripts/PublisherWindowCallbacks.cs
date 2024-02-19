using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// Visual Element callbacks directly triggered from the UI via a user,
    /// subscribed to @ PublisherWindow.setOnActionEvents.
    /// OnButtonClick, FocusOut, OnChanged, etc.
    /// Set @ setOnActionEvents(), unset at unsetActionEvents()
    public partial class PublisherWindow
    {
        #region Init from PublisherWindow.cs CreateGUI()
        /// Curry sync Actions from UI => to async Tasks
        private void setOnActionEvents()
        {
            topBannerBtn.clicked += onTopBannerBtnClick;
            
            identitySelectedDropdown.RegisterValueChangedCallback(onIdentitySelectedDropdownChanged); // Show if !null
            identityNicknameTxt.RegisterValueChangedCallback(onIdentityNicknameTxtChanged); // Replace spaces with dashes
            identityNicknameTxt.RegisterCallback<FocusOutEvent>(onIdentityNicknameFocusOut);
            identityEmailTxt.RegisterValueChangedCallback(onIdentityEmailTxtChanged); // Normalize email chars
            identityEmailTxt.RegisterCallback<FocusOutEvent>(onIdentityEmailTxtFocusOut); // If valid, enable Add New Identity btn
            identityAddBtn.clicked += onIdentityAddBtnClickAsync; // Add new identity
            
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
             
            identitySelectedDropdown.RegisterValueChangedCallback(onIdentitySelectedDropdownChanged);
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
        #endregion // Init from PublisherWindow.cs CreateGUI()
        
        
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
        
        /// This is hidden, by default, until a first identity is added
        private void onIdentitySelectedDropdownChanged(ChangeEvent<string> evt)
        {
            bool selectedAnything = identitySelectedDropdown.index >= 0;
            bool isHidden = identitySelectedDropdown.style.display == DisplayStyle.None;
            
            // We have "some" identity loaded by runtime code; show this dropdown
            if (selectedAnything && isHidden)
                identitySelectedDropdown.style.display = DisplayStyle.Flex;
        }
        
        /// Used for init only, for when the persistent ViewDataKey
        /// val is loaded from EditorPrefs 
        private void onPublishModulePathTxtInitChanged(ChangeEvent<string> evt)
        {
            onDirPathSet();
            revealPublishResultCacheIfHostExists(openFoldout: null);
            publishModulePathTxt.UnregisterValueChangedCallback(onPublishModulePathTxtInitChanged);
        }
        
        /// Toggle identity btn enabled based on email + nickname being valid
        private void onIdentityNicknameFocusOut(FocusOutEvent evt) =>
            checkIdentityReqsToggleIdentityBtn();
        
        /// Toggle identity btn enabled based on email + nickname being valid
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
                await startPublishChain();
            }
            catch (Exception e)
            {
                Debug.LogError($"CliError: {e}");
                throw;
            }
        }
        #endregion // Direct UI Callbacks
        
        
        #region Input Formatting Utils
        private string replaceSpacesWithDashes(string str) =>
            str?.Replace(" ", "-");
        
        /// Remove ALL whitespace from string
        private string superTrim(string str) =>
            str?.Replace(" ", "");

        /// This checks for valid email chars for OnChange events
        private bool tryFormatAsEmail(string input, out string formattedEmail)
        {
            formattedEmail = null;
            if (string.IsNullOrWhiteSpace(input)) 
                return false;
    
            // Simplified regex pattern to allow characters typically found in emails
            const string emailCharPattern = @"^[a-zA-Z0-9@._+-]+$"; // Allowing "+" (email aliases)
            if (!Regex.IsMatch(input, emailCharPattern))
                return false;
    
            formattedEmail = input;
            return true;
        }

        /// Useful for FocusOut events, checking the entire email for being valid.
        /// At minimum: "a@b.c"
        private bool checkIsValidEmail(string emailStr)
        {
            // No whitespace, contains "@" contains ".", allows "+" (alias), contains chars in between
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(emailStr, pattern);
        }

        /// Checked at OnFocusOut events to ensure both email+nickname are valid.
        /// Toggle identityAddBtn enabled based validity of both.
        private void checkIdentityReqsToggleIdentityBtn()
        {
            bool isEmailValid = checkIsValidEmail(identityEmailTxt.value);
            bool isNicknameValid = !string.IsNullOrWhiteSpace(identityNicknameTxt.value);
            identityAddBtn.SetEnabled(isEmailValid && isNicknameValid);
        }
        #endregion // Input Formatting Utils
    }
}
