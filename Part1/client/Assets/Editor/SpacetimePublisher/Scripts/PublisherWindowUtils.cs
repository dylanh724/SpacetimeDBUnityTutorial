using System.Text.RegularExpressions;

namespace SpacetimeDB.Editor
{
    /// Validations, trimming, special formatting
    public partial class PublisherWindow 
    {
        private string replaceSpacesWithDashes(string str) =>
            str?.Replace(" ", "-");
        
        /// Remove ALL whitespace from string
        private string superTrim(string str) =>
            str?.Replace(" ", "");

        /// This checks for valid email chars for OnChange events
        private bool tryFormatAsEmail(string input, out string formattedEmail)
        {
            formattedEmail = null;
            if (string.IsNullOrWhiteSpace(input)) 
                return false;
    
            // Simplified regex pattern to allow characters typically found in emails
            const string emailCharPattern = @"^[a-zA-Z0-9@._+-]+$"; // Allowing "+" (email aliases)
            if (!Regex.IsMatch(input, emailCharPattern))
                return false;
    
            formattedEmail = input;
            return true;
        }

        /// Useful for FocusOut events, checking the entire email for being valid.
        /// At minimum: "a@b.c"
        private bool checkIsValidEmail(string emailStr)
        {
            // No whitespace, contains "@" contains ".", allows "+" (alias), contains chars in between
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(emailStr, pattern);
        }

        /// Useful for FocusOut events, checking the entire host for being valid.
        /// At minimum, must start with "http".
        private bool checkIsValidUrl(string url) => url.StartsWith("http");

        /// Checked at OnFocusOut events to ensure both nickname+email txt fields are valid.
        /// Toggle identityAddBtn enabled based validity of both.
        private void checkIdentityReqsToggleIdentityBtn()
        {
            bool isNicknameValid = !string.IsNullOrWhiteSpace(identityNicknameTxt.value);
            bool isEmailValid = checkIsValidEmail(identityEmailTxt.value);
            identityAddBtn.SetEnabled(isNicknameValid && isEmailValid);
        }
        
        /// Checked at OnFocusOut events to ensure both nickname-host txt fields are valid.
        /// Toggle serverAddBtn enabled based validity of both.
        private void checkServerReqsToggleServerBtn()
        {
            bool isHostValid = checkIsValidUrl(serverHostTxt.value);
            bool isNicknameValid = !string.IsNullOrWhiteSpace(serverNicknameTxt.value);
            serverAddBtn.SetEnabled(isNicknameValid && isHostValid);
        }
    }
}