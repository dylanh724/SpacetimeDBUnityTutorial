using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static SpacetimeDB.Editor.PublisherMeta;

namespace SpacetimeDB.Editor
{
    /// While `PublisherWindowCallbacks` is for direct-user UI interactions,
    /// These actions trigger the middleware between the UI and CLI
    public partial class PublisherWindow
    {
        /// (1) Suggest module name, if empty
        /// (2) Reveal publisher group
        /// (3) Ensure spacetimeDB CLI is installed async
        private void onDirPathSet()
        {
            // We just updated the path - hide old publish result cache
            publishResultFoldout.style.display = DisplayStyle.None;
            
            // ServerModulePathTxt persists: If previously entered, show the publish group
            bool hasPathSet = !string.IsNullOrEmpty(serverModulePathTxt.value);
            if (hasPathSet)
                revealPublisherGroupUiAsync(); // +Ensures SpacetimeDB CLI is installed async
        }

        /// Validates if we at least have a host name before revealing
        /// bug: If you are calling this from CreateGUI, openFoldout will be ignored.
        private void revealPublishResultCacheIfHostExists(bool? openFoldout)
        {
            if (string.IsNullOrEmpty(publishResultHostTxt.value))
                return;
            
            // Reveal the publish result info cache
            publishResultFoldout.style.display = DisplayStyle.Flex;
            
            if (openFoldout != null)
                publishResultFoldout.value = (bool)openFoldout;
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
            
            // Hacky readonly Toggle feat workaround
            publishResultIsOptimizedBuildToggle.SetEnabled(false);
            publishResultIsOptimizedBuildToggle.style.opacity = 1;
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
                if (publishResult.IsSuccessfulPublish)
                {
                    // Just a warning
                    updateStatus(StringStyle.Success, "Published successfully, +warnings:\n" +
                        publishResult.StyledFriendlyErrorMessage);
                }
                else
                {
                    // Critical fail
                    updateStatus(StringStyle.Error, publishResult.StyledFriendlyErrorMessage 
                        ?? publishResult.CliError);
                    return;   
                }
            }
            
            // Success - reset UI back to normal
            setReadyStatus();
            setPublishResultGroupUi(publishResult);
        }

        private async Task<PublishServerModuleResult> publish()
        {
            _ = startProgressBarAsync(
                barTitle: "Publishing...",
                initVal: 1,
                valIncreasePerSec: 1,
                autoHideOnComplete: false);
            
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Publishing Module to SpacetimeDB");

            PublishConfig publishConfig = new(nameTxt.value, serverModulePathTxt.value);
            
            PublishServerModuleResult publishResult;
            try
            {
                publishResult = await SpacetimeDbCli.PublishServerModuleAsync(publishConfig);
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
            string barTitle = "Running CLI...",
            int initVal = 5, 
            int valIncreasePerSec = 5,
            bool autoHideOnComplete = true)
        {
            installProgressBar.title = barTitle;
            installProgressBar.value = Mathf.Clamp(initVal, 1, 100);
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
            _ = startProgressBarAsync(barTitle: "Checking...", autoHideOnComplete: false);
            publishStatusLabel.text = GetStyledStr(
                StringStyle.Action, 
                "Checking for SpacetimeDB CLI");
            
            // Check if Spacetime CLI is installed => install, if !found
            bool isSpacetimeCliInstalled;

            try
            {
                isSpacetimeCliInstalled = await SpacetimeDbCli.CheckIsSpacetimeCliInstalledAsync();
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
                installResult = await SpacetimeDbCli.InstallSpacetimeCliAsync();
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
    }
}