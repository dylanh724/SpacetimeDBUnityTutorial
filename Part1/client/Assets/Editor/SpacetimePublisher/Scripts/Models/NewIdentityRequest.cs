namespace SpacetimeDB.Editor
{
    /// Info passed from the UI to CLI during the CLI `spacetime identity new`
    /// Print ToString to get the CLI "--name {nickname} --email {email}"
    public class NewIdentityRequest : SpacetimeIdentity
    {
        /// Returns what's sent to the CLI: "--name {nickname} --email {email}"
        public override string ToString() => 
            $"--name \"{Nickname}\" --email \"{Email}\"";
        
        public NewIdentityRequest(string nickname, string email)
            : base(nickname, email)
        {
        }
    }
}