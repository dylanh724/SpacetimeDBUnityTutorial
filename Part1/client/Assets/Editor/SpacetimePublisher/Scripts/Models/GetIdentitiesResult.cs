using System.Collections.Generic;

namespace SpacetimeDB.Editor
{
    /// Result of `spacetime identity list`
    public class GetIdentitiesResult : SpacetimeCliResult
    {
        public List<SpacetimeIdentity> Identities { get; private set; }
        public int DefaultIdentityIndex { get; private set; }
        public bool HasIdentity => Identities.Count > 0;
        
        
        public GetIdentitiesResult(SpacetimeCliResult cliResult)
            : base(cliResult.CliOutput, cliResult.CliError)
        {
            Identities = new List<SpacetimeIdentity>();
        }
    }
}