using System;
using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace SpacetimeDB.Editor
{
    /// CLI action helper for PublisherWindow
    public static class SpacetimeDbCli
    {
        #region Static Options
        /// TODO: Possibly integrate this within the PublisherWindow?
        private const CliLogLevel LOG_LEVEL = CliLogLevel.Info;
        
        public enum CliLogLevel
        {
            Info,
            Error,
        }
        #endregion // Static Options

        
        #region Init
        /// Install the SpacetimeDB CLI | https://spacetimedb.com/install 
        public static async Task<SpacetimeCliResult> InstallSpacetimeCliAsync()
        {
            if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log("Installing SpacetimeDB CLI tool...");
            
            SpacetimeCliResult result; 
            
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    result = await runCliCommandAsync("powershell -Command \"iwr " +
                        "https://windows.spacetimedb.com -UseBasicParsing | iex\"\n");
                    break;
                
                case RuntimePlatform.OSXEditor:
                    result = await runCliCommandAsync("brew install clockworklabs/tap/spacetime");
                    break;
                
                case RuntimePlatform.LinuxEditor:
                    result = await runCliCommandAsync("curl -sSf https://install.spacetimedb.com | sh");
                    break;
                
                default:
                    throw new NotImplementedException("Unsupported OS");
            }
            
            if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log($"Installed spacetimeDB CLI tool | {PublisherMeta.DOCS_URL}");
            
            return result;
        }
        #endregion // Init
        
        
        #region Core CLI
        private static async Task<SpacetimeCliResult> runCliCommandAsync(string argSuffix)
        {
            string terminal = getTerminalPrefix(); // Cross-Platform terminal: cmd || bash
            string argPrefix = getCommandPrefix();
            string fullParsedArgs = $"{argPrefix} \"{argSuffix}\"";

            using Process process = new();
            process.StartInfo.FileName = terminal;
            process.StartInfo.Arguments = fullParsedArgs;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Input Logs
            if (LOG_LEVEL == CliLogLevel.Info)
            {
                Debug.Log("CLI Input: \n```\n<color=yellow>" +
                    $"{terminal} {fullParsedArgs}</color>\n```\n");
            }
            
            // Results
            string output = null;
            string error = null;

            try
            {
                process.Start();

                output = await process.StandardOutput.ReadToEndAsync();
                error = await process.StandardError.ReadToEndAsync();
                
                process.WaitForExit();
            }
            catch (Exception e)
            {
                Debug.LogError($"spacetime CLI check failed: {e.Message}");
            }
            
            // Process results, log err (if any), return parsed Result 
            SpacetimeCliResult cliResult = new(output, error);
            logCliResults(cliResult);

            return cliResult;
        }
        
        private static void logCliResults(SpacetimeCliResult cliResult)
        {
            bool hasOutput = !string.IsNullOrEmpty(cliResult.CliOutput);
            bool hasLogLevelInfoNoErr = LOG_LEVEL == CliLogLevel.Info && !cliResult.HasCliErr;
            string prettyOutput = $"\n```\n<color=yellow>{cliResult.CliOutput}</color>\n```\n";
            
            if (hasOutput && hasLogLevelInfoNoErr)
                Debug.Log($"CLI Output: {prettyOutput}");

            if (cliResult.HasCliErr)
            {
                Debug.LogError($"CLI Output (with verbose errors): {prettyOutput}");
                Debug.LogError($"CLI Error: {cliResult.CliError}\n" +
                    "(For +details, see output err above)");
            }
        }
        
        private static string getCommandPrefix()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return "/c";
                
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return "-c";
                
                default:
                    Debug.LogError("Unsupported OS");
                    return null;
            }
        }

        /// Return either "cmd.exe" || "/bin/bash"
        private static string getTerminalPrefix()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return "cmd.exe";
                
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return "/bin/bash";
                
                default:
                    Debug.LogError("Unsupported OS");
                    return null;
            }
        }
        #endregion // Core CLI
            
        
        #region High Level CLI Actions
        public static async Task<bool> CheckIsSpacetimeCliInstalledAsync()
        {
            SpacetimeCliResult cliResult = await runCliCommandAsync("spacetime version");

            bool isSpacetimeCliInstalled = !cliResult.HasCliErr;
            if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log($"{nameof(isSpacetimeCliInstalled)}=={isSpacetimeCliInstalled}");

            return isSpacetimeCliInstalled;
        }
        
        /// Uses the `spacetime publish` CLI command, appending +args from UI elements
        public static async Task<PublishServerModuleResult> PublishServerModuleAsync(PublishRequest publishRequest)
        {
            string argSuffix = $"spacetime publish {publishRequest}";
            SpacetimeCliResult cliResult = await runCliCommandAsync(argSuffix);
            return onPublishServerModuleDone(cliResult);
        }
        
        /// Uses the `spacetime identity new` CLI command, then set as default.
        public static async Task<SpacetimeCliResult> AddNewIdentityAsync(NewIdentityRequest newIdentityRequest)
        {
            string argSuffix = $"spacetime identity new {newIdentityRequest} -d"; // -d == set as default
            SpacetimeCliResult cliResult = await runCliCommandAsync(argSuffix);
            return cliResult;
        }
        
        /// Uses the `npm install -g wasm-opt` CLI command
        public static async Task<SpacetimeCliResult> InstallWasmOptPkgAsync()
        {
            const string argSuffix = "npm install -g wasm-opt";
            SpacetimeCliResult cliResult = await runCliCommandAsync(argSuffix);
            return onInstallWasmOptPkgDone(cliResult);
        }

        private static SpacetimeCliResult onInstallWasmOptPkgDone(SpacetimeCliResult cliResult)
        {
            // Success results in !CliError and "changed {numPkgs} packages in {numSecs}s
            return cliResult;
        }

        private static PublishServerModuleResult onPublishServerModuleDone(SpacetimeCliResult cliResult)
        {
            // Check for general CLI errs (that may contain false-positives for `spacetime publish`)
            bool hasGeneralCliErr = !cliResult.HasCliErr;
            if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log($"{nameof(hasGeneralCliErr)}=={hasGeneralCliErr}");

            // Dive deeper into the context || error
            PublishServerModuleResult publishResult = new(cliResult);

            if (publishResult.HasPublishErr)
            {
                // This may not necessarily be a major or breaking issue.
                // For example, !optimized builds will show as an "error" rather than warning.
                Debug.LogError($"Server module publish issue found | {publishResult}"); // json
            }
            else if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log($"Server module publish success | {publishResult}"); // json
            
            return publishResult;
        }
        
        /// Uses the `spacetime identity list` CLI command
        public static async Task<GetIdentitiesResult> GetIdentitiesAsync()
        {
            SpacetimeCliResult cliResult = await runCliCommandAsync("spacetime identity list");
            GetIdentitiesResult getIdentitiesResult = new(cliResult); 
            throw new NotImplementedException("TODO");
        }
        #endregion // High Level CLI Actions
    }
}