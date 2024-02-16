using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;

namespace SpacetimeDB.Editor
{
    /// Extends SpacetimeCliResult to catch specific `spacetime publish` results
    public class PublishServerModuleResult : SpacetimeCliResult
    {
        #region Success
        /// The errors may have false-positive warnings; this is the true success checker
        public readonly bool IsSuccessfulPublish;
        
        /// `wasm-opt` !found, so the module continued with an "unoptimised" version
        public readonly bool CouldNotFindWasmOpt;

        /// Eg: "http://localhost:3000" || "https://testnet.spacetimedb.com"
        public readonly string UploadedToHost;

        /// Eg: "http://localhost"
        public readonly string UploadedToUrl;

        /// Eg: 3000
        public readonly ushort UploadedToPort;

        /// Not to be confused with UploadedToHost
        public readonly string DatabaseAddressHash;

        public readonly bool IsLocal;
        #endregion // Success


        #region Errs
        public enum PublishErrorCode
        {
            None,
            MSB1003_InvalidProjectDir,
            OS10061_ServerHostNotRunning,
            UnknownError,
        }

        /// Parsed from known MSBUILD publishing error codes
        /// (!) We currently only catch invalid project dir errors
        public readonly PublishErrorCode PublishErrCode;
        
        /// (!) These may be lesser warnings. For a true indicator of success, check `IsSuccessfulPublish` 
        public bool HasPublishErr => PublishErrCode != PublishErrorCode.None;

        /// You may pass this raw string to the UI, if a known err is caught
        public readonly string StyledFriendlyErrorMessage;
        #endregion // Errs


        public PublishServerModuleResult(SpacetimeCliResult cliResult)
            : base(cliResult.CliOutput, cliResult.CliError)
        {
            bool hasOutputErr = CliOutput.Contains("Error:");

            if (cliResult.HasCliErr || hasOutputErr)
            {
                if (cliResult.HasCliErr)
                {
                    // CliError >>
                    bool hasErrWorkingProjDirNotFound =
                        cliResult.HasCliErr &&
                        cliResult.CliError.Contains("Working project directory not found");

                    if (hasErrWorkingProjDirNotFound)
                    {
                        this.PublishErrCode = PublishErrorCode.MSB1003_InvalidProjectDir;
                        this.StyledFriendlyErrorMessage = PublisherMeta.GetStyledStr(
                            PublisherMeta.StringStyle.Error,
                            "<b>Failed:</b> Invalid server module dir");
                    }
                }
            
                // CLI resulted success, but what about an internal error specific to publisher?
                if (cliResult.CliError.Contains("Error:"))
                {
                    bool isServerNotRunning = cliResult.CliError.Contains("os error 10061");

                    if (isServerNotRunning)
                    {
                        this.PublishErrCode = PublishErrorCode.OS10061_ServerHostNotRunning;
                        this.StyledFriendlyErrorMessage = PublisherMeta.GetStyledStr(
                            PublisherMeta.StringStyle.Error,
                            "<b>Failed:</b> Server host not running:\n" +
                            "(1) Open terminal\n" +
                            "(2) `spacetime start`\n" +
                            "(3) Try again");
                    }
                    else
                        this.PublishErrCode = PublishErrorCode.UnknownError;
                }

                // Check for false-positive errs (that are more-so warnings)
                this.IsSuccessfulPublish = CliOutput.Contains("Updated database with domain");
                
                if (!IsSuccessfulPublish)
                    return;
            }
 
            // ---------------------
            // Success >>
            this.CouldNotFindWasmOpt = CliOutput.Contains("Could not find wasm-opt");
            this.IsLocal = CliOutput.Contains("Uploading to local =>");
            this.DatabaseAddressHash = getDatabaseAddressHash();

            // Use regex to find the host url from CliOutput.
            // Eg, from "Uploading to local => http://127.0.0.1:3000"
            // Eg, from "Uploading to testnet => https://testnet.spacetimedb.com"
            (string url, string port)? urlPortTuple = getHostUrlFromCliOutput();
            if (urlPortTuple == null)
                return;

            this.UploadedToHost = $"{urlPortTuple.Value.url}:{urlPortTuple.Value.port}";
            this.UploadedToUrl = urlPortTuple.Value.url;
            
            // There may not be a port
            if (ushort.TryParse(urlPortTuple.Value.port, out ushort parsedPort))
                this.UploadedToPort = parsedPort; // Parsing successful, update with the parsed value
            else
            {
                // No port - assume based on http(s) prefix
                this.UploadedToPort = UploadedToHost.StartsWith("https") 
                    ? (ushort)443 // ssl
                    : (ushort)80; // !ssl
                
                // We also need to remove the `:` from the url host
                this.UploadedToHost = UploadedToHost.Replace(":", "");
            }
        }

        /// Use regex to find the host url from CliOutput.
        /// Eg, from "Uploading to local => http://127.0.0.1:3000"
        private (string url, string port)? getHostUrlFromCliOutput()
        {
            const string pattern = @"Uploading to .* => (https?://([^:/\s]+)(?::(\d+))?)";
            Match match = Regex.Match(CliOutput, pattern);

            if (!match.Success)
                return null;

            string url = match.Groups[2].Value;
            string port = match.Groups[3].Value; // Optional
            
            // url sanity check
            if (string.IsNullOrEmpty(url))
                Debug.LogError("Failed to parse host url from CliOutput");

            return (url, port);
        }

        /// This could either be from "created" or "updated" prompts.
        /// Eg: "41c6f8bff828bb33356c6104b35efe45"
        private string getDatabaseAddressHash()
        {
            // Check for updated
            const string updatedPattern = @"Updated database with domain:.+address: (\w+)";
            Match match = Regex.Match(CliOutput, updatedPattern);
            if (match.Success)
                return match.Groups[1].Value;
            
            // Check for created
            const string createdPattern = @"Created new database with address: (\S+)";
            match = Regex.Match(CliOutput, createdPattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// Returns a json summary
        public override string ToString() => 
            $"{nameof(PublishServerModuleResult)}: " +
            $"{JsonConvert.SerializeObject(this, Formatting.Indented)}";
    }
}