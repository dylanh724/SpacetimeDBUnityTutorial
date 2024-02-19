using System;
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
            publishModulePathTxt.RegisterValueChangedCallback(onServerModulePathTxtInitChanged); // For init only
            publishModulePathTxt.RegisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            publishPathSetDirectoryBtn.clicked += OnPublishPathSetDirectoryBtnClick;
            publishModuleNameTxt.RegisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked += onPublishBtnClickAsync;
            publishResultIsOptimizedBuildToggle.RegisterValueChangedCallback(
                onPublishResultIsOptimizedBuildToggleChanged);
            installWasmOptBtn.clicked += onInstallWasmOptBtnClick;
        }
        
        /// Cleanup: This should parity the opposite of setOnActionEvents()
        private void unsetOnActionEvents()
        {
            topBannerBtn.clicked -= onTopBannerBtnClick;
            publishModulePathTxt.UnregisterValueChangedCallback(onServerModulePathTxtInitChanged); // For init only
            publishModulePathTxt.UnregisterCallback<FocusOutEvent>(onServerModulePathTxtFocusOut);
            publishPathSetDirectoryBtn.clicked -= OnPublishPathSetDirectoryBtnClick;
            publishModuleNameTxt.UnregisterCallback<FocusOutEvent>(onNameTxtFocusOut);
            publishBtn.clicked -= onPublishBtnClickAsync;
            publishResultIsOptimizedBuildToggle.UnregisterValueChangedCallback(
                onPublishResultIsOptimizedBuildToggleChanged);
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
        
        /// Used for init only, for when the persistent ViewDataKey
        /// val is loaded from EditorPrefs 
        private void onServerModulePathTxtInitChanged(ChangeEvent<string> evt)
        {
            onDirPathSet();
            revealPublishResultCacheIfHostExists(openFoldout: null);
            publishModulePathTxt.UnregisterValueChangedCallback(onServerModulePathTxtInitChanged);
        }
        
        /// Toggle next section if !null
        private void onServerModulePathTxtFocusOut(FocusOutEvent evt)
        {
            // Prevent inadvertent UI showing too early, frozen on modal file picking
            if (_isFilePicking)
                return;
            
            bool hasPathSet = !string.IsNullOrEmpty(publishModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync();
            else
                publishGroupBox.style.display = DisplayStyle.None;
        }
        
        /// Explicitly declared and curried so we can unsubscribe
        private void onNameTxtFocusOut(FocusOutEvent evt) =>
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
    }
}
