using Newtonsoft.Json;

namespace SpacetimeDB.Editor
{
    /// Info passed from the UI to CLI during the CLI `spacetime publish
    /// Print ToString to get the CLI "--project-path {path} {module-name}"
    public class PublishConfig
    {
        /// Usage: "my-server-module-name"
        public readonly string ServerModuleName;

        /// Usage: "absolute/path/to/server/module/dir"
        public readonly string ServerModulePath;

        /// Returns what's sent to the CLI: "--project-path {path} {module-name}"
        public override string ToString() => 
            $"--project-path \"{ServerModulePath}\" {ServerModuleName}";
        

        public PublishConfig(string serverModuleName, string serverModulePath)
        {
            this.ServerModuleName = serverModuleName;
            this.ServerModulePath = serverModulePath;
        }
    }
}