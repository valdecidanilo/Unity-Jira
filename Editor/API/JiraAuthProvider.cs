using System;
using System.Text;

namespace OxenteGames.JiraCommunication.API
{
    internal interface IJiraAuthProvider
    {
        string BuildAuthorizationHeader();
    }

    internal sealed class JiraBasicTokenAuthProvider : IJiraAuthProvider
    {
        private readonly string _email;
        private readonly string _apiToken;

        public JiraBasicTokenAuthProvider(string email, string apiToken)
        {
            _email = email?.Trim() ?? string.Empty;
            _apiToken = apiToken ?? string.Empty;
        }

        public string BuildAuthorizationHeader()
        {
            string rawCredentials = $"{_email}:{_apiToken}";
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
            return $"Basic {encoded}";
        }
    }
}
