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
        public static async Task InstallSpacetimeCli()
        {
            throw new NotImplementedException("TODO");
        }
        
        public static async Task<bool> CheckIsSpacetimeCliInstalled()
        {
            SpacetimeCliResult cliResult = await runCliCommandAsync("spacetime version");

            bool isSpacetimeCliInstalled = cliResult.HasErr;
            Debug.Log($"{nameof(isSpacetimeCliInstalled)}=={isSpacetimeCliInstalled}");

            return isSpacetimeCliInstalled;
        }
        
        private static async Task<SpacetimeCliResult> runCliCommandAsync(string command)
        {
            string terminal = GetTerminalPrefix(); // Cross-Platform

            using Process process = new();
            process.StartInfo.FileName = terminal;
            process.StartInfo.Arguments = command;
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
                
            if (cliResult.HasErr)
                Debug.LogError($"Error: {error}");

            return cliResult;
        }

        /// Return either "cmd.exe" || "/bin/bash"
        private static string GetTerminalPrefix()
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