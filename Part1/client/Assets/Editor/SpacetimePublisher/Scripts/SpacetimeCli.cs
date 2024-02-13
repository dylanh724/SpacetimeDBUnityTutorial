using System;
using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace SpacetimeDB.Editor
{
    /// CLI action helper for PublisherWindow
    public static class SpacetimeCli
    {
        /// TODO: Possibly integrate this within the PublisherWindow?
        private const CliLogLevel LOG_LEVEL = CliLogLevel.Info;
        
        public enum CliLogLevel
        {
            Info,
            Error,
        }
        
        /// Install the SpacetimeDB CLI | https://spacetimedb.com/install 
        public static async Task<SpacetimeCliResult> InstallSpacetimeCliAsync()
        {
            Debug.Log("Installing SpacetimeDB CLI tool...");
            SpacetimeCliResult result; 
            
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    result = await runCliCommandAsync("powershell -Command \"iwr https://windows.spacetimedb.com -UseBasicParsing | iex\"\n");
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
            
            Debug.Log($"Installed spacetimeDB CLI tool | {PublisherMeta.DOCS_URL}");
            return result;
        }
        
        public static async Task<bool> CheckIsSpacetimeCliInstalledAsync()
        {
            SpacetimeCliResult cliResult = await runCliCommandAsync("spacetime version");

            bool isSpacetimeCliInstalled = !cliResult.HasErr;
            if (LOG_LEVEL == CliLogLevel.Info)
                Debug.Log($"{nameof(isSpacetimeCliInstalled)}=={isSpacetimeCliInstalled}");

            return isSpacetimeCliInstalled;
        }
        
        private static async Task<SpacetimeCliResult> runCliCommandAsync(string argSuffix)
        {
            string terminal = getTerminalPrefix(); // Cross-Platform terminal: cmd || bash
            string argPrefix = getCommandPrefix();
            string parsedArgs = $"{argPrefix} \"{argSuffix}\"";

            using Process process = new();
            process.StartInfo.FileName = terminal;
            process.StartInfo.Arguments = parsedArgs;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

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
                
            bool hasOutput = !string.IsNullOrEmpty(output);
            bool hasLogLevelInfoOrErr = LOG_LEVEL == CliLogLevel.Info || cliResult.HasErr; 
            if (hasOutput && hasLogLevelInfoOrErr)
                Debug.Log($"CLI Output: \n```\n<color=yellow>{output}</color>\n```\n");
            
            if (cliResult.HasErr)
                Debug.LogError($"CLI Error: {error}");

            return cliResult;
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
    }
}