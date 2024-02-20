using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// Used for indirect actions from UI, not called directly from the UI itself.
    /// For example, DoSomething() would fit here more than OnBtnClick() found @ `PublisherWindowCallbacks.cs`
    /// Often wrapped in try/catch.
    public partial class PublisherWindow
    {
        #region Init from PublisherWindowInit.CreateGUI
        /// Try to get get list of Identities from CLI
        private async Task<GetIdentitiesResult> getIdentitiesSetDropdown()
        {
            GetIdentitiesResult getIdentitiesResult;
            try
            {
                getIdentitiesResult = await SpacetimeDbCli.GetSetIdentitiesAsync();
            } 
            catch (Exception e)
            { 
                Debug.LogError($"Error: {e}");
                throw;
            }
            
            // Process result and update UI
            bool isSuccess = getIdentitiesResult.HasIdentity;
            if (isSuccess)
                onGetSetIdentitiesSuccess(getIdentitiesResult.Identities);
            else
                onGetSetIdentitiesFail(getIdentitiesResult);

            return getIdentitiesResult;
        }
        #endregion // Init from PublisherWindowInit.CreateGUI
        

        /// Validates if we at least have a host name before revealing
        /// bug: If you are calling this from CreateGUI, openFoldout will be ignored.
        private void revealPublishResultCacheIfHostExists(bool? openFoldout)
        {
            bool hasVal = string.IsNullOrEmpty(publishResultHostTxt.value);
            bool isPlaceholderVal = publishResultHostTxt.value.StartsWith("{"); 
            if (hasVal || isPlaceholderVal)
                return;
            
            // Reveal the publish result info cache
            publishResultFoldout.style.display = DisplayStyle.Flex;
            
            if (openFoldout != null)
                publishResultFoldout.value = (bool)openFoldout;
        }
        
        /// (1) Suggest module name, if empty
        /// (2) Reveal publisher group
        /// (3) Ensure spacetimeDB CLI is installed async
        private void onDirPathSet()
        {
            // We just updated the path - hide old publish result cache
            publishResultFoldout.style.display = DisplayStyle.None;
            
            // Set the tooltip to equal the path, since it's likely cutoff
            publishModulePathTxt.tooltip = publishModulePathTxt.value;
            
            // ServerModulePathTxt persists: If previously entered, show the publish group
            bool hasPathSet = !string.IsNullOrEmpty(publishModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync(); // +Ensures SpacetimeDB CLI is installed async
        }
        
        /// Dynamically sets a dashified-project-name placeholder, if empty
        private void suggestModuleNameIfEmpty()
        {
            // Set the server module name placeholder text dynamically, based on the project name
            // Replace non-alphanumeric chars with dashes
            bool hasName = !string.IsNullOrEmpty(publishModuleNameTxt.value);
            if (hasName)
                return; // Keep whatever the user customized
            
            // Prefix "unity-", dashify the name, replace "client" with "server (if found).
            string unityProjectName = $"unity-{Application.productName.ToLowerInvariant()}";
            string projectNameDashed = System.Text.RegularExpressions.Regex
                .Replace(unityProjectName, @"[^a-z0-9]", "-")
                .Replace("client", "server");
            
            publishModuleNameTxt.value = projectNameDashed;
        }
        
        /// - Set to the initial state as if no inputs were set.
        /// - This exists so we can show all ui elements simultaneously in the
        ///   ui builder for convenience.
        /// - (!) If called from CreateGUI, after a couple frames,
        ///       any persistence may override this.
        private void resetUi()
        {
            installCliGroupBox.style.display = DisplayStyle.None;
            installCliProgressBar.style.display = DisplayStyle.None;
            installCliStatusLabel.style.display = DisplayStyle.None;
            
            identityAddNewShowUiBtn.style.display = DisplayStyle.None;
            identityNewGroupBox.style.display = DisplayStyle.None;
            resetIdentityDropdown();
            identitySelectedDropdown.value = GetStyledStr(StringStyle.Action, "Searching..."); 
            identityStatusLabel.style.display = DisplayStyle.None;
            identityAddBtn.SetEnabled(false);
             
            publishFoldout.style.display = DisplayStyle.None;
            publishGroupBox.style.display = DisplayStyle.None;
            publishInstallProgressBar.style.display = DisplayStyle.None;
            publishStatusLabel.style.display = DisplayStyle.None;
            publishResultFoldout.style.display = DisplayStyle.None;
            publishResultFoldout.value = false;
            publishInstallProgressBar.style.display = DisplayStyle.None;
            
            // Hacky readonly Toggle feat workaround
            publishResultIsOptimizedBuildToggle.SetEnabled(false);
            publishResultIsOptimizedBuildToggle.style.opacity = 1;
        }
        
          private void onGetSetIdentitiesFail(GetIdentitiesResult getIdentitiesResult)
        {
            // Hide dropdown, reveal new ui group
            Debug.Log("No identities found - revealing new identity group");
            
            identitySelectedDropdown.choices.Clear();
            identitySelectedDropdown.style.display = DisplayStyle.None;
            identityNewGroupBox.style.display = DisplayStyle.Flex;
        }

        private void resetIdentityDropdown()
        {
            identitySelectedDropdown.choices.Clear();
            identitySelectedDropdown.value = "";
            identitySelectedDropdown.index = -1;
        }
        
        /// Set the identity dropdown. TODO: Do we have any reason to cache this list?
        private void onGetSetIdentitiesSuccess(List<SpacetimeIdentity> identities)
        {
            // Logs for each found, with default shown
            foreach (SpacetimeIdentity identity in identities)
                Debug.Log($"Found identity: {identity}");
            
            // Set the dropdown with the newIdentity nicknames

            
            // Setting will trigger the onIdentitySelectedDropdownChangedAsync event @ PublisherWindowInit
            for (int i = 0; i < identities.Count; i++)
            {
                SpacetimeIdentity identity = identities[i];
                identitySelectedDropdown.choices.Add(identity.Nickname);

                if (identity.IsDefault)
                    identitySelectedDropdown.index = i;
            }
            
            // Ensure a default was found
            bool foundDefault = identitySelectedDropdown.index >= 0;
            if (!foundDefault)
            {
                Debug.LogError($"No default identity found - Falling back to [0]:{identities[0].Nickname}");
                identitySelectedDropdown.index = 0;
                // => When this is returned, the next task should be setDefaultIdentityAsync()
            }
            
            // Allow selection, show [+] new reveal ui btn
            identitySelectedDropdown.pickingMode = PickingMode.Position;
            identityAddNewShowUiBtn.style.display = DisplayStyle.Flex;
            
            // Hide UI
            identityStatusLabel.style.display = DisplayStyle.None;
            identityNewGroupBox.style.display = DisplayStyle.None;
            
            // Show the next section
            publishFoldout.style.display = DisplayStyle.Flex;
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
            setPublishReadyStatus();
        }

        /// Sets status label to "Ready" and enables Publisher btn
        private void setPublishReadyStatus()
        {
            publishBtn.SetEnabled(true);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Success, 
                "Ready");
        }

        private void onPublishDone(PublishServerModuleResult publishResult)
        {
            if (publishResult.HasPublishErr)
            {
                if (publishResult.IsSuccessfulPublish)
                {
                    // Just a warning
                    updatePublishStatus(StringStyle.Success, "Published successfully, +warnings:\n" +
                        publishResult.StyledFriendlyErrorMessage);
                }
                else
                {
                    // Critical fail
                    updatePublishStatus(StringStyle.Error, publishResult.StyledFriendlyErrorMessage 
                        ?? publishResult.CliError);
                    return;   
                }
            }
            
            // Success - reset UI back to normal
            setPublishReadyStatus();
            setPublishResultGroupUi(publishResult);
        }

        private async Task<PublishServerModuleResult> publish()
        {
            _ = startProgressBarAsync(
                publishInstallProgressBar,
                barTitle: "Publishing...",
                initVal: 1,
                valIncreasePerSec: 1,
                autoHideOnComplete: false);
            
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Publishing Module to SpacetimeDB");

            PublishRequest publishRequest = new(publishModuleNameTxt.value, publishModulePathTxt.value);
            
            PublishServerModuleResult publishResult;
            try
            {
                publishResult = await SpacetimeDbCli.PublishServerModuleAsync(publishRequest);
            }
            catch (Exception e)
            {
                Debug.LogError($"CliError: {e}");
                throw;
            }
            finally
            {
                publishInstallProgressBar.style.display = DisplayStyle.None;
            }

            return publishResult;
        }

        private void setPublishResultGroupUi(PublishServerModuleResult publishResult)
        {
            // Load the result data
            publishResultHostTxt.value = publishResult.UploadedToHost;
            publishResultDbAddressTxt.value = publishResult.DatabaseAddressHash;
            
            // Set via ValueWithoutNotify since this is a hacky "readonly" Toggle (no official feat for this, yet)
            bool isOptimizedBuildUsingWasmOpt = !publishResult.CouldNotFindWasmOpt;
            publishResultIsOptimizedBuildToggle.value = isOptimizedBuildUsingWasmOpt;
            
            // Show the result group and expand the foldout
            revealPublishResultCacheIfHostExists(openFoldout: true);
        }

        /// Show progress bar, clamped to 1~100, updating every 1s
        /// Stops when reached 100, or if style display is hidden
        private async Task startProgressBarAsync(
            ProgressBar progressBar,
            string barTitle = "Running CLI...",
            int initVal = 5, 
            int valIncreasePerSec = 5,
            bool autoHideOnComplete = true)
        {
            progressBar.title = barTitle;
            progressBar.value = Mathf.Clamp(initVal, 1, 100);
            progressBar.style.display = DisplayStyle.Flex;
            
            while (progressBar.value < 100 && 
                   progressBar.style.display == DisplayStyle.Flex)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                progressBar.value += valIncreasePerSec;
            }
            
            if (autoHideOnComplete)
                progressBar.style.display = DisplayStyle.None;
        }
        
        /// Check for install => Install if !found -> Throw if err
        private async Task ensureSpacetimeCliInstalledAsync()
        {
            // Check if Spacetime CLI is installed => install, if !found
            bool isSpacetimeCliInstalled;

            try
            {
                isSpacetimeCliInstalled = await SpacetimeDbCli.CheckIsSpacetimeCliInstalledAsync();
            }
            catch (Exception e)
            {
                onSpacetimeCliInstallFail(e.Message);
                throw;
            }

            if (isSpacetimeCliInstalled)
            {
                onSpacetimeCliInstallSuccess();
                return;
            }
            
            // Command !found: Update status => Install now
            _ = startProgressBarAsync(
                installCliProgressBar, 
                barTitle: "Installing SpacetimeDB CLI...",
                initVal: 4,
                valIncreasePerSec: 4,
                autoHideOnComplete: true);

            installCliStatusLabel.style.display = DisplayStyle.None;
            installCliGroupBox.style.display = DisplayStyle.Flex;

            SpacetimeCliResult installResult;
            try
            {
                installResult = await SpacetimeDbCli.InstallSpacetimeCliAsync();
            }
            catch (Exception e)
            {
                onSpacetimeCliInstallFail(e.Message);
                throw;
            }
            finally
            {
                publishInstallProgressBar.style.display = DisplayStyle.None;
            }
            
            bool hasSpacetimeCli = !installResult.HasCliErr;
            if (hasSpacetimeCli)
            {
                installCliGroupBox.style.display = DisplayStyle.None;
                return; // Success
            }
            
            // Critical error: Spacetime CLI !installed and failed install attempt
            onSpacetimeCliInstallFail("See logs");
            installCliStatusLabel.style.display = DisplayStyle.Flex;
        }

        private void onSpacetimeCliInstallFail(string friendlyFailMsg)
        {
            installCliStatusLabel.text = GetStyledStr(
                StringStyle.Error,
                $"<b>Failed:</b> Could not install Spacetime CLI - {friendlyFailMsg}");
            
            installCliStatusLabel.style.display = DisplayStyle.Flex;
            installCliGroupBox.style.display = DisplayStyle.Flex;
        }

        /// Hide CLI group
        private void onSpacetimeCliInstallSuccess()
        {
            installCliProgressBar.style.display = DisplayStyle.None;
            installCliGroupBox.style.display = DisplayStyle.None;
        }

        /// Show a styled friendly string to UI. Errs will enable publish btn.
        private void updatePublishStatus(StringStyle style, string friendlyStr)
        {
            publishStatusLabel.text = GetStyledStr(style, friendlyStr);

            if (style != StringStyle.Error)
                return;
            
            publishBtn.SetEnabled(true);
        }
        
        private void setPublishStartUi()
        {
            // Set UI
            publishResultFoldout.style.display = DisplayStyle.None;
            publishBtn.SetEnabled(false);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Connecting...");
            publishStatusLabel.style.display = DisplayStyle.Flex;
        }
        
        /// Install `wasm-opt` npm pkg for a "set and forget" publish optimization boost
        private async Task installWasmOptPackageViaNpmAsync()
        {
            // Disable btn + show installing status
            installWasmOptBtn.SetEnabled(false);
            installWasmOptBtn.text = GetStyledStr(StringStyle.Action, "Installing...");

            SpacetimeCliResult cliResult;
            try
            {
                cliResult = await SpacetimeDbCli.InstallWasmOptPkgAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e.Message}");
                installWasmOptBtn.text = GetStyledStr(StringStyle.Error, $"<b>Error:</b> {e.Message}");
                throw;
            }

            bool isSuccess = !cliResult.HasCliErr;
            if (!isSuccess)
            {
                // Error
                installWasmOptBtn.SetEnabled(false);
                return;
            }
            
            // Success: We still want to show the install button, but tweak it.
            // It'll hide next time we publish
            installWasmOptBtn.text = GetStyledStr(StringStyle.Success, "Installed");
            publishResultIsOptimizedBuildToggle.value = true;
        }
        
        private async Task addNewIdentity(string nickname, string email)
        {
            // Sanity check
            if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(email))
                return;
            
            // UI: Disable btn + show installing status to id label
            identityAddBtn.SetEnabled(false);
            identityStatusLabel.text = GetStyledStr(StringStyle.Action, $"Adding {nickname}...");
            identityStatusLabel.style.display = DisplayStyle.Flex;
            publishStatusLabel.style.display = DisplayStyle.None;
            publishResultFoldout.style.display = DisplayStyle.None;
            
            // Add newIdentity
            NewNewIdentityRequest newNewIdentityRequest = new(nickname, email);
            SpacetimeCliResult cliResult = await SpacetimeDbCli.AddNewIdentityAsync(newNewIdentityRequest);
            SpacetimeIdentity identity = new(nickname, isDefault:true);
            onAddNewIdentityDone(identity, cliResult);
        }

        private void onAddNewIdentityDone(SpacetimeIdentity identity, SpacetimeCliResult cliResult)
        {
            // Common
            
            if (cliResult.HasCliErr)
            {
                // Fail
                identityAddBtn.SetEnabled(true);
                identityStatusLabel.text = GetStyledStr(
                    StringStyle.Error, 
                    $"<b>Failed to add `{identity.Nickname}`</b>: {cliResult.CliError}");
                
                identityStatusLabel.style.display = DisplayStyle.Flex;
            }

            // Success: Add to dropdown + set default + show. Hide the [+] add group.
            // Don't worry about caching choices; we'll get the new choices via CLI each load
            Debug.Log($"Added new identity success: {identity.Nickname}");
            onGetSetIdentitiesSuccess(new List<SpacetimeIdentity> { identity });
        }

        private async Task setDefaultIdentityAsync(string nicknameOrDbAddress)
        {
            // Sanity check
            if (string.IsNullOrEmpty(nicknameOrDbAddress))
                return;
            
            SpacetimeCliResult cliResult;
            try
            {
                cliResult = await SpacetimeDbCli.SetDefaultIdentityAsync(nicknameOrDbAddress);                
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }

            bool isSuccess = !cliResult.HasCliErr;
            if (isSuccess)
                Debug.Log($"Changed default identity to: {nicknameOrDbAddress}");
            else
                Debug.LogError($"Failed to set default identity: {cliResult.CliError}");
        }

        private void resetPublishedInfoCache()
        {
            publishResultHostTxt.value = "";
            publishResultDbAddressTxt.value = "";
            publishResultIsOptimizedBuildToggle.value = false;
        }
        
        
    }
}