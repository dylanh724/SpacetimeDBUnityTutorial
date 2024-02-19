using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlasticPipe.PlasticProtocol.Messages;

namespace SpacetimeDB.Editor
{
    /// Result of `spacetime identity list`
    public class GetIdentitiesResult : SpacetimeCliResult
    {
        public List<string> IdentityNicknames { get; private set; }
        public int DefaultIdentityIndex { get; private set; }
        public bool HasIdentity => IdentityNicknames.Count > 0;
        
        
        public GetIdentitiesResult(SpacetimeCliResult cliResult)
            : base(cliResult.CliOutput, cliResult.CliError)
        {
            // Example raw list result below. Read from bottom-up.
            // Ignore the top hashes (TODO: What are top hashes?)
            // ###########################################################################################
            /*
             * Identities for testnet:
             DEFAULT  IDENTITY                                                          NAME            
                      1111111111111111111111111111111111111111111111111111111111111111                  
                      2222222222222222222222222222222222222222222222222222222222222222                  
                      3333333333333333333333333333333333333333333333333333333333333333                  
                      4444444444444444444444444444444444444444444444444444444444444444                  
                      5555555555555555555555555555555555555555555555555555555555555555                  
                      6666666666666666666666666666666666666666666666666666666666666666  Nickname1 
                 ***  7777777777777777777777777777777777777777777777777777777777777777  Nickname2
             */
            // ###########################################################################################
            
            // Split the input string into lines considering the escaped newline characters
            string[] lines = CliOutput.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries); 

            // Initialize the list to store nicknames
            this.IdentityNicknames = new List<string>();

            // Corrected regex pattern to ensure it captures the nickname following the hash and spaces
            // This pattern assumes the nickname is the last element in the line after the hash
            const string pattern = @"\b[a-fA-F0-9]{64}\s+(.+)$";

            foreach (string line in lines)
            {
                Match match = Regex.Match(line, pattern);
                // Check if the line matches the pattern and ensure the match includes the nickname group
                if (match.Success && match.Groups.Count > 1)
                {
                    // Extract and add the nickname to the list; it's the second group in the match
                    string potentialNickname = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(potentialNickname))
                        IdentityNicknames.Add(potentialNickname);
                }
            }
        }
    }
}