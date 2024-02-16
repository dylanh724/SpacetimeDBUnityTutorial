using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// Directly triggered from the UI via a user,
    /// subscribed to @ PublisherWindow.setOnActionEvents.
    /// OnButtonClick, FocusOut, OnChanged, etc.
    public partial class PublisherWindow
    {
        /// Open link to SpacetimeDB Module docs
        private void onTopBannerBtnClick() =>
            Application.OpenURL(TOP_BANNER_CLICK_LINK);
        
        /// Used for init only, for when the persistent ViewDataKey
        /// val is loaded from EditorPrefs 
        private void onServerModulePathTxtInitChanged(ChangeEvent<string> evt)
        {
            onDirPathSet();
            revealPublishResultCacheIfHostExists(openFoldout: null);
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

        /// Curry to an async Task to install `wasm-opt` npm pkg
        private async void onInstallWasmOptBtnClick() =>
            await installWasmOptPackageViaNpmAsync();
        
        /// Show folder dialog -> Set path label
        private void onSetDirectoryBtnClick()
        {
            string pathBefore = serverModulePathTxt.value;
            // Show folder panel (modal FolderPicker dialog)
            isFilePicking = true;
            
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Server Module Dir", 
                Application.dataPath, 
                "");
            
            isFilePicking = false;
            
            // Cancelled or same path?
            bool pathChanged = selectedPath == pathBefore;
            if (string.IsNullOrEmpty(selectedPath) || pathChanged)
                return;
            
            // Path changed: set path val + reveal next UI group
            serverModulePathTxt.value = selectedPath;
            onDirPathSet();
        }
        
        /// Unity does not have a readonly prop for Toggles, yet; hacky workaround.
        private void onPublishResultIsOptimizedBuildToggleChanged(ChangeEvent<bool> evt)
        {
            // Revert to old val
            publishResultIsOptimizedBuildToggle.SetValueWithoutNotify(evt.previousValue);
            evt.StopPropagation();
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
    }
}
