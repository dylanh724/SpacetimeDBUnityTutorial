using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// Unlike PublisherWindowCallbacks, these are not called *directly* from UI.
    /// Runs an action -> Processes isSuccess -> calls success || fail @ PublisherWindowCallbacks.
    /// Try to avoid updating UI from here.
    public partial class PublisherWindow
    {
        #region Init from PublisherWindowInit.CreateGUI
        /// Try to get get list of Servers from CLI.
        /// This should be called at init at runtime from PublisherWIndow at CreateGUI time.
        private async Task getServersSetDropdown()
        {
            GetServersResult getServersResult;
            try
            {
                getServersResult = await SpacetimeDbCli.GetServersAsync();
            } 
            catch (Exception e)
            { 
                Debug.LogError($"Error: {e}");
                throw;
            }
            
            // Process result and update UI
            bool isSuccess = getServersResult.HasServer;
            if (!isSuccess)
            {
                onGetSetServersFail(getServersResult);
                return;
            }
            
            // Success
            await onGetSetServersSuccessEnsureDefault(getServersResult.Servers);
            await getIdentitiesSetDropdown(); // Process and reveal the next UI group
        }
        
        /// Try to get get list of Identities from CLI.
        /// (!) Servers must already be set.
        private async Task getIdentitiesSetDropdown()
        {
            // Validate: Only run this when there's a selected server
            bool hasSelectedServer = serverSelectedDropdown.index >= 0;
            if (!hasSelectedServer)
            {
                Debug.LogError("Tried to get identities before server is selected");
                return;
            }
            
            GetIdentitiesResult getIdentitiesResult;
            try
            {
                getIdentitiesResult = await SpacetimeDbCli.GetIdentitiesAsync();
            } 
            catch (Exception e)
            { 
                Debug.LogError($"Error: {e}");
                throw;
            }
            
            // Process result and update UI
            bool isSuccess = getIdentitiesResult.HasIdentity;
            if (!isSuccess)
            {
                onGetSetIdentitiesFail(getIdentitiesResult);
                return;
            }
            
            // Success
            await onGetSetIdentitiesSuccessEnsureDefault(getIdentitiesResult.Identities);
        }
        #endregion // Init from PublisherWindowInit.CreateGUI
        

        /// Validates if we at least have a host name before revealing
        /// bug: If you are calling this from CreateGUI, openFoldout will be ignored.
        private void revealPublishResultCacheIfHostExists(bool? openFoldout)
        {
            bool hasVal = !string.IsNullOrEmpty(publishResultHostTxt.value);
            bool isPlaceholderVal = hasVal && publishResultHostTxt.value.StartsWith("{"); 
            if (!hasVal || isPlaceholderVal)
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
            // Hide install CLI
            installCliGroupBox.style.display = DisplayStyle.None;
            installCliProgressBar.style.display = DisplayStyle.None;
            installCliStatusLabel.style.display = DisplayStyle.None;
            
            // Hide all foldouts and labels from Identity+ (show Server)
            toggleFoldoutRipple(startRippleFrom: FoldoutGroupType.Identity, show:false);
            
            // Hide server
            serverAddNewShowUiBtn.style.display = DisplayStyle.None;
            serverNewGroupBox.style.display = DisplayStyle.None;
            resetServerDropdown();
            serverSelectedDropdown.value = GetStyledStr(StringStyle.Action, "Searching ...");
            
            identityAddNewShowUiBtn.style.display = DisplayStyle.None;
            identityNewGroupBox.style.display = DisplayStyle.None;
            resetIdentityDropdown();
            identitySelectedDropdown.value = GetStyledStr(StringStyle.Action, "Searching ..."); 
            identityAddBtn.SetEnabled(false);
             
            publishGroupBox.style.display = DisplayStyle.None;
            publishResultFoldout.value = false; // Minimize foldout
            publishInstallProgressBar.style.display = DisplayStyle.None;
            
            // Hacky readonly Toggle feat workaround
            publishResultIsOptimizedBuildToggle.SetEnabled(false);
            publishResultIsOptimizedBuildToggle.style.opacity = 1;
        }
        
        /// (!) bug: If NO servers are found, including the default, we'll regenerate them back.
        private void onGetSetServersFail(GetServersResult getServersResult)
        {
            if (!getServersResult.HasServer && !isRegeneratingDefaultServers)
            {
                Debug.Log("[BUG] No servers found; defaults were wiped: " +
                    "regenerating, then trying again...");
                isRegeneratingDefaultServers = true;
                _ = regenerateServers();         
                return;
            }
            
            // Hide dropdown, reveal new ui group
            Debug.Log("No servers found - revealing 'add new server' group");

            isRegeneratingDefaultServers = false; // in case we looped around to a fail
            serverSelectedDropdown.choices.Clear();
            serverSelectedDropdown.style.display = DisplayStyle.None;
            serverNewGroupBox.style.display = DisplayStyle.Flex;
        }

        /// When local and testnet are missing, it's 99% due to a bug:
        /// We'll add them back. Assuming default ports (3000) and testnet targets.
        private async Task regenerateServers()
        {
            Debug.Log("Regenerating default servers: [ local, testnet* ] *Becomes default");
            
            // UI
            serverStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "<b>Regenerating default servers:</b>\n[ local, testnet* ]");
            serverStatusLabel.style.display = DisplayStyle.Flex;

            AddServerRequest addServerRequest = null;
            
            // local (forces `--no-fingerprint` so it doesn't need to be running now)
            addServerRequest = new("local", "http://127.0.0.1:3000");
            _ = await SpacetimeDbCli.AddServerAsync(addServerRequest);
            
            // testnet (becomes default)
            addServerRequest = new("testnet", "https://testnet.spacetimedb.com");
            _ = await SpacetimeDbCli.AddServerAsync(addServerRequest);
            
            // Success - try again
            _ = getServersSetDropdown();
        }

        private void onGetSetIdentitiesFail(GetIdentitiesResult getIdentitiesResult)
        {
            // Hide dropdown, reveal new ui group
            Debug.Log("No identities found - revealing 'add new identity' group");
            
            identitySelectedDropdown.choices.Clear();
            identitySelectedDropdown.style.display = DisplayStyle.None;
            identityNewGroupBox.style.display = DisplayStyle.Flex;
        }

        /// Works around UI Builder bug on init that will add the literal "string" type to [0]
        private void resetIdentityDropdown()
        {
            identitySelectedDropdown.choices.Clear();
            identitySelectedDropdown.value = "";
            identitySelectedDropdown.index = -1;
        }
        
        /// Works around UI Builder bug on init that will add the literal "string" type to [0]
        private void resetServerDropdown()
        {
            serverSelectedDropdown.choices.Clear();
            serverSelectedDropdown.value = "";
            serverSelectedDropdown.index = -1;
        }
        
        /// Set the selected identity dropdown. If identities found but no default, [0] will be set. 
        private async Task onGetSetIdentitiesSuccessEnsureDefault(List<SpacetimeIdentity> identities)
        {
            // Logs for each found, with default shown
            foreach (SpacetimeIdentity identity in identities)
                Debug.Log($"Found identity: {identity}");
            
            // Setting will trigger the onIdentitySelectedDropdownChangedAsync event @ PublisherWindowInit
            for (int i = 0; i < identities.Count; i++)
            {
                SpacetimeIdentity identity = identities[i];
                identitySelectedDropdown.choices.Add(identity.Nickname);

                if (identity.IsDefault)
                {
                    // Set the index to the most recently-added one
                    int recentlyAddedIndex = identitySelectedDropdown.choices.Count - 1;
                    identitySelectedDropdown.index = recentlyAddedIndex;
                }
            }
            
            // Ensure a default was found
            bool foundIdentity = identities.Count > 0;
            bool foundDefault = identitySelectedDropdown.index >= 0;
            if (foundIdentity && !foundDefault)
            {
                Debug.LogError("Found Identities, but no default " +
                    $"Falling back to [0]:{identities[0].Nickname} and setting via CLI...");
                identitySelectedDropdown.index = 0;
            
                // We need a default identity set
                string nickname = identities[0].Nickname;
                await setDefaultIdentityAsync(nickname);
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
        
        /// Set the selected server dropdown. If servers found but no default, [0] will be set. 
        private async Task onGetSetServersSuccessEnsureDefault(List<SpacetimeServer> servers)
        {
            // Logs for each found, with default shown
            foreach (SpacetimeServer server in servers)
                Debug.Log($"Found server: {server}");
            
            // Setting will trigger the onIdentitySelectedDropdownChangedAsync event @ PublisherWindowInit
            for (int i = 0; i < servers.Count; i++)
            {
                SpacetimeServer server = servers[i];
                serverSelectedDropdown.choices.Add(server.Nickname);

                if (server.IsDefault)
                {
                    // Set the index to the most recently-added one
                    int recentlyAddedIndex = serverSelectedDropdown.choices.Count - 1;
                    serverSelectedDropdown.index = recentlyAddedIndex;
                }
            }
            
            // Ensure a default was found
            bool foundServer = servers.Count > 0;
            bool foundDefault = serverSelectedDropdown.index >= 0;
            if (foundServer && !foundDefault)
            {
                Debug.LogError("Found Servers, but no default: " +
                    $"Falling back to [0]:{servers[0].Nickname} and setting via CLI...");
                serverSelectedDropdown.index = 0;
            
                // We need a default server set
                string nickname = servers[0].Nickname;
                await SpacetimeDbCli.SetDefaultServerAsync(nickname);
            }
            
            // Allow selection, show [+] new reveal ui btn
            serverSelectedDropdown.pickingMode = PickingMode.Position;
            serverAddNewShowUiBtn.style.display = DisplayStyle.Flex;
            
            // Hide UI
            serverStatusLabel.style.display = DisplayStyle.None;
            serverNewGroupBox.style.display = DisplayStyle.None;
            
            // Show the next section
            identityFoldout.style.display = DisplayStyle.Flex;
        }
    
        /// This will reveal the group and initially check for the spacetime cli tool
        private void revealPublisherGroupUiAsync()
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
            publishStatusLabel.style.display = DisplayStyle.None;
            _ = startProgressBarAsync(
                publishInstallProgressBar,
                barTitle: "Publishing to SpacetimeDB ...",
                initVal: 1,
                valIncreasePerSec: 1,
                autoHideOnComplete: false);
            
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
            string barTitle = "Running CLI ...",
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
                barTitle: "Installing SpacetimeDB CLI ...",
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
                $"<b>Failed:</b> Could not install Spacetime CLI\n{friendlyFailMsg}");
            
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
            publishStatusLabel.style.display = DisplayStyle.Flex;

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
                "Connecting ...");
            publishStatusLabel.style.display = DisplayStyle.Flex;
        }
        
        /// Install `wasm-opt` npm pkg for a "set and forget" publish optimization boost
        private async Task installWasmOptPackageViaNpmAsync()
        {
            SpacetimeCliResult cliResult = await SpacetimeDbCli.InstallWasmOptPkgAsync();

            bool isSuccess = !cliResult.HasCliErr;
            if (isSuccess)
                onInstallWasmOptPackageViaNpmSuccess();
            else
                onInstallWasmOptPackageViaNpmFail(cliResult);
        }
        
        private async Task addIdentityAsync(string nickname, string email)
        {
            // Add newIdentity
            AddIdentityRequest addIdentityRequestRequest = new(nickname, email);
            SpacetimeCliResult cliResult = await SpacetimeDbCli.AddIdentityAsync(addIdentityRequestRequest);
            SpacetimeIdentity identity = new(nickname, isDefault:true);

            if (cliResult.HasCliErr)
                onAddIdentityFail(identity, cliResult);
            else
                onAddIdentitySuccess(identity);
        }
        
        private async Task addServerAsync(string nickname, string host)
        {
            // Run the CLI cmd
            AddServerResult addServerResult;
            try
            {
                AddServerRequest request = new(nickname, host);
                addServerResult = await SpacetimeDbCli.AddServerAsync(request);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
            
            SpacetimeServer server = new(nickname, host, isDefault:true);

            if (addServerResult.HasCliErr)
                onAddServerFail(server, addServerResult);
            else
                onAddServerSuccess(server);
        }

        private async Task setDefaultIdentityAsync(string idNicknameOrDbAddress)
        {
            // Sanity check
            if (string.IsNullOrEmpty(idNicknameOrDbAddress))
                return;
            
            SpacetimeCliResult cliResult;
            try
            {
                cliResult = await SpacetimeDbCli.SetDefaultIdentityAsync(idNicknameOrDbAddress);                
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }

            bool isSuccess = !cliResult.HasCliErr;
            if (isSuccess)
                Debug.Log($"Changed default identity to: {idNicknameOrDbAddress}");
            else
                Debug.LogError($"Failed to set default identity: {cliResult.CliError}");
        }

        private void resetPublishedInfoCache()
        {
            publishResultHostTxt.value = "";
            publishResultDbAddressTxt.value = "";
            publishResultIsOptimizedBuildToggle.value = false;
        }
        
        /// Toggles the group visibility of the foldouts. Labels also hide if !show.
        /// Toggles ripple downwards from top.
        private void toggleFoldoutRipple(FoldoutGroupType startRippleFrom, bool show)
        {
            // ---------------
            // Server, Identity, Publish, PublishResult
            if (startRippleFrom <= FoldoutGroupType.Server)
            {
                serverFoldout.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show)
                    serverStatusLabel.style.display = DisplayStyle.None;
            }
            
            // ---------------
            // Identity, Publish, PublishResult
            if (startRippleFrom <= FoldoutGroupType.Identity)
            {
                identityFoldout.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show)
                    identityStatusLabel.style.display = DisplayStyle.None;    
            }
            else
                return;
            
            // ---------------
            // Publish, PublishResult
            if (startRippleFrom <= FoldoutGroupType.Publish)
            {
                publishFoldout.style.display = DisplayStyle.None;
                if (!show)
                    publishStatusLabel.style.display = DisplayStyle.None;
            }
            else
                return;

            // ---------------
            // PublishResult+
            if (startRippleFrom <= FoldoutGroupType.PublishResult)
                publishResultFoldout.style.display = DisplayStyle.None;
        }
    }
}