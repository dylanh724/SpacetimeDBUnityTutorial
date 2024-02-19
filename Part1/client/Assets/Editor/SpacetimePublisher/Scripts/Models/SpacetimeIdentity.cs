namespace SpacetimeDB.Editor
{
    /// SpacetimeDB CLI Identity { Nickname, Email }
    public class SpacetimeIdentity
    {
        /// Usage: "My-Case-InSeNsItIvE-Nickname" (underscores are also ok)
        public readonly string Nickname;

        /// Usage: "a@b.c" || "a+1@b.c"
        public readonly string Email;

        public override string ToString() => $"{Nickname} <{Email}>";
        

        public SpacetimeIdentity(string nickname, string email)
        {
            this.Nickname = nickname;
            this.Email = email;
        }
    }
}