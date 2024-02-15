using System.Text.RegularExpressions;
using Unity.Plastic.Newtonsoft.Json;

namespace SpacetimeDB.Editor
{
    /// Extends SpacetimeCliResult to catch specific `spacetime publish` results
    public class PublishServerModuleResult : SpacetimeCliResult
    {
        #region Success
        /// `wasm-opt` !found, so the module continued with an "unoptimised" version
        public readonly bool CouldNotFindWasmOpt;

        /// Eg: "http://localhost:3000"
        public readonly string UploadedToUrlAndPort;

        /// Eg: "http://localhost"
        public readonly string UploadedToUrl;

        /// Eg: 3000
        public readonly ushort UploadedToPort;

        /// Not to be confused with UploadedToUrlAndPort
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
        
        /// (!) This may have a different result than base.HasCliErr due to
        /// false positive CLI errors, such as `wasm-opt` not found.
        /// Prioritize this over base.HasCliErr. 
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
                            "<b>Failed:</b> Server host not running");
                    }
                    else
                        this.PublishErrCode = PublishErrorCode.UnknownError;
                }

                // Regardless of err type, stop here
                return;
            }

            // ---------------------
            // Success >>
            this.CouldNotFindWasmOpt = CliOutput.Contains("Could not find wasm-opt");
            this.IsLocal = CliOutput.Contains("Uploading to local =>");
            this.DatabaseAddressHash = getDatabaseAddressHash();

            // Use regex to find the host url from CliOutput.
            // Eg, from "Uploading to local => http://127.0.0.1:3000"
            (string url, string port)? urlPortTuple = getHostUrlFromCliOutput();
            if (urlPortTuple == null)
                return;

            this.UploadedToUrlAndPort = $"{urlPortTuple.Value.url}:{urlPortTuple.Value.port}";
            this.UploadedToUrl = urlPortTuple.Value.url;
            this.UploadedToPort = ushort.Parse(urlPortTuple.Value.port);
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
            string port = match.Groups[3].Value;

            return (url, port);
        }

        /// Eg: "41c6f8bff828bb33356c6104b35efe45"
        private string getDatabaseAddressHash()
        {
            const string pattern = @"Created new database with address: (\w+)";
            Match match = Regex.Match(CliOutput, pattern);

            return match.Success ? match.Groups[1].Value : null;
        }

        /// Returns a json summary
        public override string ToString() => 
            $"{nameof(PublishServerModuleResult)}: " +
            $"{JsonConvert.SerializeObject(this, Formatting.Indented)}";
    }
}