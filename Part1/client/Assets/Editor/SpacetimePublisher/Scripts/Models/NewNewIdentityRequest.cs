namespace SpacetimeDB.Editor
{
    /// Info passed from the UI to CLI during the CLI `spacetime newIdentity new`
    /// Print ToString to get the CLI "--name {nickname} --email {email}"
    public class NewNewIdentityRequest : SpacetimeNewIdentity
    {
        /// Returns what's sent to the CLI: "--name {nickname} --email {email}"
        public override string ToString() => 
            $"--name \"{Nickname}\" --email \"{Email}\"";
        
        public NewNewIdentityRequest(string nickname, string email)
            : base(nickname, email)
        {
        }
    }
}