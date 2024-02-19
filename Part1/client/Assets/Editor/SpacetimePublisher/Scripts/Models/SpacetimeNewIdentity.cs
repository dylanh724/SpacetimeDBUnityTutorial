namespace SpacetimeDB.Editor
{
    /// SpacetimeDB CLI Identity { Nickname, Email }
    public class SpacetimeNewIdentity : SpacetimeIdentity
    {
        /// Usage: "a@b.c" || "a+1@b.c"
        public string Email { get; private set; }
        
        public override string ToString() => $"{Nickname} <{Email}> (isDefault? {IsDefault})";
        

        /// Sets nickname + email. Forces default true for base.IsDefault
        public SpacetimeNewIdentity(
            string nickname, 
            string email)
            : base(nickname, isDefault:true)
        {
            this.Email = email;
        }
    }
}