namespace SpacetimeDB.Editor
{
    /// Result from SpacetimeCli.runCliCommandAsync
    public class SpacetimeCliResult
    {
        public readonly string Output;
        public readonly string Error;
        public bool HasErr => !string.IsNullOrEmpty(Error);
        
        public SpacetimeCliResult(string output, string error)
        {
            Output = output;
            Error = error;
        }
    }
}
