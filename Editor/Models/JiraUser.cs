using System;

namespace OxenteGames.JiraCommunication.Models
{
    [Serializable]
    internal sealed class JiraUser
    {
        public string accountId;
        public string displayName;
        public string emailAddress;
        public bool active;
        public string timeZone;
    }
}
