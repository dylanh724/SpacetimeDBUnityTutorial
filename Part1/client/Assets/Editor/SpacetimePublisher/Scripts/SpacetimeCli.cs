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
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return await runCliCommandAsync("iwr https://windows.spacetimedb.com -useb | iex");
                
                case RuntimePlatform.OSXEditor:
                    return await runCliCommandAsync("brew install clockworklabs/tap/spacetime");
                
                case RuntimePlatform.LinuxEditor:
                    return await runCliCommandAsync("curl -sSf https://install.spacetimedb.com | sh");
                
                default:
                    throw new NotImplementedException("Unsupported OS");
            }
        }
        
        public static async Task<bool> CheckIsSpacetimeCliInstalledAsync()
        {
            SpacetimeCliResult cliResult = await runCliCommandAsync("spacetime version");

            bool isSpacetimeCliInstalled = !cliResult.HasErr;
            Debug.Log($"{nameof(isSpacetimeCliInstalled)}=={isSpacetimeCliInstalled}");

            return isSpacetimeCliInstalled;
        }

        private static async Task printDir() =>
            _ = await runCliCommandAsync("pwd");
        
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
                
            if (cliResult.HasErr || LOG_LEVEL == CliLogLevel.Info)
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